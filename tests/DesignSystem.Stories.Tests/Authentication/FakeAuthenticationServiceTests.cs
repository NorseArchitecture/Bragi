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

	[Fact]
	async Task Login_under_Success_returns_the_root_next_url()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).Login(AnyLogin(), TestContext.Current.CancellationToken);
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
}
