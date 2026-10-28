using System.Buffers.Text;
using System.Collections.Immutable;
using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Account;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;

namespace Norse.DesignSystem.Stories.Passkeys;

/// <summary>
///     Catalog-only stand-in for <see cref="IPasskeyService" /> — never calls Himinbjörg, never touches gRPC. A
///     stateless switch over the account tier's ambient <see cref="AccountScenario" />, the
///     <see cref="FakeAccountService" /> shape. Every answer is built afresh from immutable seeds — a named and an
///     unnamed passkey, each with <c>CredentialIds</c>'s server-shaped rename URL — since the wire records are mutable
///     and one story host serves every circuit. The reads answer every
///     non-<see cref="AccountScenario.Fault" /> scenario, <see cref="AccountScenario.NoPasskeys" /> listing none; the
///     writes mirror the real handlers verbatim (<c>Identity.Web.Server/Passkeys/*Handler.cs</c>), the id refusals
///     included: an id that is not base64url answers the handler's <c>CredentialId</c> validation and one the fixture
///     does not hold its not-found. A registration lands on the unnamed fixture's rename page, since a new passkey is
///     unnamed. Parity tests pin every shape. Scenarios that do not apply to a method throw — a story arming the wrong
///     scenario is an authoring error.
/// </summary>
sealed class FakePasskeyService(Scenario<AccountScenario> scenario) : IPasskeyService
{
	// The seeds: credential id and name. The wire records are mutable and the one story host serves every circuit, so
	// each answer is built afresh from these immutable seeds and shares nothing.
	static readonly ImmutableArray<(string CredentialId, string Name)> _seeds = [("cred-one", "Work laptop"), ("cred-two", "")];

	// The unnamed seed: a new passkey is unnamed.
	const string NewCredentialId = "cred-two";

	/// <inheritdoc />
	public Task<Outcome<PasskeysResponse>> GetPasskeys(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Unspecified => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GetPasskeys."),
			AccountScenario.NoPasskeys => Outcome<PasskeysResponse>.Ok(new() { Passkeys = [] }),
			AccountScenario.Fault => Fault<PasskeysResponse>(),
			_ => Outcome<PasskeysResponse>.Ok(new() { Passkeys = [.. _seeds.Select(static seed => Entry(seed.CredentialId, seed.Name))] }),
		});

	/// <inheritdoc />
	public Task<Outcome<PasskeyEntry>> GetPasskey(GetPasskeyRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		return Task.FromResult(scenario.Value switch
		{
			AccountScenario.Unspecified => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GetPasskey."),
			AccountScenario.Fault => Fault<PasskeyEntry>(),
			_ => Find(request.CredentialId) is { } failed ?
				new Outcome<PasskeyEntry>(failed) :
				Outcome<PasskeyEntry>.Ok(Entry(request.CredentialId, _seeds.Single(seed => seed.CredentialId == request.CredentialId).Name)),
		});
	}

	/// <inheritdoc />
	public Task<Outcome<PasskeyCreationOptionsResponse>> GetPasskeyCreationOptions(CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Unspecified =>
				throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GetPasskeyCreationOptions."),
			AccountScenario.Fault => Fault<PasskeyCreationOptionsResponse>(),
			_ => Outcome<PasskeyCreationOptionsResponse>.Ok(new() { OptionsJson = "{}", AttestationState = "story" }),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> AddPasskey(AddPasskeyRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success => Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = RenameUrl(NewCredentialId) }),
			AccountScenario.Fault => Fault<NavigationResult>(),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to AddPasskey."),
		});

	/// <inheritdoc />
	public Task<Outcome<NavigationResult>> RenamePasskey(RenamePasskeyRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		return Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success => Find(request.CredentialId) is { } failed ?
				new Outcome<NavigationResult>(failed) :
				Outcome<NavigationResult>.Ok(new NavigationResult { NextUrl = "/Account/Manage/Passkeys?status=passkey-updated" }),
			AccountScenario.Fault => Fault<NavigationResult>(),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to RenamePasskey."),
		});
	}

	/// <inheritdoc />
	public Task<Outcome<BoolResponse>> RemovePasskey(RemovePasskeyRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		return Task.FromResult(scenario.Value switch
		{
			AccountScenario.Success => Find(request.CredentialId) is { } failed ?
				new Outcome<BoolResponse>(failed) :
				Outcome<BoolResponse>.Ok(new BoolResponse { Value = true }),
			AccountScenario.Fault => Fault<BoolResponse>(),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to RemovePasskey."),
		});
	}

	// The handlers' id checks, in their order: decode first, then the caller's passkeys; null when a seed holds it.
	// CredentialIds.Invalid and .NotFound, built afresh.
	Failed? Find(string credentialId) =>
		!Base64Url.IsValid(credentialId) ?
			new(new Problem
			{
				Category = ErrorCategory.Validation,
				Errors = new Dictionary<string, string[]> { [nameof(GetPasskeyRequest.CredentialId)] = ["The passkey id is not valid."] }
			}) :
			scenario.Value is not AccountScenario.NoPasskeys && _seeds.Any(seed => seed.CredentialId == credentialId) ?
				null :
				new(Problem.ModelError(ErrorCategory.NotFound, "The passkey was not found."));

	// CredentialIds.Entry and .RenameUrl under an empty PathBase.
	static PasskeyEntry Entry(string credentialId, string name) =>
		new() { CredentialId = credentialId, Name = name, RenameUrl = RenameUrl(credentialId) };

	static string RenameUrl(string credentialId) =>
		$"/Account/Manage/RenamePasskey/{credentialId}";

	static Outcome<T> Fault<T>()
		where T : notnull =>
		Outcome<T>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId);
}
