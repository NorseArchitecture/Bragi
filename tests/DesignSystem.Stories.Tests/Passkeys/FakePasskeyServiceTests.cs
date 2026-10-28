using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Account;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Passkeys;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.Primitives;

namespace Norse.DesignSystem.Stories.Tests.Passkeys;

/// <summary>
///     Parity with Himinbjörg's <c>Passkeys/</c> handlers: entries and rename URLs in <c>CredentialIds</c>'s shape, the
///     <c>ManageUrls.Status</c> landing, and the handlers' own id refusals.
/// </summary>
public sealed class FakePasskeyServiceTests
{
	static readonly PasskeyEntry _workLaptop =
		new() { CredentialId = "cred-one", Name = "Work laptop", RenameUrl = "/Account/Manage/RenamePasskey/cred-one" };

	static readonly PasskeyEntry _unnamed =
		new() { CredentialId = "cred-two", Name = "", RenameUrl = "/Account/Manage/RenamePasskey/cred-two" };

	static FakePasskeyService CreateFake(AccountScenario scenario) =>
		new(new Scenario<AccountScenario>(scenario));

	static CancellationToken Cancellation => TestContext.Current.CancellationToken;

	static AddPasskeyRequest AnyAdd() =>
		new() { CredentialJson = "{}", AttestationState = "story" };

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

	static void ShouldBeTheInvalidId(Problem problem)
	{
		problem.Category.ShouldBe(ErrorCategory.Validation);
		problem.Errors.Keys.ShouldBe([nameof(GetPasskeyRequest.CredentialId)]);
		problem.Errors[nameof(GetPasskeyRequest.CredentialId)].ShouldBe(["The passkey id is not valid."]);
	}

	static void ShouldBeNotFound(Problem problem)
	{
		problem.Category.ShouldBe(ErrorCategory.NotFound);
		problem.Errors[string.Empty].ShouldBe(["The passkey was not found."]);
	}

	[Fact]
	async Task GetPasskeys_under_Success_lists_a_named_and_an_unnamed_passkey() =>
		ValueOf(await CreateFake(AccountScenario.Success).GetPasskeys(Cancellation)).Passkeys.ShouldBe([_workLaptop, _unnamed]);

	[Fact]
	async Task GetPasskeys_under_NoPasskeys_lists_none() =>
		ValueOf(await CreateFake(AccountScenario.NoPasskeys).GetPasskeys(Cancellation)).Passkeys.ShouldBeEmpty();

	[Theory]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	[InlineData(AccountScenario.MachineRemembered)]
	async Task GetPasskeys_under_any_other_scenario_answers_the_Success_fixture(AccountScenario scenario) =>
		ValueOf(await CreateFake(scenario).GetPasskeys(Cancellation)).Passkeys
			.ShouldBe(ValueOf(await CreateFake(AccountScenario.Success).GetPasskeys(Cancellation)).Passkeys);

	// One story host serves every circuit, so no answer may share state with the next.
	[Fact]
	async Task A_caller_mutating_an_answer_never_changes_the_next_one()
	{
		var fake = CreateFake(AccountScenario.Success);
		var passkeys = ValueOf(await fake.GetPasskeys(Cancellation)).Passkeys;
		passkeys[0].Name = "mutated";
		passkeys[1] = _workLaptop;
		ValueOf(await fake.GetPasskey(new() { CredentialId = "cred-two" }, Cancellation)).RenameUrl = "mutated";
		ValueOf(await fake.GetPasskeyCreationOptions(Cancellation)).AttestationState = "mutated";
		ProblemOf(await fake.GetPasskey(new() { CredentialId = "not an id!" }, Cancellation))
			.Errors[nameof(GetPasskeyRequest.CredentialId)][0] = "mutated";

		ValueOf(await fake.GetPasskeys(Cancellation)).Passkeys.ShouldBe([_workLaptop, _unnamed]);
		ValueOf(await fake.GetPasskey(new() { CredentialId = "cred-two" }, Cancellation)).ShouldBe(_unnamed);
		ValueOf(await fake.GetPasskeyCreationOptions(Cancellation)).AttestationState.ShouldBe("story");
		ShouldBeTheInvalidId(ProblemOf(await fake.GetPasskey(new() { CredentialId = "not an id!" }, Cancellation)));
	}

