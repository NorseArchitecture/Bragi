using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.Primitives;
using Norse.Primitives.Pii;

namespace Norse.DesignSystem.Stories.Identity;

/// <summary>
///     Catalog-only stand-in for <see cref="IIdentityService" /> — never calls Himinbjörg, never touches
///     gRPC. A stateless switch over the ambient <see cref="IdentityScenario" /> serving one immutable
///     catalog subject. The masked response carries the masks the PII primitives themselves produce
///     (<see cref="EmailAddress.Masked" />, <see cref="PhoneNumber.Masked" />) — the catalog never authors one.
/// </summary>
sealed class FakeIdentityService(Scenario<IdentityScenario> scenario) : IIdentityService
{
	const string Email = "designer@example.com";

	const string Phone = "+15551234567";

	static readonly PersonalDataResponse _personalData = new() { Email = Email, PhoneNumber = Phone };

	static readonly MaskedPersonalDataResponse _maskedPersonalData = new()
	{
		Email = EmailAddress.Parse(Email).Match(static email => email.Masked, Unparsable),
		PhoneNumber = PhoneNumber.Parse(Phone).Match(static phone => phone.Masked, Unparsable)
	};

	/// <inheritdoc />
	public Task<Outcome<PersonalDataResponse>> GetMyPersonalDataAsync(GetMyPersonalDataRequest request,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			IdentityScenario.Success => Outcome<PersonalDataResponse>.Ok(_personalData),
			IdentityScenario.Fault =>
				Outcome<PersonalDataResponse>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GetMyPersonalDataAsync."),
		});

	/// <inheritdoc />
	public Task<Outcome<MaskedPersonalDataResponse>> GetMaskedPersonalDataAsync(GetMaskedPersonalDataRequest request,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			IdentityScenario.Success => Outcome<MaskedPersonalDataResponse>.Ok(_maskedPersonalData),
			IdentityScenario.Fault =>
				Outcome<MaskedPersonalDataResponse>.Err(ErrorCategory.Fault, correlationId: FakeAuthenticationService.CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GetMaskedPersonalDataAsync."),
		});

	static string Unparsable(Failure failure) =>
		throw new InvalidOperationException($"The catalog subject's fixture failed to parse: {failure}.");
}
