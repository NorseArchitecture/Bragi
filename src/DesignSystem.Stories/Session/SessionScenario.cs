namespace Norse.DesignSystem.Stories.Session;

/// <summary>
///     The principals a story can pin <see cref="FakeAuthenticationStateProvider" /> into — a real
///     signed-in account or the anonymous-cookie principal, which is authenticated too.
/// </summary>
enum SessionScenario
{
	/// <summary>Sentinel CLR default — never a valid scenario; the provider throws on it.</summary>
	Unspecified = 0,

	/// <summary>A signed-in account — the holder's initial value, so an unwrapped story passes the account policy.</summary>
	SignedIn = 1,

	/// <summary>The anonymous-cookie principal: authenticated, carrying only the anonymous role.</summary>
	Anonymous = 2
}
