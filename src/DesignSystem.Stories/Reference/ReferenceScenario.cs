namespace Norse.DesignSystem.Stories.Reference;

/// <summary>
///     The states a story can pin <see cref="FakeReferenceService" /> into — mirrors what the real read
///     path (<c>CountryQueryHandler</c>) actually emits reachable through the UI, not the full
///     <c>ErrorCategory</c> enum. The handler's defensive re-validation branch never appears here:
///     <c>CountryRequestValidator</c> blocks a malformed or empty code before submit, so the fake is
///     never asked to answer for one.
/// </summary>
enum ReferenceScenario
{
	/// <summary>Sentinel CLR default — never a valid scenario; the fake throws on it.</summary>
	Unspecified = 0,

	/// <summary>Happy path — the holder's initial value, so an unwrapped story renders success.</summary>
	Success = 1,

	/// <summary>An unmapped failure with the fixed catalog correlation id.</summary>
	Fault = 2
}
