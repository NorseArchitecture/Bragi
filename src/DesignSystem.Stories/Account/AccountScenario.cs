namespace Norse.DesignSystem.Stories.Account;

/// <summary>
///     The states a story can pin <see cref="FakeAccountService" /> into — mirrors what the real account
///     handlers actually emit (spec §7 "Stories (Bragi)"), not the full <c>ErrorCategory</c> enum. The family
///     spans the whole account tier: <see cref="FakeAccountService" />, <c>FakeTwoFactorService</c> and
///     <c>FakePasskeyService</c> all read it, and a member that applies to no method of a fake throws "does not
///     apply" there like every foreign scenario.
/// </summary>
enum AccountScenario
{
	/// <summary>Sentinel CLR default — never a valid scenario; the fake throws on it.</summary>
	Unspecified = 0,

	/// <summary>Happy path — the holder's initial value, so an unwrapped story renders success.</summary>
	Success = 1,

	/// <summary>A passwordless account signed in through its one external login; that login cannot be removed.</summary>
	ExternalOnly = 2,

	/// <summary>An account with no second factor: two-factor off, no authenticator, no recovery codes.</summary>
	TwoFactorDisabled = 3,

	/// <summary>An account with a single unredeemed recovery code left.</summary>
	RecoveryCodesLow = 4,

	/// <summary>An account with no registered passkeys.</summary>
	NoPasskeys = 5,

	/// <summary>A write rejected because the current password is wrong.</summary>
	IncorrectPassword = 6,

	/// <summary>A new password rejected by password policy.</summary>
	PasswordPolicy = 7,

	/// <summary>An authenticator verification code rejected — Train 3's two-factor setup (<c>EnableAuthenticator</c>).</summary>
	InvalidVerificationCode = 8,

	/// <summary>An unmapped failure with the fixed catalog correlation id.</summary>
	Fault = 9,

	/// <summary>The calling browser is remembered for two-factor sign-in, so the page offers to forget it.</summary>
	MachineRemembered = 10
}
