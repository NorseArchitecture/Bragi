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
		///     rides the fake, so driven Register stories validate against catalog truth. Singletons
		///     deliberately: WASM makes scoped effectively singleton anyway — say what you mean.
		/// </summary>
		/// <returns>The same service collection instance.</returns>
		public IServiceCollection AddNorseStoryFakes() =>
			services
				.AddSingleton(new Scenario<AuthenticationScenario>(AuthenticationScenario.Success))
				.AddSingleton<IAuthenticationService, FakeAuthenticationService>()
				.AddSingleton<RecordingSessionTransition>()
				.AddSingleton<ISessionTransition>(static provider => provider.GetRequiredService<RecordingSessionTransition>())
				.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>()
				.AddSingleton<IValidator<RegisterRequest>, RegisterRequestValidator>()
				.AddSingleton(new Scenario<ReferenceScenario>(ReferenceScenario.Success))
				.AddSingleton<IReferenceService, FakeReferenceService>()
				.AddSingleton<IValidator<CountryRequest>, CountryRequestValidator>();
	}
}
