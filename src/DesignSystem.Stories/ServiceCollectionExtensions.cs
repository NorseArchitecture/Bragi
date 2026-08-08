using Microsoft.Extensions.DependencyInjection;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Authentication;

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
		/// Registers a fake <see cref="IAuthenticationService"/> so the
		/// authentication stories render and are interactive with no server context.
		/// </summary>
		/// <returns>The same service collection instance.</returns>
		public IServiceCollection AddNorseStoryFakes() =>
			services.AddScoped<IAuthenticationService, FakeAuthenticationService>();
	}
}
