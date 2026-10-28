using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.Primitives;
using Norse.Primitives.Pii;

namespace Norse.DesignSystem.Stories.Authentication;

/// <summary>
///     Catalog-only stand-in for <see cref="IAuthenticationService" /> — never calls Himinbjörg, never
///     touches gRPC. A stateless switch over the ambient <see cref="AuthenticationScenario" />:
///     behavior is selected by the story (via <c>ScenarioScope</c>), never accumulated — the fake holds
///     no mutable state of its own, and every answer is built afresh, since the error dictionaries are mutable
///     and one story host serves every circuit. Canonical outcomes mirror the real producers verbatim
///     (spec §1.3: <c>LoginHandler</c>, <c>RegisterHandler</c>, <c>ExceptionTranslationBehavior</c>; the
///     account-lifecycle handlers; scenario-driven fakes defined in §7 "Stories (Bragi)" of
///     <c>../Glitnir/docs/Himinbjorg/specs/2026-10-26-identity-account-lifecycle-over-grpc-design.md</c>);
///     parity tests pin every shape. Scenarios that do not apply to a method throw — a story arming
///     the wrong scenario is an authoring error, and silence would mask it.
/// </summary>
sealed class FakeAuthenticationService(Scenario<AuthenticationScenario> scenario) : IAuthenticationService
{
	/// <summary>
	///     Typed into the Default (playground) story's own Email field to preview the
	///     invalid-credentials state interactively — a garnish beside the pinned stories, never the
	///     pinning mechanism.
	/// </summary>
	internal const string InvalidCredentialsSentinelEmail = "fail@example.com";

	/// <summary>
	///     The fixed catalog correlation id for <see cref="AuthenticationScenario.Fault" />. The real id
	///     is minted per fault by Midgard's <c>ExceptionTranslationBehavior</c> via
	///     <see cref="Guid.NewGuid" />, which would break the identical-render bar pinned stories exist
	///     for; this value is obviously synthetic and never mistakable for a real incident reference.
	/// </summary>
	internal static readonly Guid CatalogCorrelationId = new("0badc0de-0bad-c0de-0bad-c0de0badc0de");

	// Every sign-in refusal of the stock InvalidCredentials shape, keyed to the model, as the handlers answer it.
	static Outcome<T> Refuse<T>(string message)
		where T : notnull =>
		new(new Failed(Problem.ModelError(ErrorCategory.InvalidCredentials, message)));

	// Shared by every sign-in arm — Himinbjörg's SignInRefusal collapses each lockout to this one text.
	static Dictionary<string, string[]> LockedOut() =>
		new() { [string.Empty] = ["This account is locked out. Try again later or reset your password."] };

	// Exactly what the proven "aaaaaaaa" fixture yields under the default password policy (RegisterHandlerTests);
	// Register and ResetPassword both key it to their own Password field, which share the one name.
	static Dictionary<string, string[]> PasswordPolicy() =>
		new()
		{
			[nameof(RegisterRequest.Password)] =
			[
				"Passwords must have at least one non alphanumeric character.",
				"Passwords must have at least one digit ('0'-'9').",
				"Passwords must have at least one uppercase ('A'-'Z').",
			],
		};

