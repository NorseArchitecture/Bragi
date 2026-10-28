using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Norse.Abstractions.Components.Authorization;
using Norse.Abstractions.Components.Primitives;
using Norse.AuthN.Components;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Account;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Identity;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.DesignSystem.Stories.Session;
using Norse.DesignSystem.Stories.Shell;
using Norse.Reference;
using Norse.Reference.Components;

namespace Norse.DesignSystem.Stories.Tests;

public sealed class ServiceCollectionExtensionsTests
{
	// Mirrors the story host's composition: the host's own service registration provides logging,
	// and this extension provides everything the catalog's forms need — fake, scenario, and the real
	// client-side validators Blazilla resolves from DI (a form that can't validate is a catalog that lies).
	static ServiceProvider Build()
	{
		ServiceCollection services = new();
		services.AddLogging();
		services.AddNorseStoryFakes();
		return services.BuildServiceProvider();
	}

	[Fact]
	void Registers_the_login_validator_blazilla_resolves()
	{
		using var provider = Build();
		provider.GetRequiredService<IValidator<LoginRequest>>().ShouldBeOfType<LoginRequestValidator>();
	}

	[Fact]
	void Registers_the_register_validator_with_the_fake_behind_its_email_check()
	{
		using var provider = Build();
		provider.GetRequiredService<IValidator<RegisterRequest>>().ShouldBeOfType<RegisterRequestValidator>();
	}

	[Fact]
	void Registers_every_train_one_validator()
	{
		using var provider = Build();
		provider.GetRequiredService<IValidator<TwoFactorSignInRequest>>().ShouldBeOfType<TwoFactorSignInRequestValidator>();
		provider.GetRequiredService<IValidator<RecoveryCodeSignInRequest>>().ShouldBeOfType<RecoveryCodeSignInRequestValidator>();
		provider.GetRequiredService<IValidator<ForgotPasswordRequest>>().ShouldBeOfType<ForgotPasswordRequestValidator>();
		provider.GetRequiredService<IValidator<ResetPasswordRequest>>().ShouldBeOfType<ResetPasswordRequestValidator>();
		provider.GetRequiredService<IValidator<ConfirmEmailRequest>>().ShouldBeOfType<ConfirmEmailRequestValidator>();
		provider.GetRequiredService<IValidator<ConfirmEmailChangeRequest>>().ShouldBeOfType<ConfirmEmailChangeRequestValidator>();
		provider.GetRequiredService<IValidator<ResendEmailConfirmationRequest>>().ShouldBeOfType<ResendEmailConfirmationRequestValidator>();
	}

	[Fact]
	void Registers_the_fake_as_the_same_instance_within_one_scope()
	{
		using var provider = Build();
		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<IAuthenticationService>()
			.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
	}

	[Fact]
	void A_new_scope_gets_its_own_authentication_fake_instance()
	{
		using var provider = Build();
		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		scopeA.ServiceProvider.GetRequiredService<IAuthenticationService>()
			.ShouldNotBeSameAs(scopeB.ServiceProvider.GetRequiredService<IAuthenticationService>());
	}

	[Fact]
	void A_new_scope_gets_its_own_authentication_scenario_instance()
	{
		using var provider = Build();
		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		scopeA.ServiceProvider.GetRequiredService<Scenario<AuthenticationScenario>>()
			.ShouldNotBeSameAs(scopeB.ServiceProvider.GetRequiredService<Scenario<AuthenticationScenario>>());
	}

	[Fact]
	void Registers_the_country_request_validator_blazilla_resolves()
	{
		using var provider = Build();
		provider.GetRequiredService<IValidator<CountryRequest>>().ShouldBeOfType<CountryRequestValidator>();
	}

