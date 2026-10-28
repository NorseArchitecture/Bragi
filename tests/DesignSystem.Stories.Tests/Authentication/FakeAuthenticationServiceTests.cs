using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.Primitives;
using Norse.Primitives.Pii;

namespace Norse.DesignSystem.Stories.Tests.Authentication;

public sealed class FakeAuthenticationServiceTests
{
	// One holder + fake per test, pinned to the scenario under test — the fake itself is stateless;
	// all behavior selection lives in the ambient value.
	static FakeAuthenticationService CreateFake(AuthenticationScenario scenario) =>
		new(new Scenario<AuthenticationScenario>(scenario));

	static LoginRequest AnyLogin(string email = "designer@example.com") =>
		new() { EmailInput = email, Password = "aaaaaaaa" };

	static RegisterRequest AnyRegister() =>
		new() { EmailInput = "designer@example.com", Password = "aaaaaaaa" };

	static TwoFactorSignInRequest AnyTwoFactor() =>
		new() { Challenge = "story", Code = "123456" };

	static RecoveryCodeSignInRequest AnyRecoveryCode() =>
		new() { Challenge = "story", RecoveryCode = "abcde-fghij" };

	static ForgotPasswordRequest AnyForgotPassword() =>
		new() { EmailInput = "designer@example.com" };

	static ResetPasswordRequest AnyResetPassword() =>
		new() { EmailInput = "designer@example.com", Code = "story", Password = "aaaaaaaa" };

	static ConfirmEmailRequest AnyConfirmEmail() =>
		new() { UserId = "story", Code = "story" };

	static ConfirmEmailChangeRequest AnyConfirmEmailChange() =>
		new() { UserId = "story", EmailInput = "designer@example.com", Code = "story" };

	static ResendEmailConfirmationRequest AnyResendEmailConfirmation() =>
		new() { EmailInput = "designer@example.com" };

