using Microsoft.Extensions.DependencyInjection;

namespace Norse.DesignSystem.Stories.Authentication;

/// <summary>
/// Registers the catalog's backing fakes. The story host calls this one method and stays
/// deliberately dumb about what stands behind the stories — the fakes themselves never leave
/// this assembly.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers a fake <see cref="Norse.AuthN.Services.IAuthenticationService"/> so the
	/// authentication stories render and are interactive with no server context.
	/// </summary>
	/// <param name="services">The service collection to register into.</param>
	/// <returns>The same service collection instance.</returns>
	public static IServiceCollection AddNorseStoryFakes(this IServiceCollection services) =>
		services.AddScoped<Norse.AuthN.Services.IAuthenticationService, FakeAuthenticationService>();
}