	[Fact]
	void Registers_the_reference_fake_as_the_same_instance_within_one_scope()
	{
		using var provider = Build();
		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<IReferenceService>()
			.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<IReferenceService>());
	}

	[Fact]
	void A_new_scope_gets_its_own_reference_fake_instance()
	{
		using var provider = Build();
		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		scopeA.ServiceProvider.GetRequiredService<IReferenceService>()
			.ShouldNotBeSameAs(scopeB.ServiceProvider.GetRequiredService<IReferenceService>());
	}

	[Fact]
	void Registers_the_recorder_as_the_catalogs_session_transition()
	{
		using var provider = Build();
		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<ISessionTransition>()
			.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<RecordingSessionTransition>());
	}

	[Fact]
	void A_new_scope_gets_its_own_session_transition_recorder()
	{
		using var provider = Build();
		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		scopeA.ServiceProvider.GetRequiredService<RecordingSessionTransition>()
			.ShouldNotBeSameAs(scopeB.ServiceProvider.GetRequiredService<RecordingSessionTransition>());
	}

	[Fact]
	void Registers_every_train_two_validator()
	{
		using var provider = Build();
		provider.GetRequiredService<IValidator<SetPhoneNumberRequest>>().ShouldBeOfType<SetPhoneNumberRequestValidator>();
		provider.GetRequiredService<IValidator<ChangePasswordRequest>>().ShouldBeOfType<ChangePasswordRequestValidator>();
		provider.GetRequiredService<IValidator<SetPasswordRequest>>().ShouldBeOfType<SetPasswordRequestValidator>();
		provider.GetRequiredService<IValidator<ChangeEmailRequest>>().ShouldBeOfType<ChangeEmailRequestValidator>();
		provider.GetRequiredService<IValidator<RemoveExternalLoginRequest>>().ShouldBeOfType<RemoveExternalLoginRequestValidator>();
		provider.GetRequiredService<IValidator<DeletePersonalDataRequest>>().ShouldBeOfType<DeletePersonalDataRequestValidator>();
	}

	[Fact]
	void Registers_the_account_identity_and_session_fakes_per_scope()
	{
		using var provider = Build();
		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		var a = scopeA.ServiceProvider;
		var b = scopeB.ServiceProvider;

		a.GetRequiredService<IAccountService>().ShouldBeOfType<FakeAccountService>().ShouldBeSameAs(a.GetRequiredService<IAccountService>());
		a.GetRequiredService<IAccountService>().ShouldNotBeSameAs(b.GetRequiredService<IAccountService>());
		a.GetRequiredService<Scenario<AccountScenario>>().Value.ShouldBe(AccountScenario.Success);
		a.GetRequiredService<Scenario<AccountScenario>>().ShouldNotBeSameAs(b.GetRequiredService<Scenario<AccountScenario>>());

		a.GetRequiredService<IIdentityService>().ShouldBeOfType<FakeIdentityService>().ShouldBeSameAs(a.GetRequiredService<IIdentityService>());
		a.GetRequiredService<IIdentityService>().ShouldNotBeSameAs(b.GetRequiredService<IIdentityService>());
		a.GetRequiredService<Scenario<IdentityScenario>>().Value.ShouldBe(IdentityScenario.Success);
		a.GetRequiredService<Scenario<IdentityScenario>>().ShouldNotBeSameAs(b.GetRequiredService<Scenario<IdentityScenario>>());

		a.GetRequiredService<AuthenticationStateProvider>().ShouldBeOfType<FakeAuthenticationStateProvider>()
			.ShouldBeSameAs(a.GetRequiredService<AuthenticationStateProvider>());
		a.GetRequiredService<AuthenticationStateProvider>().ShouldNotBeSameAs(b.GetRequiredService<AuthenticationStateProvider>());
		a.GetRequiredService<Scenario<SessionScenario>>().Value.ShouldBe(SessionScenario.SignedIn);
		a.GetRequiredService<Scenario<SessionScenario>>().ShouldNotBeSameAs(b.GetRequiredService<Scenario<SessionScenario>>());
	}

	// AddCascadingAuthenticationState registers the Task<AuthenticationState> cascade every AuthorizeView
	// reads as the framework's internal ICascadingValueSupplier — unnameable from here, so it's matched by name.
	[Fact]
	void Registers_the_cascading_authentication_state()
	{
		ServiceCollection services = new();
		services.AddNorseStoryFakes();
		services.ShouldContain(static descriptor =>
			descriptor.ServiceType.FullName == "Microsoft.AspNetCore.Components.ICascadingValueSupplier");
	}

	[Fact]
	async Task Resolves_the_account_policy()
	{
		using var provider = Build();
		using var scope = provider.CreateScope();
		var policy = await scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(AuthNPolicies.Account);
		policy.ShouldNotBeNull();

		var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
		ClaimsPrincipal anonymous = new(new ClaimsIdentity([new(ClaimTypes.Role, NorseRoles.Anonymous)], "Story"));
		ClaimsPrincipal user = new(new ClaimsIdentity([new(ClaimTypes.Role, "user")], "Story"));
		(await authorization.AuthorizeAsync(anonymous, AuthNPolicies.Account)).Succeeded.ShouldBeFalse();
		(await authorization.AuthorizeAsync(user, AuthNPolicies.Account)).Succeeded.ShouldBeTrue();

		// The registered provider's own principals, under each session scenario.
		var session = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
		var signedIn = (await session.GetAuthenticationStateAsync()).User;
		(await authorization.AuthorizeAsync(signedIn, AuthNPolicies.Account)).Succeeded.ShouldBeTrue();
		using (scope.ServiceProvider.GetRequiredService<Scenario<SessionScenario>>().Pin(SessionScenario.Anonymous))
		{
			var anonymousSession = (await session.GetAuthenticationStateAsync()).User;
			(await authorization.AuthorizeAsync(anonymousSession, AuthNPolicies.Account)).Succeeded.ShouldBeFalse();
		}
	}

	[Fact]
	void Registers_the_story_shell_layout()
	{
		using var provider = Build();
		var shell = provider.GetRequiredService<IAppShellLayout>().ShouldBeOfType<StoryShellLayout>();
		shell.LayoutType.ShouldBe(typeof(StoryShell));
		typeof(LayoutComponentBase).IsAssignableFrom(shell.LayoutType).ShouldBeTrue();
	}
}
