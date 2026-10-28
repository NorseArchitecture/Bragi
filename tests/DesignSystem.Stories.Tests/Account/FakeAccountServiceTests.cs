using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Account;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.Primitives;

namespace Norse.DesignSystem.Stories.Tests.Account;

public sealed class FakeAccountServiceTests
{
	// One holder + fake per test, pinned to the scenario under test — the fake itself is stateless;
	// all behavior selection lives in the ambient value.
	static FakeAccountService CreateFake(AccountScenario scenario) =>
		new(new Scenario<AccountScenario>(scenario));

	static SetPhoneNumberRequest AnySetPhoneNumber() =>
		new() { PhoneInput = "+15551234567" };

	static ChangePasswordRequest AnyChangePassword() =>
		new() { OldPassword = "aaaaaaaa", NewPassword = "bbbbbbbb" };

	static SetPasswordRequest AnySetPassword() =>
		new() { NewPassword = "aaaaaaaa" };

	static ChangeEmailRequest AnyChangeEmail() =>
		new() { NewEmailInput = "new@example.com" };

	static RemoveExternalLoginRequest AnyRemoveExternalLogin() =>
		new() { LoginProvider = "Google", ProviderKey = "story-google" };

	static DeletePersonalDataRequest AnyDeletePersonalData() =>
		new() { Password = "aaaaaaaa" };

	static async Task<AccountStatusResponse> StatusUnder(AccountScenario scenario)
	{
		var outcome = await CreateFake(scenario).GetAccountStatus(TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<AccountStatusResponse> success).ShouldBeTrue();
		return success.Value;
	}

	static async Task<ExternalLoginsResponse> LoginsUnder(AccountScenario scenario)
	{
		var outcome = await CreateFake(scenario).GetExternalLogins(TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<ExternalLoginsResponse> success).ShouldBeTrue();
		return success.Value;
	}

	static Problem ProblemOf<T>(Outcome<T> outcome)
		where T : notnull
	{
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		return failed.Problem;
	}

	static string NextUrlOf(Outcome<NavigationResult> outcome)
	{
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		return success.Value.NextUrl;
	}

