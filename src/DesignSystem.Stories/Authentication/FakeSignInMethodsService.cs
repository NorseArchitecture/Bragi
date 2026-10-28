using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Scenarios;

namespace Norse.DesignSystem.Stories.Authentication;

/// <summary>
///     Catalog-only stand-in for <see cref="ISignInMethodsService" /> — never calls Himinbjörg, never touches gRPC.
///     A stateless switch over the same ambient <see cref="AuthenticationScenario" /> as
///     <see cref="FakeAuthenticationService" />, since the sign-in pages that render <c>SignInMethodLinks</c> are
///     that family's stories. The links mirror <c>GetSignInMethodsHandler</c>'s shape for the landing
///     <c>ReturnUrls.Resolve</c> would decide (ported under the catalog's empty <c>PathBase</c>: anything the server
///     refuses, an external-login route included, lands on <c>/</c>), and answer every scenario,
///     <see cref="AuthenticationScenario.Fault" /> included — they are a read each sign-in page makes whatever its own
///     pinned state, the handler's only failure is a pipeline fault, and a faulted read would put a second error on
///     Login's pinned Fault story — with no providers except under <see cref="AuthenticationScenario.ExternalProviders" />. Passkey
///     sign-in mirrors <c>PasskeySignInHandler</c>'s refusals verbatim; parity tests pin every shape. Scenarios
///     that do not apply to a write throw — a story arming the wrong scenario is an authoring error.
/// </summary>
sealed class FakeSignInMethodsService(Scenario<AuthenticationScenario> scenario) : ISignInMethodsService
{
	// ReturnUrls.MaxLength.
	const int MaxReturnUrlLength = 2048;

	// ExternalLoginRoutes.All.
	static readonly ImmutableArray<string> _externalLoginRoutes =
		["/Account/PerformExternalLogin", "/Account/Manage/LinkExternalLogin", "/Account/Manage/LinkExternalLoginCallback"];

	/// <inheritdoc />
	public Task<Outcome<SignInMethodsResponse>> GetSignInMethods(GetSignInMethodsRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		// QueryString.Create's own encoder, so every link is byte-identical to the handler's.
		var returnUrl = UrlEncoder.Default.Encode(Resolve(request.ReturnUrl));
		return Task.FromResult(Outcome<SignInMethodsResponse>.Ok(new SignInMethodsResponse
		{
			PasswordSignInUrl = $"/Account/Login?returnUrl={returnUrl}",
			PasskeySignInUrl = $"/Account/LoginWithPasskey?returnUrl={returnUrl}",
			Providers = scenario.Value is AuthenticationScenario.ExternalProviders ?
				[new() { DisplayName = "Example provider", StartUrl = $"/Account/PerformExternalLogin?provider=Example&returnUrl={returnUrl}" }] :
				[]
		}));
	}

	/// <inheritdoc />
	public Task<Outcome<PasskeyRequestOptionsResponse>> GetPasskeyRequestOptions(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value is AuthenticationScenario.Fault ?
			Outcome<PasskeyRequestOptionsResponse>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId) :
			Outcome<PasskeyRequestOptionsResponse>.Ok(new PasskeyRequestOptionsResponse { OptionsJson = "{}", AssertionState = "story" }));

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> PasskeySignIn(PasskeySignInRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		return Task.FromResult(scenario.Value switch
		{
			AuthenticationScenario.Success =>
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = Resolve(request.ReturnUrl) }),
			// PasskeySignInHandler's anti-enumeration collapse; the handler's one static instance is built afresh here,
			// since the wire shape is mutable and the catalog shares nothing across circuits.
			AuthenticationScenario.InvalidCredentials =>
				new Outcome<NavigationResult>(new Failed(Problem.ModelError(ErrorCategory.InvalidCredentials,
					"The passkey could not be used to sign in."))),
			// Himinbjörg's SignInRefusal collapses each lockout to this one text, the FakeAuthenticationService arm's.
			AuthenticationScenario.LockedOut =>
				Outcome<NavigationResult>.Err(ErrorCategory.LockedOut,
					new Dictionary<string, string[]> { [string.Empty] = ["This account is locked out. Try again later or reset your password."] }),
			AuthenticationScenario.Fault =>
				Outcome<NavigationResult>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to PasskeySignIn."),
		});
	}

	// ReturnUrls.Resolve ported under the catalog's empty PathBase (the containment step is then vacuous): length and
	// control characters over the whole candidate, raw and decoded once; the path split at its first literal ? or #
	// before any decoding; separators, encodings and dot segments judged on the path raw and decoded; and no
	// external-login route as a landing (T3-34).
	static string Resolve(string? candidate) =>
		IsLocal(candidate) ?
			candidate :
			"/";

	static bool IsLocal([NotNullWhen(true)] string? candidate)
	{
		if (string.IsNullOrEmpty(candidate) || candidate.Length > MaxReturnUrlLength
			|| HasControl(candidate) || HasControl(Uri.UnescapeDataString(candidate)))
			return false;

		var boundary = candidate.AsSpan().IndexOfAny('?', '#');
		var path = boundary < 0 ?
			candidate :
			candidate[..boundary];
		if (!path.StartsWith('/'))
			return false;
		var decodedPath = Uri.UnescapeDataString(path);
		if (IsAmbiguous(path) || IsAmbiguous(decodedPath))
			return false;
		foreach (var segment in decodedPath.Split('/'))
			if (segment is "." or "..")
				return false;

		return !IsExternalLoginRoute(path) && !IsExternalLoginRoute(decodedPath);
	}

	static bool HasControl(string value)
	{
		foreach (var character in value)
			if (char.IsControl(character))
				return true;
		return false;
	}

	static bool IsAmbiguous(string path) =>
		path.Contains('\\', StringComparison.Ordinal)
		|| path.Contains("//", StringComparison.Ordinal)
		|| path.Contains("%2e", StringComparison.OrdinalIgnoreCase)
		|| path.Contains("%2f", StringComparison.OrdinalIgnoreCase)
		|| path.Contains("%5c", StringComparison.OrdinalIgnoreCase);

	// ExternalLoginRoutes.IsExternalLoginRoute: case-insensitive, as routing matches, with a trailing slash tolerated.
	static bool IsExternalLoginRoute(string path)
	{
		var route = path.EndsWith('/') ?
			path[..^1] :
			path;
		foreach (var candidate in _externalLoginRoutes)
			if (string.Equals(route, candidate, StringComparison.OrdinalIgnoreCase))
				return true;
		return false;
	}
}
