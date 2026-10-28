using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Norse.Abstractions.Components.Authorization;
using Norse.DesignSystem.Stories.Scenarios;

namespace Norse.DesignSystem.Stories.Session;

/// <summary>
///     The catalog's <see cref="AuthenticationStateProvider" />: a stateless switch over the ambient
///     <see cref="SessionScenario" />, so a story's <c>AuthorizeView</c> and the account policy see either a
///     signed-in account or the anonymous-cookie principal with no server context. The anonymous principal
///     is authenticated, exactly like the one Midgard's <c>NorseAnonymousHandler</c> mints — only its
///     <see cref="NorseRoles.Anonymous" /> role keeps it out of the account tier. A fresh principal per call:
///     <see cref="ClaimsPrincipal" /> is mutable, and no circuit may alter another's.
/// </summary>
sealed class FakeAuthenticationStateProvider(Scenario<SessionScenario> scenario) : AuthenticationStateProvider
{
	const string AuthenticationType = "Story";

	/// <inheritdoc />
	public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
		Task.FromResult(new AuthenticationState(scenario.Value switch
		{
			SessionScenario.SignedIn => new ClaimsPrincipal(new ClaimsIdentity(
			[
				new(ClaimTypes.NameIdentifier, "0badc0de-0bad-c0de-0bad-c0de0badc0de"),
				new(ClaimTypes.Name, "designer@example.com"),
				new(ClaimTypes.Role, "user")
			], AuthenticationType)),
			SessionScenario.Anonymous =>
				new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Role, NorseRoles.Anonymous)], AuthenticationType)),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GetAuthenticationStateAsync."),
		}));
}
