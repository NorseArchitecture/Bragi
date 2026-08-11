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
///     no mutable state of its own. Canonical outcomes mirror the real producers verbatim
///     (spec §1.3: <c>LoginHandler</c>, <c>RegisterHandler</c>, <c>ExceptionTranslationBehavior</c>);
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

	static readonly Failed _invalidCredentials =
		new(Problem.ModelError(ErrorCategory.InvalidCredentials, "Invalid email or password."));

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
				new Outcome<NavigationResult>(_invalidCredentials),
			AuthenticationScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/" }),
			AuthenticationScenario.InvalidCredentials =>
				new Outcome<NavigationResult>(_invalidCredentials),
			AuthenticationScenario.LockedOut =>
				Outcome<NavigationResult>.Err(ErrorCategory.LockedOut,
					new Dictionary<string, string[]> { [string.Empty] = ["This account is locked out. Try again later or reset your password."] }),
			AuthenticationScenario.NotAllowed =>
				Outcome<NavigationResult>.Err(ErrorCategory.NotAllowed,
					new Dictionary<string, string[]> { [string.Empty] = ["Sign-in is not allowed for this account."] }),
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
				Outcome<NavigationResult>.Err(ErrorCategory.Validation,
					new Dictionary<string, string[]>
					{
						[nameof(RegisterRequest.Password)] =
						[
							"Passwords must have at least one non alphanumeric character.",
							"Passwords must have at least one digit ('0'-'9').",
							"Passwords must have at least one uppercase ('A'-'Z').",
						],
					}),
			AuthenticationScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to Register."),
		});
}
