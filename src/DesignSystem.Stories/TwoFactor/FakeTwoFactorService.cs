using System.Collections.Immutable;
using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Account;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;

namespace Norse.DesignSystem.Stories.TwoFactor;

/// <summary>
///     Catalog-only stand-in for <see cref="ITwoFactorService" /> — never calls Himinbjörg, never touches gRPC. A
///     stateless switch over the account tier's ambient <see cref="AccountScenario" />, the
///     <see cref="FakeAccountService" /> shape. Every answer is built afresh from immutable seeds, since the wire records are
///     mutable and one story host serves every circuit. The setup read answers every
///     non-<see cref="AccountScenario.Fault" /> scenario (a write story must still load its form); the writes mirror
///     the real handlers verbatim (<c>Identity.Web.Server/TwoFactor/*Handler.cs</c>): each <c>NextUrl</c> is the one
///     the handler authors under an empty <c>PathBase</c> with nothing deferred, and each refusal keeps the handler's
///     key and text. Enable follows the scenario's account: the <see cref="AccountScenario.Success" /> account still
///     holds recovery codes and lands on the two-factor page, while a <see cref="AccountScenario.TwoFactorDisabled" />
///     account holds none and is handed ten through <c>RecoveryCodesHandoff</c>'s reveal page. Parity tests pin every
///     shape. Scenarios that do not apply to a method throw — a story arming the wrong scenario is an authoring error.
/// </summary>
sealed class FakeTwoFactorService(Scenario<AccountScenario> scenario) : ITwoFactorService
{
	// AuthenticatorSetup over the stored key ABCDEFGHIJKLMNOP, the development issuer "Norse" and the catalog's
	// signed-in designer; UrlEncoder.Default leaves the label's @ as it is. The SVG is a fixed placeholder (no QR
	// encoder in the catalog), shown as an image like the real one.
	const string SharedKey = "abcd efgh ijkl mnop";

	const string AuthenticatorUri = "otpauth://totp/Norse:designer@example.com?secret=ABCDEFGHIJKLMNOP&issuer=Norse&digits=6";

	const string QrCodeSvg =
		"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 84 84" width="84" height="84"><rect width="84" height="84" fill="#ffffff"/><path d="M0 0h28v28H0zM56 0h28v28H56zM0 56h28v28H0zM36 36h12v12H36z" fill="#000000"/></svg>""";

	// The stock batch of ten, in Identity's recovery-code shape. The wire records are mutable and the one story host
	// serves every circuit, so each answer is built afresh from these immutable seeds and shares nothing.
	static readonly ImmutableArray<string> _codes =
	[
		"2KQ8M-7XRTN", "4WJ3P-9CHVL", "6DNB5-3GFQZ", "8TYV2-5MKWR", "AXC7H-2PLJD",
		"CRF4N-8VZBT", "EHQ9W-6DMXK", "GLT3B-4RNYC", "JPZ6K-7WHFV", "LVD2X-9QTMB"
	];

	/// <inheritdoc />
	public Task<Outcome<AuthenticatorSetupResponse>> GetAuthenticatorSetup(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Unspecified =>
				throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GetAuthenticatorSetup."),
			AccountScenario.Fault => Fault<AuthenticatorSetupResponse>(),
			_ => Outcome<AuthenticatorSetupResponse>.Ok(new() { SharedKey = SharedKey, AuthenticatorUri = AuthenticatorUri, QrCodeSvg = QrCodeSvg }),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> EnableAuthenticator(EnableAuthenticatorRequest request,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success => Navigate("/Account/Manage/TwoFactorAuthentication?status=authenticator-verified"),
			AccountScenario.TwoFactorDisabled => Navigate("/Account/Manage/ShowRecoveryCodes?handoff=story-handoff"),
			// EnableAuthenticatorHandler's wrong-code refusal, keyed to the request's one field.
			AccountScenario.InvalidVerificationCode => Outcome<NavigationResult>.Err(ErrorCategory.Validation,
				new Dictionary<string, string[]> { [nameof(EnableAuthenticatorRequest.Code)] = ["Verification code is invalid."] }),
			AccountScenario.Fault => Fault<NavigationResult>(),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to EnableAuthenticator."),
		});

	/// <inheritdoc />
	public Task<Outcome<RecoveryCodesResponse>> RevealRecoveryCodes(RevealRecoveryCodesRequest request,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success => Codes(),
			AccountScenario.Fault => Fault<RecoveryCodesResponse>(),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to RevealRecoveryCodes."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> ResetAuthenticator(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			// The handler refuses nothing: disabling a factor that is already off is no refusal.
			AccountScenario.Success or AccountScenario.TwoFactorDisabled =>
				Navigate("/Account/Manage/EnableAuthenticator?status=authenticator-reset"),
			AccountScenario.Fault => Fault<NavigationResult>(),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to ResetAuthenticator."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> DisableTwoFactor(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success => Navigate("/Account/Manage/TwoFactorAuthentication?status=two-factor-disabled"),
			AccountScenario.TwoFactorDisabled => Refuse<NavigationResult>("Two-factor authentication is not enabled."),
			AccountScenario.Fault => Fault<NavigationResult>(),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to DisableTwoFactor."),
		});

	/// <inheritdoc />
	public Task<Outcome<RecoveryCodesResponse>> GenerateRecoveryCodes(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success => Codes(),
			AccountScenario.TwoFactorDisabled => Refuse<RecoveryCodesResponse>(
				"Cannot generate recovery codes because two-factor authentication is not enabled."),
			AccountScenario.Fault => Fault<RecoveryCodesResponse>(),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GenerateRecoveryCodes."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> ForgetTwoFactorClient(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			// The forget button renders only for a remembered machine; forgetting an unremembered one is no refusal.
			AccountScenario.Success or AccountScenario.MachineRemembered =>
				Navigate("/Account/Manage/TwoFactorAuthentication?status=browser-forgotten"),
			AccountScenario.Fault => Fault<NavigationResult>(),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to ForgetTwoFactorClient."),
		});

	static Outcome<RecoveryCodesResponse> Codes() =>
		Outcome<RecoveryCodesResponse>.Ok(new() { RecoveryCodes = [.. _codes] });

	static Outcome<T> Refuse<T>(string message)
		where T : notnull =>
		new(new Failed(Problem.ModelError(ErrorCategory.Validation, message)));

	static Outcome<NavigationResult> Navigate(string nextUrl) =>
		Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = nextUrl });

	static Outcome<T> Fault<T>()
		where T : notnull =>
		Outcome<T>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId);
}