	// Unconditionally "not taken" under every scenario: Blazilla's async EmailExists rule runs during
	// validation before submit, so any other answer would stop driven Register stories from ever
	// reaching their pinned server state.
	public Task<Outcome<BoolResponse>> EmailExists(EmailExistsRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(Outcome<BoolResponse>.Ok(new BoolResponse { Value = false }));

	public Task<Outcome<NavigationResult>> Login(LoginRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success when request.Email is Success<EmailAddress>(var email) &&
				email.WireValue.Equals(InvalidCredentialsSentinelEmail, StringComparison.OrdinalIgnoreCase) =>
				Refuse<NavigationResult>("Invalid email or password."),
			// ExternalProviders only adds a provider link beside the form; a manual submit there signs in like Success.
			AuthenticationScenario.Success or AuthenticationScenario.ExternalProviders =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/" }),
			AuthenticationScenario.InvalidCredentials =>
				Refuse<NavigationResult>("Invalid email or password."),
			AuthenticationScenario.LockedOut =>
				Outcome<NavigationResult>.Err(ErrorCategory.LockedOut, LockedOut()),
			AuthenticationScenario.NotAllowed =>
				Outcome<NavigationResult>.Err(ErrorCategory.NotAllowed,
					new Dictionary<string, string[]> { [string.Empty] = ["Sign-in is not allowed for this account."] }),
			AuthenticationScenario.TwoFactorRequired =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/Account/LoginWith2fa?challenge=story" }),
			AuthenticationScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to Login."),
		});

	public Task<Outcome<NavigationResult>> Logout(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/" }),
			AuthenticationScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to Logout."),
		});

	public Task<Outcome<NavigationResult>> Register(RegisterRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/" }),
			AuthenticationScenario.RegistrationConflict =>
				Outcome<NavigationResult>.Err(ErrorCategory.Conflict,
					new Dictionary<string, string[]> { [nameof(RegisterRequest.Email)] = ["Email 'taken@example.com' is already taken."] }),
			AuthenticationScenario.RegistrationValidation =>
				Outcome<NavigationResult>.Err(ErrorCategory.Validation, PasswordPolicy()),
			AuthenticationScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to Register."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> TwoFactorSignIn(TwoFactorSignInRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/" }),
			AuthenticationScenario.InvalidTwoFactorCode =>
				Refuse<NavigationResult>("Invalid authenticator code."),
			AuthenticationScenario.LockedOut =>
				Outcome<NavigationResult>.Err(ErrorCategory.LockedOut, LockedOut()),
			AuthenticationScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to TwoFactorSignIn."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> RecoveryCodeSignIn(RecoveryCodeSignInRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/" }),
			AuthenticationScenario.InvalidRecoveryCode =>
				Refuse<NavigationResult>("Invalid recovery code entered."),
			AuthenticationScenario.LockedOut =>
				Outcome<NavigationResult>.Err(ErrorCategory.LockedOut, LockedOut()),
			AuthenticationScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to RecoveryCodeSignIn."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/Account/ForgotPasswordConfirmation" }),
			AuthenticationScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to ForgotPassword."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/Account/ResetPasswordConfirmation" }),
			AuthenticationScenario.InvalidResetToken =>
				Refuse<NavigationResult>("The reset link is invalid or has expired."),
			AuthenticationScenario.RegistrationValidation =>
				Outcome<NavigationResult>.Err(ErrorCategory.Validation, PasswordPolicy()),
			AuthenticationScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to ResetPassword."),
		});

	/// <inheritdoc />
	public Task<Outcome<BoolResponse>> ConfirmEmail(ConfirmEmailRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success =>
				Outcome<BoolResponse>.Ok(new BoolResponse { Value = true }),
			AuthenticationScenario.EmailConfirmationFailed =>
				Refuse<BoolResponse>("The confirmation link is invalid or has expired."),
			AuthenticationScenario.Fault =>
				Outcome<BoolResponse>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to ConfirmEmail."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> ConfirmEmailChange(ConfirmEmailChangeRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/Account/Login" }),
			AuthenticationScenario.EmailConfirmationFailed =>
				Refuse<NavigationResult>("The confirmation link is invalid or has expired."),
			AuthenticationScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to ConfirmEmailChange."),
		});

	/// <inheritdoc />
	public Task<Outcome<BoolResponse>> ResendEmailConfirmation(ResendEmailConfirmationRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success =>
				Outcome<BoolResponse>.Ok(new BoolResponse { Value = true }),
			AuthenticationScenario.Fault =>
				Outcome<BoolResponse>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to ResendEmailConfirmation."),
		});
}
