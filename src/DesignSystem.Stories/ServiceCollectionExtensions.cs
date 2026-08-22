using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Norse.AuthN.Components;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Reference;
using Norse.DesignSystem.Stories.Scenarios;
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
		///     Registers the catalog's fake <see cref="IAuthenticationService" /> and
		///     <see cref="IReferenceService" />, each with its own ambient <see cref="Scenario{TScenario}" />
		///     (initialized to the family's <c>Success</c> member) so their stories render and pin their
		///     states with no server context, plus the <see cref="RecordingSessionTransition" /> that stands
		///     in for <see cref="ISessionTransition" />. Also registers the real client-side validators
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
				.AddScoped(static _ => new Scenario<ReferenceScenario>(ReferenceScenario.Success))
				.AddScoped<IReferenceService, FakeReferenceService>()
				.AddScoped<IValidator<CountryRequest>, CountryRequestValidator>();
	}
}
