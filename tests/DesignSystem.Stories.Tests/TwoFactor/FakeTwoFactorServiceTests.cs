using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Account;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.DesignSystem.Stories.TwoFactor;
using Norse.Primitives;

namespace Norse.DesignSystem.Stories.Tests.TwoFactor;

/// <summary>
///     Parity with Himinbjörg's <c>TwoFactor/</c> handlers: every <c>NextUrl</c> is the one the handler authors under an
///     empty <c>PathBase</c> with nothing deferred, and every refusal is the handler's own text and key.
/// </summary>
public sealed class FakeTwoFactorServiceTests
{
	static readonly string[] _codes =
	[
		"2KQ8M-7XRTN", "4WJ3P-9CHVL", "6DNB5-3GFQZ", "8TYV2-5MKWR", "AXC7H-2PLJD",
		"CRF4N-8VZBT", "EHQ9W-6DMXK", "GLT3B-4RNYC", "JPZ6K-7WHFV", "LVD2X-9QTMB"
	];

	static FakeTwoFactorService CreateFake(AccountScenario scenario) =>
		new(new Scenario<AccountScenario>(scenario));

	static CancellationToken Cancellation => TestContext.Current.CancellationToken;

	static EnableAuthenticatorRequest AnyEnable() =>
		new() { Code = "123456" };

	static RevealRecoveryCodesRequest AnyReveal() =>
		new() { Handoff = "story-handoff" };

	static Problem ProblemOf<T>(Outcome<T> outcome)
		where T : notnull
	{
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		return failed.Problem;
	}

	static T ValueOf<T>(Outcome<T> outcome)
		where T : notnull
	{
		outcome.TryGetValue(out Success<T> success).ShouldBeTrue();
		return success.Value;
	}

