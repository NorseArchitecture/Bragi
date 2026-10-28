using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;

namespace Norse.DesignSystem.Stories.Account;

/// <summary>
///     Catalog-only stand-in for <see cref="IAccountService" /> — never calls Himinbjörg, never touches
///     gRPC. A stateless switch over the ambient <see cref="AccountScenario" />, the
///     <see cref="FakeAuthenticationService" /> shape: behavior is selected by the story (via
///     <c>ScenarioScope</c>), never accumulated. The two reads serve shared static seed fixtures, which no
///     component mutates, and answer every non-<see cref="AccountScenario.Fault" /> scenario (a write
///     story must still load its form);
///     the writes mirror the real account handlers verbatim (<c>Identity.Web.Server/Account/*Handler.cs</c>,
///     spec §7 "Stories (Bragi)" of
///     <c>../Glitnir/docs/Himinbjorg/specs/2026-10-26-identity-account-lifecycle-over-grpc-design.md</c>),
///     and parity tests pin every shape. Scenarios that do not apply to a method throw — a story arming
///     the wrong scenario is an authoring error, and silence would mask it.
/// </summary>
sealed class FakeAccountService(Scenario<AccountScenario> scenario) : IAccountService
{
	static readonly AccountStatusResponse _status = new()
	{
		HasPassword = true,
		EmailConfirmed = true,
		TwoFactorEnabled = true,
		HasAuthenticator = true,
		RecoveryCodesLeft = 3,
		IsMachineRemembered = false,
		PasskeyCount = 2,
		ExternalLoginCount = 1
	};

	static readonly AccountStatusResponse _externalOnlyStatus = _status with { HasPassword = false };

	static readonly AccountStatusResponse _twoFactorDisabledStatus =
		_status with { TwoFactorEnabled = false, HasAuthenticator = false, RecoveryCodesLeft = 0 };

	static readonly AccountStatusResponse _recoveryCodesLowStatus = _status with { RecoveryCodesLeft = 1 };

	static readonly AccountStatusResponse _noPasskeysStatus = _status with { PasskeyCount = 0 };

	static readonly ExternalLoginsResponse _logins = new()
	{
		Logins = [new() { LoginProvider = "Google", ProviderKey = "story-google", DisplayName = "Google" }],
		AvailableProviders = [new() { Name = "Microsoft", DisplayName = "Microsoft Account" }],
		CanRemove = true
	};

	static readonly ExternalLoginsResponse _externalOnlyLogins = _logins with { CanRemove = false };

	// IdentityErrorFields.ForPasswordChange keys PasswordMismatch to OldPassword and every password rule to
	// NewPassword — for SetPassword too, whose request names its one field the same.
	static readonly Dictionary<string, string[]> _incorrectOldPassword =
		new() { [nameof(ChangePasswordRequest.OldPassword)] = ["Incorrect password."] };

	static readonly Dictionary<string, string[]> _passwordPolicy =
		new() { [nameof(ChangePasswordRequest.NewPassword)] = ["Passwords must have at least one digit ('0'-'9')."] };

	// The store's refusal, keyed by its Identity error code (NorseIdentityErrorCodes.LastSignInMethod) the way
	// IdentityOutcomeMap keys every IdentityResult failure; RemoveExternalLoginHandler passes it through unchanged.
	static readonly Dictionary<string, string[]> _lastSignInMethod =
		new() { ["LastSignInMethod"] = ["This is the only way to sign in to your account."] };

	static readonly Failed _incorrectPassword =
		new(Problem.ModelError(ErrorCategory.InvalidCredentials, "Incorrect password."));

	// DeletePersonalDataHandler's one shred-failure line, under the catalog correlation id.
	static readonly Failed _shredFailed = new(new Problem
	{
		Category = ErrorCategory.Fault,
		Errors = new Dictionary<string, string[]>
		{
			[string.Empty] = ["Your account could not be fully deleted. The failure has been logged for an administrator to finish."]
		},
		CorrelationId = FakeAuthenticationService.CatalogCorrelationId
	});

	/// <inheritdoc />
	public Task<Outcome<AccountStatusResponse>> GetAccountStatus(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Unspecified =>
				throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GetAccountStatus."),
			AccountScenario.ExternalOnly => Outcome<AccountStatusResponse>.Ok(_externalOnlyStatus),
			AccountScenario.TwoFactorDisabled => Outcome<AccountStatusResponse>.Ok(_twoFactorDisabledStatus),
			AccountScenario.RecoveryCodesLow => Outcome<AccountStatusResponse>.Ok(_recoveryCodesLowStatus),
			AccountScenario.NoPasskeys => Outcome<AccountStatusResponse>.Ok(_noPasskeysStatus),
			AccountScenario.Fault =>
				Outcome<AccountStatusResponse>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId),
			_ => Outcome<AccountStatusResponse>.Ok(_status),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> SetPhoneNumber(SetPhoneNumberRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/Account/Manage?status=phone-updated" }),
			AccountScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to SetPhoneNumber."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/Account/Manage/ChangePassword?status=password-changed" }),
			AccountScenario.IncorrectPassword =>
				Outcome<NavigationResult>.Err(ErrorCategory.InvalidCredentials, _incorrectOldPassword),
			AccountScenario.PasswordPolicy =>
				Outcome<NavigationResult>.Err(ErrorCategory.Validation, _passwordPolicy),
			AccountScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to ChangePassword."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> SetPassword(SetPasswordRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			// An external-only account is exactly the one that sets a password.
			AccountScenario.Success or AccountScenario.ExternalOnly =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/Account/Manage/ChangePassword?status=password-set" }),
			AccountScenario.PasswordPolicy =>
				Outcome<NavigationResult>.Err(ErrorCategory.Validation, _passwordPolicy),
			AccountScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to SetPassword."),
		});

	/// <inheritdoc />
	public Task<Outcome<BoolResponse>> ChangeEmail(ChangeEmailRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success =>
				Outcome<BoolResponse>.Ok(new BoolResponse { Value = true }),
			AccountScenario.Fault =>
				Outcome<BoolResponse>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to ChangeEmail."),
		});

	/// <inheritdoc />
	public Task<Outcome<ExternalLoginsResponse>> GetExternalLogins(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Unspecified =>
				throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GetExternalLogins."),
			AccountScenario.ExternalOnly => Outcome<ExternalLoginsResponse>.Ok(_externalOnlyLogins),
			AccountScenario.Fault =>
				Outcome<ExternalLoginsResponse>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId),
			_ => Outcome<ExternalLoginsResponse>.Ok(_logins),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> RemoveExternalLogin(RemoveExternalLoginRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/Account/Manage/ExternalLogins?status=external-login-removed" }),
			AccountScenario.ExternalOnly =>
				Outcome<NavigationResult>.Err(ErrorCategory.Conflict, _lastSignInMethod),
			AccountScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to RemoveExternalLogin."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> DeletePersonalData(DeletePersonalDataRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			// A passwordless account deletes itself without a password check.
			AccountScenario.Success or AccountScenario.ExternalOnly =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/" }),
			AccountScenario.IncorrectPassword =>
				new Outcome<NavigationResult>(_incorrectPassword),
			AccountScenario.Fault =>
				new Outcome<NavigationResult>(_shredFailed),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to DeletePersonalData."),
		});
}
