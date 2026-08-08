using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;

namespace Norse.DesignSystem.Stories.Authentication;

/// <summary>
/// Catalog-only stand-in for <see cref="IAuthenticationService"/> — never calls Himinbjörg, never
/// touches gRPC. Lives here so the Login/Register/Logout stories and the fake they depend on ship
/// together; the story host (Yggdrasil's <c>Hosting.Stories.Client</c>) registers it via
/// <see cref="ServiceCollectionExtensions.AddNorseStoryFakes"/> and stays a pure composition root.
/// </summary>
sealed class FakeAuthenticationService : IAuthenticationService
{
	// LoginResult.Succeeded was deleted platform-wide (ruled 2026-08-06, see the type's own doc
	// comment) -- a rejected login is a Failed(Problem) instead, never a bare-success record with a
	// false flag. This fake reports success, with the next-hop URL always resolved to "/" -- there is
	// no real sign-in flow behind this story-host fake, so no 2FA challenge or deferred completion URL
	// is ever produced. The one exception is the sentinel below: it exists purely so Login's story can
	// preview the model-level validation-summary state without a running backend -- type it into the
	// story's own Email field, no separate control needed.
	static readonly Failed _invalidCredentials =
		new(Problem.ModelError(ErrorCategory.InvalidCredentials, "Invalid email or password."));

	const string InvalidCredentialsSentinelEmail = "fail@example.com";

	public Task<Outcome<LoginResult>> Login(LoginRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(request.Email.Equals(InvalidCredentialsSentinelEmail, StringComparison.OrdinalIgnoreCase)
			? new Outcome<LoginResult>(_invalidCredentials)
			: Outcome<LoginResult>.Ok(new LoginResult { NextUrl = "/" }));

	public Task<Outcome<RegisterResult>> Register(RegisterRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(Outcome<RegisterResult>.Ok(new RegisterResult { Succeeded = true }));

	// Always reports "not taken" -- a story-host fake with no real user store behind it; there is
	// nothing for a checked email to ever collide with.
	public Task<Outcome<BoolResponse>> EmailExists(EmailExistsRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(Outcome<BoolResponse>.Ok(new BoolResponse { Value = false }));

	public Task<Outcome<LogoutResult>> Logout(CancellationToken cancellationToken = default) =>
		Task.FromResult(Outcome<LogoutResult>.Ok(new LogoutResult()));
}
