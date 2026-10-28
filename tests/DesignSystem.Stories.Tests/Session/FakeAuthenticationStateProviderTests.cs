using System.Security.Claims;
using Norse.Abstractions.Components.Authorization;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.DesignSystem.Stories.Session;

namespace Norse.DesignSystem.Stories.Tests.Session;

public sealed class FakeAuthenticationStateProviderTests
{
	static async Task<ClaimsPrincipal> PrincipalUnder(SessionScenario scenario) =>
		(await new FakeAuthenticationStateProvider(new Scenario<SessionScenario>(scenario)).GetAuthenticationStateAsync()).User;

	[Fact]
	async Task SignedIn_yields_the_user_role_principal()
	{
		var user = await PrincipalUnder(SessionScenario.SignedIn);
		user.Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeTrue();
		user.Identity.AuthenticationType.ShouldBe("Story");
		user.FindFirst(ClaimTypes.NameIdentifier).ShouldNotBeNull().Value.ShouldBe("0badc0de-0bad-c0de-0bad-c0de0badc0de");
		user.FindFirst(ClaimTypes.Name).ShouldNotBeNull().Value.ShouldBe("designer@example.com");
		user.IsInRole("user").ShouldBeTrue();
		user.IsInRole(NorseRoles.Anonymous).ShouldBeFalse();
	}

	// Authenticated, like the real anonymous principal Midgard's NorseAnonymousHandler mints.
	[Fact]
	async Task Anonymous_yields_the_anonymous_role_principal()
	{
		var user = await PrincipalUnder(SessionScenario.Anonymous);
		user.Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeTrue();
		user.Identity.AuthenticationType.ShouldBe("Story");
		user.Claims.ShouldHaveSingleItem().Type.ShouldBe(ClaimTypes.Role);
		user.IsInRole(NorseRoles.Anonymous).ShouldBeTrue();
	}

	[Fact]
	async Task The_sentinel_throws_loudly() =>
		await Should.ThrowAsync<InvalidOperationException>(() => PrincipalUnder(SessionScenario.Unspecified));
}
