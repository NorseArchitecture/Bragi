namespace Norse.DesignSystem.Stories.Identity;

/// <summary>
///     The states a story can pin <see cref="FakeIdentityService" /> into — the disclosure surface either
///     answers or faults; nothing else is reachable through the UI.
/// </summary>
enum IdentityScenario
{
	/// <summary>Sentinel CLR default — never a valid scenario; the fake throws on it.</summary>
	Unspecified = 0,

	/// <summary>Happy path — the holder's initial value, so an unwrapped story renders success.</summary>
	Success = 1,

	/// <summary>An unmapped failure with the fixed catalog correlation id.</summary>
	Fault = 2
}