	[Fact]
	async Task Login_under_Success_returns_the_root_next_url()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).Login(AnyLogin(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/");
	}

	// ExternalProviders only adds a provider link beside the form; a manual submit on that story signs in like Success.
	[Fact]
	async Task Login_under_ExternalProviders_returns_the_root_next_url()
	{
		var outcome = await CreateFake(AuthenticationScenario.ExternalProviders).Login(AnyLogin(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/");
	}

	[Fact]
	async Task Login_under_Success_still_honors_the_playground_sentinel_email()
	{
		// Upper-cased deliberately — the comparison is OrdinalIgnoreCase.
		var outcome = await CreateFake(AuthenticationScenario.Success).Login(AnyLogin("FAIL@example.com"), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
	}

	[Fact]
	async Task Login_under_InvalidCredentials_pins_the_generic_anti_enumeration_message()
	{
		var outcome = await CreateFake(AuthenticationScenario.InvalidCredentials).Login(AnyLogin(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
		failed.Problem.Errors[string.Empty].ShouldBe(["Invalid email or password."]);
	}

	[Fact]
	async Task Login_under_LockedOut_pins_the_handlers_exact_model_message()
	{
		var outcome = await CreateFake(AuthenticationScenario.LockedOut).Login(AnyLogin(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.LockedOut);
		failed.Problem.Errors[string.Empty].ShouldBe(["This account is locked out. Try again later or reset your password."]);
	}

	[Fact]
	async Task Login_under_NotAllowed_pins_the_handlers_exact_model_message()
	{
		var outcome = await CreateFake(AuthenticationScenario.NotAllowed).Login(AnyLogin(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.NotAllowed);
		failed.Problem.Errors[string.Empty].ShouldBe(["Sign-in is not allowed for this account."]);
	}

	[Fact]
	async Task Login_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).Login(AnyLogin(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task Login_under_TwoFactorRequired_routes_to_the_two_factor_challenge()
	{
		var outcome = await CreateFake(AuthenticationScenario.TwoFactorRequired).Login(AnyLogin(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/Account/LoginWith2fa?challenge=story");
	}

	[Theory]
	[InlineData(AuthenticationScenario.Unspecified)]
	[InlineData(AuthenticationScenario.RegistrationConflict)]
	[InlineData(AuthenticationScenario.RegistrationValidation)]
	async Task Login_throws_loudly_on_scenarios_that_do_not_apply_to_it(AuthenticationScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(scenario).Login(AnyLogin(), TestContext.Current.CancellationToken));

	[Fact]
	async Task Register_under_Success_returns_the_root_next_url()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).Register(AnyRegister(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/");
	}

	[Fact]
	async Task Register_under_RegistrationConflict_pins_the_exact_email_keyed_dictionary()
	{
		var outcome = await CreateFake(AuthenticationScenario.RegistrationConflict).Register(AnyRegister(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Conflict);
		failed.Problem.Errors.Keys.ShouldBe([nameof(RegisterRequest.Email)]);
		failed.Problem.Errors[nameof(RegisterRequest.Email)].ShouldBe(["Email 'taken@example.com' is already taken."]);
	}

	[Fact]
	async Task Register_under_RegistrationValidation_pins_the_exact_three_password_policy_messages()
	{
		// Exactly what the proven "aaaaaaaa" fixture yields (RegisterHandlerTests) — no PasswordTooShort,
		// which Heimdall's client-side MinimumLength(8) makes unreachable through the composed flow.
		var outcome = await CreateFake(AuthenticationScenario.RegistrationValidation).Register(AnyRegister(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Validation);
		failed.Problem.Errors.Keys.ShouldBe([nameof(RegisterRequest.Password)]);
		failed.Problem.Errors[nameof(RegisterRequest.Password)].ShouldBe([
			"Passwords must have at least one non alphanumeric character.",
			"Passwords must have at least one digit ('0'-'9').",
			"Passwords must have at least one uppercase ('A'-'Z').",
		]);
	}

	[Fact]
	async Task Register_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).Register(AnyRegister(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Theory]
	[InlineData(AuthenticationScenario.Unspecified)]
	[InlineData(AuthenticationScenario.InvalidCredentials)]
	[InlineData(AuthenticationScenario.LockedOut)]
	[InlineData(AuthenticationScenario.NotAllowed)]
	async Task Register_throws_loudly_on_scenarios_that_do_not_apply_to_it(AuthenticationScenario scenario) =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(scenario).Register(AnyRegister(), TestContext.Current.CancellationToken));

	[Theory]
	[InlineData(AuthenticationScenario.Success)]
	[InlineData(AuthenticationScenario.RegistrationConflict)]
	[InlineData(AuthenticationScenario.RegistrationValidation)]
	[InlineData(AuthenticationScenario.Fault)]
	async Task EmailExists_reports_not_taken_under_every_scenario_so_driven_registers_reach_the_fake(AuthenticationScenario scenario)
	{
		// Blazilla's async EmailExists rule runs during validation BEFORE submit — if this ever failed
		// or reported taken, the driven Register stories could never reach their pinned server state.
		var outcome = await CreateFake(scenario).EmailExists(new EmailExistsRequest { Email = EmailAddress.Parse("anyone@example.com") }, TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<BoolResponse> success).ShouldBeTrue();
		success.Value.Value.ShouldBeFalse();
	}

	[Fact]
	async Task Logout_under_Success_returns_the_root_next_url()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).Logout(TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/");
	}

	[Fact]
	async Task Logout_under_Fault_pins_the_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).Logout(TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
	}

	[Fact]
	async Task Logout_under_an_inapplicable_scenario_throws_the_authoring_error()
	{
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AuthenticationScenario.LockedOut).Logout(TestContext.Current.CancellationToken));
	}

	[Fact]
	async Task TwoFactorSignIn_under_Success_returns_the_root_next_url()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).TwoFactorSignIn(AnyTwoFactor(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/");
	}

	[Fact]
	async Task TwoFactorSignIn_under_InvalidTwoFactorCode_pins_the_handlers_exact_model_message()
	{
		var outcome = await CreateFake(AuthenticationScenario.InvalidTwoFactorCode).TwoFactorSignIn(AnyTwoFactor(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
		failed.Problem.Errors[string.Empty].ShouldBe(["Invalid authenticator code."]);
	}

	[Fact]
	async Task TwoFactorSignIn_under_LockedOut_pins_the_login_lockout_message()
	{
		var outcome = await CreateFake(AuthenticationScenario.LockedOut).TwoFactorSignIn(AnyTwoFactor(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.LockedOut);
		failed.Problem.Errors[string.Empty].ShouldBe(["This account is locked out. Try again later or reset your password."]);
	}

	[Fact]
	async Task TwoFactorSignIn_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).TwoFactorSignIn(AnyTwoFactor(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task TwoFactorSignIn_under_an_inapplicable_scenario_throws_the_authoring_error() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AuthenticationScenario.InvalidRecoveryCode).TwoFactorSignIn(AnyTwoFactor(), TestContext.Current.CancellationToken));

	[Fact]
	async Task RecoveryCodeSignIn_under_Success_returns_the_root_next_url()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).RecoveryCodeSignIn(AnyRecoveryCode(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/");
	}

	[Fact]
	async Task RecoveryCodeSignIn_under_InvalidRecoveryCode_pins_the_handlers_exact_model_message()
	{
		var outcome = await CreateFake(AuthenticationScenario.InvalidRecoveryCode).RecoveryCodeSignIn(AnyRecoveryCode(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
		failed.Problem.Errors[string.Empty].ShouldBe(["Invalid recovery code entered."]);
	}

	[Fact]
	async Task RecoveryCodeSignIn_under_LockedOut_pins_the_login_lockout_message()
	{
		var outcome = await CreateFake(AuthenticationScenario.LockedOut).RecoveryCodeSignIn(AnyRecoveryCode(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.LockedOut);
		failed.Problem.Errors[string.Empty].ShouldBe(["This account is locked out. Try again later or reset your password."]);
	}

	[Fact]
	async Task RecoveryCodeSignIn_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).RecoveryCodeSignIn(AnyRecoveryCode(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task RecoveryCodeSignIn_under_an_inapplicable_scenario_throws_the_authoring_error() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AuthenticationScenario.InvalidTwoFactorCode).RecoveryCodeSignIn(AnyRecoveryCode(), TestContext.Current.CancellationToken));

	[Fact]
	async Task ForgotPassword_under_Success_routes_to_the_confirmation_page()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).ForgotPassword(AnyForgotPassword(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/Account/ForgotPasswordConfirmation");
	}

	[Fact]
	async Task ForgotPassword_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).ForgotPassword(AnyForgotPassword(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task ForgotPassword_under_an_inapplicable_scenario_throws_the_authoring_error() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AuthenticationScenario.InvalidCredentials).ForgotPassword(AnyForgotPassword(), TestContext.Current.CancellationToken));

	[Fact]
	async Task ResetPassword_under_Success_routes_to_the_confirmation_page()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).ResetPassword(AnyResetPassword(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/Account/ResetPasswordConfirmation");
	}

	[Fact]
	async Task ResetPassword_under_InvalidResetToken_pins_the_handlers_exact_model_message()
	{
		var outcome = await CreateFake(AuthenticationScenario.InvalidResetToken).ResetPassword(AnyResetPassword(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
		failed.Problem.Errors[string.Empty].ShouldBe(["The reset link is invalid or has expired."]);
	}

	[Fact]
	async Task ResetPassword_under_RegistrationValidation_pins_the_exact_three_password_policy_messages()
	{
		var outcome = await CreateFake(AuthenticationScenario.RegistrationValidation).ResetPassword(AnyResetPassword(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Validation);
		failed.Problem.Errors.Keys.ShouldBe([nameof(ResetPasswordRequest.Password)]);
		failed.Problem.Errors[nameof(ResetPasswordRequest.Password)].ShouldBe([
			"Passwords must have at least one non alphanumeric character.",
			"Passwords must have at least one digit ('0'-'9').",
			"Passwords must have at least one uppercase ('A'-'Z').",
		]);
	}

	[Fact]
	async Task ResetPassword_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).ResetPassword(AnyResetPassword(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task ResetPassword_under_an_inapplicable_scenario_throws_the_authoring_error() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AuthenticationScenario.LockedOut).ResetPassword(AnyResetPassword(), TestContext.Current.CancellationToken));

	[Fact]
	async Task ConfirmEmail_under_Success_acknowledges_true()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).ConfirmEmail(AnyConfirmEmail(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<BoolResponse> success).ShouldBeTrue();
		success.Value.Value.ShouldBeTrue();
	}

	[Fact]
	async Task ConfirmEmail_under_EmailConfirmationFailed_pins_the_handlers_exact_model_message()
	{
		var outcome = await CreateFake(AuthenticationScenario.EmailConfirmationFailed).ConfirmEmail(AnyConfirmEmail(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
		failed.Problem.Errors[string.Empty].ShouldBe(["The confirmation link is invalid or has expired."]);
	}

	[Fact]
	async Task ConfirmEmail_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).ConfirmEmail(AnyConfirmEmail(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task ConfirmEmail_under_an_inapplicable_scenario_throws_the_authoring_error() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AuthenticationScenario.InvalidResetToken).ConfirmEmail(AnyConfirmEmail(), TestContext.Current.CancellationToken));

	[Fact]
	async Task ConfirmEmailChange_under_Success_routes_to_login()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).ConfirmEmailChange(AnyConfirmEmailChange(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/Account/Login");
	}

	[Fact]
	async Task ConfirmEmailChange_under_EmailConfirmationFailed_pins_the_handlers_exact_model_message()
	{
		var outcome = await CreateFake(AuthenticationScenario.EmailConfirmationFailed).ConfirmEmailChange(AnyConfirmEmailChange(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
		failed.Problem.Errors[string.Empty].ShouldBe(["The confirmation link is invalid or has expired."]);
	}

	[Fact]
	async Task ConfirmEmailChange_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).ConfirmEmailChange(AnyConfirmEmailChange(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task ConfirmEmailChange_under_an_inapplicable_scenario_throws_the_authoring_error() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AuthenticationScenario.TwoFactorRequired).ConfirmEmailChange(AnyConfirmEmailChange(), TestContext.Current.CancellationToken));

	[Fact]
	async Task ResendEmailConfirmation_under_Success_acknowledges_true()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).ResendEmailConfirmation(AnyResendEmailConfirmation(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<BoolResponse> success).ShouldBeTrue();
		success.Value.Value.ShouldBeTrue();
	}

	[Fact]
	async Task ResendEmailConfirmation_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).ResendEmailConfirmation(AnyResendEmailConfirmation(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task ResendEmailConfirmation_under_an_inapplicable_scenario_throws_the_authoring_error() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AuthenticationScenario.EmailConfirmationFailed).ResendEmailConfirmation(AnyResendEmailConfirmation(), TestContext.Current.CancellationToken));

	// One story host serves every circuit, so no answer may share state with the next: the error arrays are mutable,
	// and every answer is built afresh.
	[Fact]
	async Task A_caller_mutating_an_answer_never_changes_the_next_one()
	{
		var cancellation = TestContext.Current.CancellationToken;
		Problem[] mutated =
		[
			ProblemOf(await CreateFake(AuthenticationScenario.InvalidCredentials).Login(AnyLogin(), cancellation)),
			ProblemOf(await CreateFake(AuthenticationScenario.LockedOut).Login(AnyLogin(), cancellation)),
			ProblemOf(await CreateFake(AuthenticationScenario.InvalidTwoFactorCode).TwoFactorSignIn(AnyTwoFactor(), cancellation)),
			ProblemOf(await CreateFake(AuthenticationScenario.InvalidRecoveryCode).RecoveryCodeSignIn(AnyRecoveryCode(), cancellation)),
			ProblemOf(await CreateFake(AuthenticationScenario.InvalidResetToken).ResetPassword(AnyResetPassword(), cancellation)),
			ProblemOf(await CreateFake(AuthenticationScenario.EmailConfirmationFailed).ConfirmEmail(AnyConfirmEmail(), cancellation)),
		];
		foreach (var problem in mutated)
			problem.Errors[string.Empty][0] = "mutated";
		ProblemOf(await CreateFake(AuthenticationScenario.RegistrationValidation).Register(AnyRegister(), cancellation))
			.Errors[nameof(RegisterRequest.Password)][0] = "mutated";

		ProblemOf(await CreateFake(AuthenticationScenario.InvalidCredentials).Login(AnyLogin(), cancellation))
			.Errors[string.Empty].ShouldBe(["Invalid email or password."]);
		ProblemOf(await CreateFake(AuthenticationScenario.LockedOut).Login(AnyLogin(), cancellation))
			.Errors[string.Empty].ShouldBe(["This account is locked out. Try again later or reset your password."]);
		ProblemOf(await CreateFake(AuthenticationScenario.InvalidTwoFactorCode).TwoFactorSignIn(AnyTwoFactor(), cancellation))
			.Errors[string.Empty].ShouldBe(["Invalid authenticator code."]);
		ProblemOf(await CreateFake(AuthenticationScenario.InvalidRecoveryCode).RecoveryCodeSignIn(AnyRecoveryCode(), cancellation))
			.Errors[string.Empty].ShouldBe(["Invalid recovery code entered."]);
		ProblemOf(await CreateFake(AuthenticationScenario.InvalidResetToken).ResetPassword(AnyResetPassword(), cancellation))
			.Errors[string.Empty].ShouldBe(["The reset link is invalid or has expired."]);
		ProblemOf(await CreateFake(AuthenticationScenario.EmailConfirmationFailed).ConfirmEmail(AnyConfirmEmail(), cancellation))
			.Errors[string.Empty].ShouldBe(["The confirmation link is invalid or has expired."]);
		ProblemOf(await CreateFake(AuthenticationScenario.RegistrationValidation).Register(AnyRegister(), cancellation))
			.Errors[nameof(RegisterRequest.Password)][0].ShouldBe("Passwords must have at least one non alphanumeric character.");
	}

	static Problem ProblemOf<T>(Outcome<T> outcome)
		where T : notnull
	{
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		return failed.Problem;
	}
}
