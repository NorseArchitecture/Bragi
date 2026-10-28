using FluentValidation;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Norse.Abstractions.Components.Primitives;
using Norse.AuthN.Components;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Account;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Identity;
using Norse.DesignSystem.Stories.Reference;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.DesignSystem.Stories.Session;
using Norse.DesignSystem.Stories.Shell;
using Norse.Reference;
using Norse.Reference.Components;

namespace Norse.DesignSystem.Stories;

/// <summary>
/// Registers the catalog's backing fakes. The story host calls this one method and stays
/// deliberately dumb about what stands behind the stories — the fakes themselves never leave
/// this assembly.
/// </summary>
public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		///     Registers the catalog's fake <see cref="IAuthenticationService" />, <see cref="IAccountService" />,
		///     <see cref="IIdentityService" />, <see cref="IReferenceService" /> and
		///     <see cref="AuthenticationStateProvider" />, each with its own ambient
		///     <see cref="Scenario{TScenario}" /> (initialized to the family's happy-path member) so their
		///     stories render and pin their states with no server context, plus the
		///     <see cref="RecordingSessionTransition" /> that stands in for <see cref="ISessionTransition" />.
		///     The account tier resolves too: the cascading authentication state, the
		///     <see cref="AuthNPolicies.Account" /> policy registered from <see cref="AuthNPolicies.ConfigureAccount" />
		///     (the catalog needs no server-side policy generator), and <see cref="StoryShellLayout" /> as the
		///     <see cref="IAppShellLayout" /> the Manage layout nests inside. Also registers the real client-side validators
		///     (Asgard's <c>FormValidator</c> resolves them from DI) — the async email-availability rule
		///     rides the fake, so driven Register stories validate against catalog truth. Scoped
		///     deliberately: the story host is a Blazor Server composition, and DI scope is the framework's
		///     own per-circuit boundary — each visitor's session gets its own fake, scenario, and
		///     session-transition recorder, with no state bleeding across circuits.
		/// </summary>
		/// <returns>The same service collection instance.</returns>
		public IServiceCollection AddNorseStoryFakes() =>
			services
				.AddScoped(static _ => new Scenario<AuthenticationScenario>(AuthenticationScenario.Success))
				.AddScoped<IAuthenticationService, FakeAuthenticationService>()
				.AddScoped<RecordingSessionTransition>()
				.AddScoped<ISessionTransition>(static provider => provider.GetRequiredService<RecordingSessionTransition>())
				.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>()
				.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>()
				.AddScoped<IValidator<TwoFactorSignInRequest>, TwoFactorSignInRequestValidator>()
				.AddScoped<IValidator<RecoveryCodeSignInRequest>, RecoveryCodeSignInRequestValidator>()
				.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>()
				.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>()
				.AddScoped<IValidator<ConfirmEmailRequest>, ConfirmEmailRequestValidator>()
				.AddScoped<IValidator<ConfirmEmailChangeRequest>, ConfirmEmailChangeRequestValidator>()
				.AddScoped<IValidator<ResendEmailConfirmationRequest>, ResendEmailConfirmationRequestValidator>()
				.AddScoped(static _ => new Scenario<AccountScenario>(AccountScenario.Success))
				.AddScoped<IAccountService, FakeAccountService>()
				.AddScoped<IValidator<SetPhoneNumberRequest>, SetPhoneNumberRequestValidator>()
				.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>()
				.AddScoped<IValidator<SetPasswordRequest>, SetPasswordRequestValidator>()
				.AddScoped<IValidator<ChangeEmailRequest>, ChangeEmailRequestValidator>()
				.AddScoped<IValidator<RemoveExternalLoginRequest>, RemoveExternalLoginRequestValidator>()
				.AddScoped<IValidator<DeletePersonalDataRequest>, DeletePersonalDataRequestValidator>()
				.AddScoped(static _ => new Scenario<IdentityScenario>(IdentityScenario.Success))
				.AddScoped<IIdentityService, FakeIdentityService>()
				.AddScoped(static _ => new Scenario<SessionScenario>(SessionScenario.SignedIn))
				.AddScoped<AuthenticationStateProvider, FakeAuthenticationStateProvider>()
				.AddCascadingAuthenticationState()
				.AddAuthorizationCore(static options => options.AddPolicy(AuthNPolicies.Account, AuthNPolicies.ConfigureAccount))
				.AddSingleton<IAppShellLayout, StoryShellLayout>()
				.AddScoped(static _ => new Scenario<ReferenceScenario>(ReferenceScenario.Success))
				.AddScoped<IReferenceService, FakeReferenceService>()
				.AddScoped<IValidator<CountryRequest>, CountryRequestValidator>();
	}
}
