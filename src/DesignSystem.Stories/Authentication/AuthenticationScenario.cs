namespace Norse.DesignSystem.Stories.Authentication;

/// <summary>
///     The states a story can pin <see cref="FakeAuthenticationService" /> into — mirrors what the real
///     flow actually emits (spec §1.3), not the full <c>ErrorCategory</c> enum.
/// </summary>
enum AuthenticationScenario
{
	/// <summary>Sentinel CLR default — never a valid scenario; the fake throws on it.</summary>
	Unspecified = 0,

	/// <summary>Happy path — the holder's initial value, so an unwrapped story renders success.</summary>
	Success = 1,

	/// <summary>Login rejected with the generic anti-enumeration message.</summary>
	InvalidCredentials = 2,

	/// <summary>Login rejected because the account is locked out.</summary>
	LockedOut = 3,

	/// <summary>Login rejected as a precondition failure (sign-in not allowed for the account).</summary>
	NotAllowed = 4,

	/// <summary>Registration rejected because the email is already taken.</summary>
	RegistrationConflict = 5,

	/// <summary>Registration rejected by password policy.</summary>
	RegistrationValidation = 6,

	/// <summary>An unmapped failure with the fixed catalog correlation id.</summary>
	Fault = 7
}