	static void ShouldBeTheCatalogFault(Problem problem)
	{
		problem.Category.ShouldBe(ErrorCategory.Fault);
		problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task GetAccountStatus_under_Success_pins_the_fully_configured_account() =>
		(await StatusUnder(AccountScenario.Success)).ShouldBe(new AccountStatusResponse
		{
			HasPassword = true,
			EmailConfirmed = true,
			TwoFactorEnabled = true,
			HasAuthenticator = true,
			RecoveryCodesLeft = 3,
			IsMachineRemembered = false,
			PasskeyCount = 2,
			ExternalLoginCount = 1
		});

	[Fact]
	async Task GetAccountStatus_under_ExternalOnly_has_no_password() =>
		(await StatusUnder(AccountScenario.ExternalOnly))
			.ShouldBe(await StatusUnder(AccountScenario.Success) with { HasPassword = false });

	[Fact]
	async Task GetAccountStatus_under_TwoFactorDisabled_has_no_second_factor() =>
		(await StatusUnder(AccountScenario.TwoFactorDisabled))
			.ShouldBe(await StatusUnder(AccountScenario.Success) with { TwoFactorEnabled = false, HasAuthenticator = false, RecoveryCodesLeft = 0 });

	[Fact]
	async Task GetAccountStatus_under_RecoveryCodesLow_has_one_code_left() =>
		(await StatusUnder(AccountScenario.RecoveryCodesLow))
			.ShouldBe(await StatusUnder(AccountScenario.Success) with { RecoveryCodesLeft = 1 });

	[Fact]
	async Task GetAccountStatus_under_NoPasskeys_has_no_passkeys() =>
		(await StatusUnder(AccountScenario.NoPasskeys))
			.ShouldBe(await StatusUnder(AccountScenario.Success) with { PasskeyCount = 0 });

	// Reads must succeed for a write story to render its form.
	[Theory]
	[InlineData(AccountScenario.IncorrectPassword)]
	[InlineData(AccountScenario.PasswordPolicy)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task GetAccountStatus_under_a_write_scenario_answers_the_Success_status(AccountScenario scenario) =>
		(await StatusUnder(scenario)).ShouldBe(await StatusUnder(AccountScenario.Success));

	[Fact]
	async Task GetAccountStatus_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).GetAccountStatus(TestContext.Current.CancellationToken)));

	[Fact]
	async Task GetAccountStatus_throws_loudly_on_the_sentinel() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AccountScenario.Unspecified).GetAccountStatus(TestContext.Current.CancellationToken));

	[Fact]
	async Task GetExternalLogins_under_Success_pins_one_removable_login_and_one_available_provider()
	{
		var logins = await LoginsUnder(AccountScenario.Success);
		logins.Logins.ShouldBe([new ExternalLoginEntry { LoginProvider = "Google", ProviderKey = "story-google", DisplayName = "Google" }]);
		logins.AvailableProviders.ShouldBe([new ExternalProviderEntry { Name = "Microsoft", DisplayName = "Microsoft Account" }]);
		logins.CanRemove.ShouldBeTrue();
	}

	[Fact]
	async Task GetExternalLogins_under_ExternalOnly_offers_no_remove()
	{
		var logins = await LoginsUnder(AccountScenario.ExternalOnly);
		var success = await LoginsUnder(AccountScenario.Success);
		logins.CanRemove.ShouldBeFalse();
		logins.Logins.ShouldBe(success.Logins);
		logins.AvailableProviders.ShouldBe(success.AvailableProviders);
	}

	[Theory]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.RecoveryCodesLow)]
	[InlineData(AccountScenario.NoPasskeys)]
	[InlineData(AccountScenario.IncorrectPassword)]
	[InlineData(AccountScenario.PasswordPolicy)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task GetExternalLogins_under_any_other_scenario_answers_the_Success_fixture(AccountScenario scenario) =>
		(await LoginsUnder(scenario)).ShouldBeSameAs(await LoginsUnder(AccountScenario.Success));

	[Fact]
	async Task GetExternalLogins_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).GetExternalLogins(TestContext.Current.CancellationToken)));

	[Fact]
	async Task GetExternalLogins_throws_loudly_on_the_sentinel() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AccountScenario.Unspecified).GetExternalLogins(TestContext.Current.CancellationToken));

	[Fact]
	async Task SetPhoneNumber_under_Success_routes_to_the_manage_index_with_the_phone_updated_key() =>
		NextUrlOf(await CreateFake(AccountScenario.Success).SetPhoneNumber(AnySetPhoneNumber(), TestContext.Current.CancellationToken))
			.ShouldBe("/Account/Manage?status=phone-updated");

	[Fact]
	async Task SetPhoneNumber_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).SetPhoneNumber(AnySetPhoneNumber(), TestContext.Current.CancellationToken)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.ExternalOnly)]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.RecoveryCodesLow)]
	[InlineData(AccountScenario.NoPasskeys)]
	[InlineData(AccountScenario.IncorrectPassword)]
	[InlineData(AccountScenario.PasswordPolicy)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task SetPhoneNumber_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(scenario).SetPhoneNumber(AnySetPhoneNumber(), TestContext.Current.CancellationToken));

	[Fact]
	async Task ChangePassword_under_Success_routes_to_the_change_password_page_with_the_changed_key() =>
		NextUrlOf(await CreateFake(AccountScenario.Success).ChangePassword(AnyChangePassword(), TestContext.Current.CancellationToken))
			.ShouldBe("/Account/Manage/ChangePassword?status=password-changed");

	[Fact]
	async Task ChangePassword_under_IncorrectPassword_keys_the_mismatch_to_the_old_password()
	{
		var problem = ProblemOf(await CreateFake(AccountScenario.IncorrectPassword).ChangePassword(AnyChangePassword(), TestContext.Current.CancellationToken));
		problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
		problem.Errors.Keys.ShouldBe([nameof(ChangePasswordRequest.OldPassword)]);
		problem.Errors[nameof(ChangePasswordRequest.OldPassword)].ShouldBe(["Incorrect password."]);
	}

	[Fact]
	async Task ChangePassword_under_PasswordPolicy_keys_the_rule_to_the_new_password()
	{
		var problem = ProblemOf(await CreateFake(AccountScenario.PasswordPolicy).ChangePassword(AnyChangePassword(), TestContext.Current.CancellationToken));
		problem.Category.ShouldBe(ErrorCategory.Validation);
		problem.Errors.Keys.ShouldBe([nameof(ChangePasswordRequest.NewPassword)]);
		problem.Errors[nameof(ChangePasswordRequest.NewPassword)].ShouldBe(["Passwords must have at least one digit ('0'-'9')."]);
	}

	[Fact]
	async Task ChangePassword_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).ChangePassword(AnyChangePassword(), TestContext.Current.CancellationToken)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.ExternalOnly)]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.RecoveryCodesLow)]
	[InlineData(AccountScenario.NoPasskeys)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task ChangePassword_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(scenario).ChangePassword(AnyChangePassword(), TestContext.Current.CancellationToken));

	[Fact]
	async Task SetPassword_under_Success_routes_to_the_change_password_page_with_the_set_key() =>
		NextUrlOf(await CreateFake(AccountScenario.Success).SetPassword(AnySetPassword(), TestContext.Current.CancellationToken))
			.ShouldBe("/Account/Manage/ChangePassword?status=password-set");

	[Fact]
	async Task SetPassword_under_PasswordPolicy_keys_the_rule_to_the_new_password()
	{
		var problem = ProblemOf(await CreateFake(AccountScenario.PasswordPolicy).SetPassword(AnySetPassword(), TestContext.Current.CancellationToken));
		problem.Category.ShouldBe(ErrorCategory.Validation);
		problem.Errors.Keys.ShouldBe([nameof(SetPasswordRequest.NewPassword)]);
		problem.Errors[nameof(SetPasswordRequest.NewPassword)].ShouldBe(["Passwords must have at least one digit ('0'-'9')."]);
	}

	[Fact]
	async Task SetPassword_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).SetPassword(AnySetPassword(), TestContext.Current.CancellationToken)));

	// An external-only account is exactly the one that sets a password.
	[Fact]
	async Task SetPassword_under_ExternalOnly_answers_the_Success_route() =>
		NextUrlOf(await CreateFake(AccountScenario.ExternalOnly).SetPassword(AnySetPassword(), TestContext.Current.CancellationToken))
			.ShouldBe("/Account/Manage/ChangePassword?status=password-set");

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.RecoveryCodesLow)]
	[InlineData(AccountScenario.NoPasskeys)]
	[InlineData(AccountScenario.IncorrectPassword)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task SetPassword_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(scenario).SetPassword(AnySetPassword(), TestContext.Current.CancellationToken));

	[Fact]
	async Task ChangeEmail_under_Success_acknowledges_true()
	{
		var outcome = await CreateFake(AccountScenario.Success).ChangeEmail(AnyChangeEmail(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<BoolResponse> success).ShouldBeTrue();
		success.Value.Value.ShouldBeTrue();
	}

	[Fact]
	async Task ChangeEmail_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).ChangeEmail(AnyChangeEmail(), TestContext.Current.CancellationToken)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.ExternalOnly)]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.RecoveryCodesLow)]
	[InlineData(AccountScenario.NoPasskeys)]
	[InlineData(AccountScenario.IncorrectPassword)]
	[InlineData(AccountScenario.PasswordPolicy)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task ChangeEmail_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(scenario).ChangeEmail(AnyChangeEmail(), TestContext.Current.CancellationToken));

	[Fact]
	async Task RemoveExternalLogin_under_Success_routes_to_external_logins_with_the_removed_key() =>
		NextUrlOf(await CreateFake(AccountScenario.Success).RemoveExternalLogin(AnyRemoveExternalLogin(), TestContext.Current.CancellationToken))
			.ShouldBe("/Account/Manage/ExternalLogins?status=external-login-removed");

	[Fact]
	async Task RemoveExternalLogin_under_ExternalOnly_answers_the_last_sign_in_method_conflict()
	{
		// Keyed by the Identity error code, exactly as IdentityOutcomeMap keys the store's refusal and
		// RemoveExternalLoginHandler passes it through unchanged.
		var problem = ProblemOf(await CreateFake(AccountScenario.ExternalOnly).RemoveExternalLogin(AnyRemoveExternalLogin(), TestContext.Current.CancellationToken));
		problem.Category.ShouldBe(ErrorCategory.Conflict);
		problem.Errors.Keys.ShouldBe(["LastSignInMethod"]);
		problem.Errors["LastSignInMethod"].ShouldBe(["This is the only way to sign in to your account."]);
	}

	[Fact]
	async Task RemoveExternalLogin_under_Fault_carries_the_fixed_catalog_correlation_id() =>
		ShouldBeTheCatalogFault(ProblemOf(await CreateFake(AccountScenario.Fault).RemoveExternalLogin(AnyRemoveExternalLogin(), TestContext.Current.CancellationToken)));

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.RecoveryCodesLow)]
	[InlineData(AccountScenario.NoPasskeys)]
	[InlineData(AccountScenario.IncorrectPassword)]
	[InlineData(AccountScenario.PasswordPolicy)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task RemoveExternalLogin_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(scenario).RemoveExternalLogin(AnyRemoveExternalLogin(), TestContext.Current.CancellationToken));

	[Fact]
	async Task DeletePersonalData_under_Success_routes_to_the_app_root() =>
		NextUrlOf(await CreateFake(AccountScenario.Success).DeletePersonalData(AnyDeletePersonalData(), TestContext.Current.CancellationToken))
			.ShouldBe("/");

	[Fact]
	async Task DeletePersonalData_under_IncorrectPassword_pins_the_handlers_model_message()
	{
		var problem = ProblemOf(await CreateFake(AccountScenario.IncorrectPassword).DeletePersonalData(AnyDeletePersonalData(), TestContext.Current.CancellationToken));
		problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
		problem.Errors.Keys.ShouldBe([string.Empty]);
		problem.Errors[string.Empty].ShouldBe(["Incorrect password."]);
	}

	[Fact]
	async Task DeletePersonalData_under_Fault_pins_the_shred_failure_line_and_the_catalog_correlation_id()
	{
		var problem = ProblemOf(await CreateFake(AccountScenario.Fault).DeletePersonalData(AnyDeletePersonalData(), TestContext.Current.CancellationToken));
		ShouldBeTheCatalogFault(problem);
		problem.Errors.Keys.ShouldBe([string.Empty]);
		problem.Errors[string.Empty].ShouldBe(["Your account could not be fully deleted. The failure has been logged for an administrator to finish."]);
	}

	// A passwordless account deletes itself without a password check.
	[Fact]
	async Task DeletePersonalData_under_ExternalOnly_routes_to_the_app_root() =>
		NextUrlOf(await CreateFake(AccountScenario.ExternalOnly).DeletePersonalData(AnyDeletePersonalData(), TestContext.Current.CancellationToken))
			.ShouldBe("/");

	[Theory]
	[InlineData(AccountScenario.Unspecified)]
	[InlineData(AccountScenario.TwoFactorDisabled)]
	[InlineData(AccountScenario.RecoveryCodesLow)]
	[InlineData(AccountScenario.NoPasskeys)]
	[InlineData(AccountScenario.PasswordPolicy)]
	[InlineData(AccountScenario.InvalidVerificationCode)]
	async Task DeletePersonalData_throws_loudly_on_scenarios_that_do_not_apply_to_it(AccountScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(scenario).DeletePersonalData(AnyDeletePersonalData(), TestContext.Current.CancellationToken));
}
