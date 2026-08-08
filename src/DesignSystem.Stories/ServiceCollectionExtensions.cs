using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Norse.AuthN.Components;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;

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
		///     Registers the catalog's fake <see cref="IAuthenticationService" /> and its ambient
		///     <see cref="Scenario{TScenario}" /> (initialized to
		///     <see cref="AuthenticationScenario.Success" />) so the authentication stories render and
		///     pin their states with no server context. Also registers the real client-side validators
		///     (Blazilla resolves them from DI) — the async email-availability rule rides the fake, so
		///     driven Register stories validate against catalog truth. Singletons deliberately: WASM
		///     makes scoped effectively singleton anyway — say what you mean.
		/// </summary>
		/// <returns>The same service collection instance.</returns>
		public IServiceCollection AddNorseStoryFakes() =>
			services
				.AddSingleton(new Scenario<AuthenticationScenario>(AuthenticationScenario.Success))
				.AddSingleton<IAuthenticationService, FakeAuthenticationService>()
				.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>()
				.AddSingleton<IValidator<RegisterRequest>, RegisterRequestValidator>();
	}
}
