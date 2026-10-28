using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.Primitives;

namespace Norse.DesignSystem.Stories.Tests.Authentication;

/// <summary>
///     Parity with Himinbjörg's <c>SignInMethods/</c> handlers: the links in <c>GetSignInMethodsHandler</c>'s shape,
///     the landing <c>ReturnUrls.Resolve</c> would decide, and <c>PasskeySignInHandler</c>'s refusals.
/// </summary>
public sealed class FakeSignInMethodsServiceTests
{
	static FakeSignInMethodsService CreateFake(AuthenticationScenario scenario) =>
		new(new Scenario<AuthenticationScenario>(scenario));

	static CancellationToken Cancellation => TestContext.Current.CancellationToken;

	static PasskeySignInRequest AnyPasskeySignIn(string returnUrl = "") =>
		new() { CredentialJson = "{}", AssertionState = "story", ReturnUrl = returnUrl };

	[Theory]
	[InlineData("/Account/Manage", "%2FAccount%2FManage")]
	[InlineData("https://evil.test/", "%2F")]
	[InlineData("//evil.test/", "%2F")]
	[InlineData("", "%2F")]
	[InlineData("/Account/Manage?tab=x//y#z", "%2FAccount%2FManage%3Ftab%3Dx%2F%2Fy%23z")]
	// QueryString.Create encodes with UrlEncoder.Default, which leaves @ and the sub-delimiters ( ) * ! as they are.
	[InlineData("/Account/Manage?email=designer@example.com", "%2FAccount%2FManage%3Femail%3Ddesigner@example.com")]
	[InlineData("/Account/Manage?q=(a)*!'~ é", "%2FAccount%2FManage%3Fq%3D(a)*!%27~%20%C3%A9")]
	async Task Sign_in_methods_carry_only_the_resolved_landing(string returnUrl, string landing)
	{
		var outcome = await CreateFake(AuthenticationScenario.Success)
			.GetSignInMethods(new() { ReturnUrl = returnUrl }, Cancellation);

		outcome.TryGetValue(out Success<SignInMethodsResponse> success).ShouldBeTrue();
		success.Value.PasswordSignInUrl.ShouldBe($"/Account/Login?returnUrl={landing}");
		success.Value.PasskeySignInUrl.ShouldBe($"/Account/LoginWithPasskey?returnUrl={landing}");
		success.Value.Providers.ShouldBeEmpty();
	}

	// ReturnUrls.Resolve under an empty PathBase: every refusal it makes lands on the app root, and an external-login
	// route is never a landing (T3-34), raw or decoded, in any casing, with or without a trailing slash.
	[Theory]
	[InlineData("/Account/PerformExternalLogin?provider=Example")]
	[InlineData("/account/performexternallogin/")]
	[InlineData("/Account/Manage/LinkExternalLogin")]
	[InlineData("/Account/Manage/LinkExternalLoginCallback")]
	[InlineData("/Account/Manage%2FLinkExternalLogin")]
	[InlineData("Account/Manage")]
	[InlineData("/Account\\Manage")]
	[InlineData("/Account/../Manage")]
	[InlineData("/Account/%2e%2e/Manage")]
	[InlineData("/Account/%252e%252e/Manage")]
	[InlineData("/Account/%5cManage")]
	[InlineData("/Account/Manage\u0001")]
	[InlineData("/Account/Manage?x=%0A")]
	async Task Sign_in_methods_land_on_the_app_root_for_every_candidate_the_server_refuses(string returnUrl)
	{
		var outcome = await CreateFake(AuthenticationScenario.Success)
			.GetSignInMethods(new() { ReturnUrl = returnUrl }, Cancellation);

		outcome.TryGetValue(out Success<SignInMethodsResponse> success).ShouldBeTrue();
		success.Value.PasswordSignInUrl.ShouldBe("/Account/Login?returnUrl=%2F");
	}