	[Fact]
	async Task GetPasskeys_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).GetPasskeys(Cancellation)));

	[Fact]
	async Task GetPasskeys_throws_loudly_on_the_sentinel() =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(AccountScenario.Unspecified).GetPasskeys(Cancellation));

	[Theory]
	[InlineData("cred-one")]
	[InlineData("cred-two")]
	async Task GetPasskey_under_Success_answers_the_listed_entry(string credentialId) =>
		ValueOf(await CreateFake(AccountScenario.Success).GetPasskey(new() { CredentialId = credentialId }, Cancellation))
			.ShouldBe(credentialId == "cred-one" ? _workLaptop : _unnamed);

	[Fact]
	async Task GetPasskey_refuses_an_id_that_is_not_base64url() =>
		ShouldBeTheInvalidId(ProblemOf(await CreateFake(AccountScenario.Success).GetPasskey(new() { CredentialId = "not an id!" }, Cancellation)));

	[Fact]
	async Task GetPasskey_answers_NotFound_for_a_passkey_the_caller_does_not_have() =>
		ShouldBeNotFound(ProblemOf(await CreateFake(AccountScenario.Success).GetPasskey(new() { CredentialId = "cred-six" }, Cancellation)));

	[Fact]
	async Task GetPasskey_under_NoPasskeys_finds_nothing() =>
		ShouldBeNotFound(ProblemOf(await CreateFake(AccountScenario.NoPasskeys).GetPasskey(new() { CredentialId = "cred-one" }, Cancellation)));

	[Fact]
	async Task GetPasskey_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).GetPasskey(new() { CredentialId = "cred-one" }, Cancellation)));

	[Fact]
	async Task GetPasskey_throws_loudly_on_the_sentinel() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AccountScenario.Unspecified).GetPasskey(new() { CredentialId = "cred-one" }, Cancellation));

	// Adding the first passkey is exactly what an account with none does.
	[Theory]
	[InlineData(AccountScenario.Success)]
	[InlineData(AccountScenario.NoPasskeys)]
	async Task GetPasskeyCreationOptions_answers_the_story_ceremony(AccountScenario scenario)
	{
		var options = ValueOf(await CreateFake(scenario).GetPasskeyCreationOptions(Cancellation));
		options.OptionsJson.ShouldBe("{}");
		options.AttestationState.ShouldBe("story");
	}

	[Fact]
	async Task GetPasskeyCreationOptions_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).GetPasskeyCreationOptions(Cancellation)));

	[Fact]
	async Task GetPasskeyCreationOptions_throws_loudly_on_the_sentinel() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AccountScenario.Unspecified).GetPasskeyCreationOptions(Cancellation));

	// A new passkey is unnamed, so the handler lands on the unnamed fixture's rename page.
	[Fact]
	async Task AddPasskey_under_Success_lands_on_the_new_passkeys_rename_page() =>
		ValueOf(await CreateFake(AccountScenario.Success).AddPasskey(AnyAdd(), Cancellation)).NextUrl
			.ShouldBe("/Account/Manage/RenamePasskey/cred-two");

	[Fact]
	async Task AddPasskey_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).AddPasskey(AnyAdd(), Cancellation)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task AddPasskey_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(scenario).AddPasskey(AnyAdd(), Cancellation));

	[Fact]
	async Task RenamePasskey_under_Success_lands_on_the_passkeys_page_with_the_updated_key() =>
		ValueOf(await CreateFake(AccountScenario.Success).RenamePasskey(new() { CredentialId = "cred-one", Name = "Home desktop" }, Cancellation))
			.NextUrl.ShouldBe("/Account/Manage/Passkeys?status=passkey-updated");

	[Fact]
	async Task RenamePasskey_refuses_an_id_that_is_not_base64url() =>
		ShouldBeTheInvalidId(ProblemOf(await CreateFake(AccountScenario.Success)
			.RenamePasskey(new() { CredentialId = "not an id!", Name = "Home desktop" }, Cancellation)));

	[Fact]
	async Task RenamePasskey_answers_NotFound_for_a_passkey_the_caller_does_not_have() =>
		ShouldBeNotFound(ProblemOf(await CreateFake(AccountScenario.Success)
			.RenamePasskey(new() { CredentialId = "cred-six", Name = "Home desktop" }, Cancellation)));

	[Fact]
	async Task RenamePasskey_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault)
			.RenamePasskey(new() { CredentialId = "cred-one", Name = "Home desktop" }, Cancellation)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.NoPasskeys)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task RenamePasskey_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(scenario).RenamePasskey(new() { CredentialId = "cred-one", Name = "Home desktop" }, Cancellation));

	[Fact]
	async Task RemovePasskey_under_Success_acknowledges_true() =>
		ValueOf(await CreateFake(AccountScenario.Success).RemovePasskey(new() { CredentialId = "cred-one" }, Cancellation))
			.Value.ShouldBeTrue();

	[Fact]
	async Task RemovePasskey_refuses_an_id_that_is_not_base64url() =>
		ShouldBeTheInvalidId(ProblemOf(await CreateFake(AccountScenario.Success).RemovePasskey(new() { CredentialId = "not an id!" }, Cancellation)));

	[Fact]
	async Task RemovePasskey_answers_NotFound_for_a_passkey_the_caller_does_not_have() =>
		ShouldBeNotFound(ProblemOf(await CreateFake(AccountScenario.Success).RemovePasskey(new() { CredentialId = "cred-six" }, Cancellation)));

	[Fact]
	async Task RemovePasskey_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).RemovePasskey(new() { CredentialId = "cred-one" }, Cancellation)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.NoPasskeys)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task RemovePasskey_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(scenario).RemovePasskey(new() { CredentialId = "cred-one" }, Cancellation));
}