	static void ShouldBeTheCatalogFault(Problem problem)
	{
		problem.Category.ShouldBe(ErrorCategory.Fault);
		problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	// AuthenticatorSetup.FormatKey and .Uri over the stored key ABCDEFGHIJKLMNOP, the development issuer, and the
	// catalog's signed-in designer; UrlEncoder.Default leaves the label's @ as it is.
	[Fact]
	async Task GetAuthenticatorSetup_under_Success_answers_the_formatted_key_its_uri_and_a_fixed_svg()
	{
		var setup = ValueOf(await CreateFake(AccountScenario.Success).GetAuthenticatorSetup(Cancellation));
		setup.SharedKey.ShouldBe("abcd efgh ijkl mnop");
		setup.AuthenticatorUri.ShouldBe("otpauth://totp/Norse:designer@example.com?secret=ABCDEFGHIJKLMNOP&issuer=Norse&digits=6");
		setup.QrCodeSvg.ShouldStartWith("<svg xmlns=\"http://www.w3.org/2000/svg\"");
		setup.QrCodeSvg.ShouldNotContain("script", Case.Insensitive);
	}

	// A write story must still load its form.
	[Theory]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	[InlineData(AccountScenario.MachineRemembered)]
	async Task GetAuthenticatorSetup_under_any_other_scenario_answers_the_Success_fixture(AccountScenario scenario) =>
		ValueOf(await CreateFake(scenario).GetAuthenticatorSetup(Cancellation))
			.ShouldBe(ValueOf(await CreateFake(AccountScenario.Success).GetAuthenticatorSetup(Cancellation)));

	// One story host serves every circuit, so no answer may share state with the next.
	[Fact]
	async Task A_caller_mutating_an_answer_never_changes_the_next_one()
	{
		var fake = CreateFake(AccountScenario.Success);
		var setup = ValueOf(await fake.GetAuthenticatorSetup(Cancellation));
		setup.SharedKey = "mutated";
		ValueOf(await fake.RevealRecoveryCodes(AnyReveal(), Cancellation)).RecoveryCodes[0] = "mutated";
		ValueOf(await fake.GenerateRecoveryCodes(Cancellation)).RecoveryCodes[0] = "mutated";
		var refused = ProblemOf(await CreateFake(AccountScenario.InvalidVerificationCode).EnableAuthenticator(AnyEnable(), Cancellation));
		refused.Errors[nameof(EnableAuthenticatorRequest.Code)][0] = "mutated";

		ValueOf(await fake.GetAuthenticatorSetup(Cancellation)).SharedKey.ShouldBe("abcd efgh ijkl mnop");
		ValueOf(await fake.RevealRecoveryCodes(AnyReveal(), Cancellation)).RecoveryCodes.ShouldBe(_codes);
		ValueOf(await fake.GenerateRecoveryCodes(Cancellation)).RecoveryCodes.ShouldBe(_codes);
		ProblemOf(await CreateFake(AccountScenario.InvalidVerificationCode).EnableAuthenticator(AnyEnable(), Cancellation))
			.Errors[nameof(EnableAuthenticatorRequest.Code)].ShouldBe(["Verification code is invalid."]);
	}

	[Fact]
	async Task GetAuthenticatorSetup_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).GetAuthenticatorSetup(Cancellation)));

	[Fact]
	async Task GetAuthenticatorSetup_throws_loudly_on_the_sentinel() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AccountScenario.Unspecified).GetAuthenticatorSetup(Cancellation));

	// The Success account still holds recovery codes, so the handler issues none and lands on the two-factor page.
	[Fact]
	async Task EnableAuthenticator_under_Success_lands_on_the_two_factor_page_with_the_verified_key() =>
		ValueOf(await CreateFake(AccountScenario.Success).EnableAuthenticator(AnyEnable(), Cancellation)).NextUrl
			.ShouldBe("/Account/Manage/TwoFactorAuthentication?status=authenticator-verified");

	// No codes remain on a TwoFactorDisabled account, so the handler issues ten and seals them into the handoff.
	[Fact]
	async Task EnableAuthenticator_under_TwoFactorDisabled_hands_the_new_codes_off_to_the_reveal_page() =>
		ValueOf(await CreateFake(AccountScenario.TwoFactorDisabled).EnableAuthenticator(AnyEnable(), Cancellation)).NextUrl
			.ShouldBe("/Account/Manage/ShowRecoveryCodes?handoff=story-handoff");

	[Fact]
	async Task EnableAuthenticator_under_InvalidVerificationCode_keys_the_refusal_to_the_code()
	{
		var problem = ProblemOf(await CreateFake(AccountScenario.InvalidVerificationCode).EnableAuthenticator(AnyEnable(), Cancellation));
		problem.Category.ShouldBe(ErrorCategory.Validation);
		problem.Errors.Keys.ShouldBe([nameof(EnableAuthenticatorRequest.Code)]);
		problem.Errors[nameof(EnableAuthenticatorRequest.Code)].ShouldBe(["Verification code is invalid."]);
	}

	[Fact]
	async Task EnableAuthenticator_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).EnableAuthenticator(AnyEnable(), Cancellation)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.ExternalOnly)]
	[InlineData(AccountScenario.NoPasskeys)]
	[InlineData(AccountScenario.IncorrectPassword)]
	[InlineData(AccountScenario.MachineRemembered)]
	async Task EnableAuthenticator_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(scenario).EnableAuthenticator(AnyEnable(), Cancellation));

	[Fact]
	async Task RevealRecoveryCodes_under_Success_answers_the_ten_codes() =>
		ValueOf(await CreateFake(AccountScenario.Success).RevealRecoveryCodes(AnyReveal(), Cancellation)).RecoveryCodes.ShouldBe(_codes);

	[Fact]
	async Task RevealRecoveryCodes_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).RevealRecoveryCodes(AnyReveal(), Cancellation)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task RevealRecoveryCodes_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(scenario).RevealRecoveryCodes(AnyReveal(), Cancellation));

	// The reset refuses nothing: disabling an already-disabled factor is no refusal, so both land on setup.
	[Theory]
	[InlineData(AccountScenario.Success)]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	async Task ResetAuthenticator_lands_on_the_setup_page_with_the_reset_key(AccountScenario scenario) =>
		ValueOf(await CreateFake(scenario).ResetAuthenticator(Cancellation)).NextUrl
			.ShouldBe("/Account/Manage/EnableAuthenticator?status=authenticator-reset");

	[Fact]
	async Task ResetAuthenticator_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).ResetAuthenticator(Cancellation)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	[InlineData(AccountScenario.MachineRemembered)]
	async Task ResetAuthenticator_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(scenario).ResetAuthenticator(Cancellation));

	[Fact]
	async Task DisableTwoFactor_under_Success_lands_on_the_two_factor_page_with_the_disabled_key() =>
		ValueOf(await CreateFake(AccountScenario.Success).DisableTwoFactor(Cancellation)).NextUrl
			.ShouldBe("/Account/Manage/TwoFactorAuthentication?status=two-factor-disabled");

	[Fact]
	async Task DisableTwoFactor_under_TwoFactorDisabled_refuses_with_the_handlers_validation_text()
	{
		var problem = ProblemOf(await CreateFake(AccountScenario.TwoFactorDisabled).DisableTwoFactor(Cancellation));
		problem.Category.ShouldBe(ErrorCategory.Validation);
		problem.Errors.Keys.ShouldBe([string.Empty]);
		problem.Errors[string.Empty].ShouldBe(["Two-factor authentication is not enabled."]);
	}

	[Fact]
	async Task DisableTwoFactor_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).DisableTwoFactor(Cancellation)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	[InlineData(AccountScenario.MachineRemembered)]
	async Task DisableTwoFactor_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(scenario).DisableTwoFactor(Cancellation));

	[Fact]
	async Task GenerateRecoveryCodes_under_Success_answers_the_ten_codes() =>
		ValueOf(await CreateFake(AccountScenario.Success).GenerateRecoveryCodes(Cancellation)).RecoveryCodes.ShouldBe(_codes);

	[Fact]
	async Task GenerateRecoveryCodes_under_TwoFactorDisabled_refuses_with_the_handlers_validation_text()
	{
		var problem = ProblemOf(await CreateFake(AccountScenario.TwoFactorDisabled).GenerateRecoveryCodes(Cancellation));
		problem.Category.ShouldBe(ErrorCategory.Validation);
		problem.Errors.Keys.ShouldBe([string.Empty]);
		problem.Errors[string.Empty].ShouldBe(["Cannot generate recovery codes because two-factor authentication is not enabled."]);
	}

	[Fact]
	async Task GenerateRecoveryCodes_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).GenerateRecoveryCodes(Cancellation)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	[InlineData(AccountScenario.MachineRemembered)]
	async Task GenerateRecoveryCodes_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(scenario).GenerateRecoveryCodes(Cancellation));

	// The forget button renders only for a remembered machine; forgetting an unremembered one is no refusal.
	[Theory]
	[InlineData(AccountScenario.Success)]
	[InlineData(AccountScenario.MachineRemembered)]
	async Task ForgetTwoFactorClient_lands_on_the_two_factor_page_with_the_forgotten_key(AccountScenario scenario) =>
		ValueOf(await CreateFake(scenario).ForgetTwoFactorClient(Cancellation)).NextUrl
			.ShouldBe("/Account/Manage/TwoFactorAuthentication?status=browser-forgotten");

	[Fact]
	async Task ForgetTwoFactorClient_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).ForgetTwoFactorClient(Cancellation)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task ForgetTwoFactorClient_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(scenario).ForgetTwoFactorClient(Cancellation));
}