	[Fact]
	async Task Sign_in_methods_land_on_the_app_root_for_a_candidate_over_the_length_bound()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success)
			.GetSignInMethods(new() { ReturnUrl = "/" + new string('a', 2048) }, Cancellation);

		outcome.TryGetValue(out Success<SignInMethodsResponse> success).ShouldBeTrue();
		success.Value.PasswordSignInUrl.ShouldBe("/Account/Login?returnUrl=%2F");
	}

	[Fact]
	async Task Sign_in_methods_under_ExternalProviders_carry_the_example_provider()
	{
		var outcome = await CreateFake(AuthenticationScenario.ExternalProviders)
			.GetSignInMethods(new() { ReturnUrl = "/Account/Manage" }, Cancellation);

		outcome.TryGetValue(out Success<SignInMethodsResponse> success).ShouldBeTrue();
		success.Value.Providers.ShouldBe(
		[
			new ExternalProviderLink
			{
				DisplayName = "Example provider",
				StartUrl = "/Account/PerformExternalLogin?provider=Example&returnUrl=%2FAccount%2FManage"
			}
		]);
	}

	// The links are a read every sign-in page makes whatever its own pinned state, so every scenario answers them --
	// Fault included (Task 5.2's choice): the handler's only failure is a pipeline fault, and a faulted link read
	// would put a second, unrelated error on Login's own pinned Fault story.
	[Theory]
	[InlineData(AuthenticationScenario.InvalidCredentials)]
	[InlineData(AuthenticationScenario.LockedOut)]
	[InlineData(AuthenticationScenario.NotAllowed)]
	[InlineData(AuthenticationScenario.TwoFactorRequired)]
	[InlineData(AuthenticationScenario.Fault)]
	async Task Sign_in_methods_answer_under_every_pinned_login_scenario(AuthenticationScenario scenario)
	{
		var outcome = await CreateFake(scenario).GetSignInMethods(new(), Cancellation);

		outcome.TryGetValue(out Success<SignInMethodsResponse> success).ShouldBeTrue();
		success.Value.Providers.ShouldBeEmpty();
	}

	// One story host serves every circuit, so no answer may share state with the next.
	[Fact]
	async Task A_caller_mutating_an_answer_never_changes_the_next_one()
	{
		var links = CreateFake(AuthenticationScenario.ExternalProviders);
		(await links.GetSignInMethods(new(), Cancellation)).TryGetValue(out Success<SignInMethodsResponse> first).ShouldBeTrue();
		first.Value.Providers[0].StartUrl = "mutated";
		var locked = CreateFake(AuthenticationScenario.LockedOut);
		(await locked.PasskeySignIn(AnyPasskeySignIn(), Cancellation)).TryGetValue(out Failed lockedOut).ShouldBeTrue();
		lockedOut.Problem.Errors[string.Empty][0] = "mutated";
		var invalid = CreateFake(AuthenticationScenario.InvalidCredentials);
		(await invalid.PasskeySignIn(AnyPasskeySignIn(), Cancellation)).TryGetValue(out Failed refused).ShouldBeTrue();
		refused.Problem.Errors[string.Empty][0] = "mutated";

		(await links.GetSignInMethods(new(), Cancellation)).TryGetValue(out Success<SignInMethodsResponse> again).ShouldBeTrue();
		again.Value.Providers[0].StartUrl.ShouldBe("/Account/PerformExternalLogin?provider=Example&returnUrl=%2F");
		(await locked.PasskeySignIn(AnyPasskeySignIn(), Cancellation)).TryGetValue(out Failed lockedAgain).ShouldBeTrue();
		lockedAgain.Problem.Errors[string.Empty].ShouldBe(["This account is locked out. Try again later or reset your password."]);
		(await invalid.PasskeySignIn(AnyPasskeySignIn(), Cancellation)).TryGetValue(out Failed refusedAgain).ShouldBeTrue();
		refusedAgain.Problem.Errors[string.Empty].ShouldBe(["The passkey could not be used to sign in."]);
	}

	[Fact]
	async Task Passkey_request_options_answer_the_story_ceremony()
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).GetPasskeyRequestOptions(Cancellation);

		outcome.TryGetValue(out Success<PasskeyRequestOptionsResponse> success).ShouldBeTrue();
		success.Value.OptionsJson.ShouldBe("{}");
		success.Value.AssertionState.ShouldBe("story");
	}

	[Fact]
	async Task Passkey_request_options_under_Fault_carry_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).GetPasskeyRequestOptions(Cancellation);

		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Theory]
	[InlineData("/Account/Manage", "/Account/Manage")]
	[InlineData("https://evil.test/", "/")]
	[InlineData("", "/")]
	[InlineData("/Account/PerformExternalLogin", "/")]
	async Task Passkey_sign_in_under_Success_lands_on_the_resolved_return_url(string returnUrl, string landing)
	{
		var outcome = await CreateFake(AuthenticationScenario.Success).PasskeySignIn(AnyPasskeySignIn(returnUrl), Cancellation);

		outcome.TryGetValue(out Success<NavigationResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe(landing);
	}

	[Fact]
	async Task Passkey_sign_in_under_InvalidCredentials_collapses_to_the_passkey_refusal()
	{
		var outcome = await CreateFake(AuthenticationScenario.InvalidCredentials).PasskeySignIn(AnyPasskeySignIn(), Cancellation);

		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
		failed.Problem.Errors[string.Empty].ShouldBe(["The passkey could not be used to sign in."]);
	}

	[Fact]
	async Task Passkey_sign_in_under_LockedOut_keeps_the_lockout_text()
	{
		var outcome = await CreateFake(AuthenticationScenario.LockedOut).PasskeySignIn(AnyPasskeySignIn(), Cancellation);

		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.LockedOut);
		failed.Problem.Errors[string.Empty].ShouldBe(["This account is locked out. Try again later or reset your password."]);
	}

	[Fact]
	async Task Passkey_sign_in_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(AuthenticationScenario.Fault).PasskeySignIn(AnyPasskeySignIn(), Cancellation);

		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task Passkey_sign_in_under_an_inapplicable_scenario_throws() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(AuthenticationScenario.RegistrationConflict).PasskeySignIn(AnyPasskeySignIn(), Cancellation));
}
