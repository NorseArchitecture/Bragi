using Norse.Abstractions.Contracts;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.Primitives;
using Norse.Reference;

namespace Norse.DesignSystem.Stories.Reference;

/// <summary>
///     Catalog-only stand-in for <see cref="IReferenceService" /> — never calls Mímisbrunnr's wire, never
///     touches gRPC. A stateless switch over the ambient <see cref="ReferenceScenario" />: behavior is
///     selected by the story (via <c>ScenarioScope</c>), never accumulated. Under
///     <see cref="ReferenceScenario.Success" />, every recognized code resolves its top-level fields for
///     real off the browser-safe <see cref="Iso3166" /> dataset (Id/Alpha2/Alpha3/Name/Code) — the
///     catalog never invents those. The ancestry chain lives only in Mímisbrunnr's EF-backed view model,
///     unreachable from a browser-safe assembly, so this fake carries one hand-fixtured chain (the real
///     UN M49 relationship: United States → Northern America → Americas) and leaves every other code's
///     <see cref="CountryResponse.Region" /> null — never wrong, just incomplete, exactly like
///     Antarctica's real absence.
/// </summary>
sealed class FakeReferenceService(Scenario<ReferenceScenario> scenario) : IReferenceService
{
	/// <summary>
	///     The fixed catalog correlation id for <see cref="ReferenceScenario.Fault" /> — obviously
	///     synthetic, the same sentinel <c>FakeAuthenticationService</c> uses.
	/// </summary>
	internal static readonly Guid CatalogCorrelationId = new("0badc0de-0bad-c0de-0bad-c0de0badc0de");

	// Ids are synthetic (the real ones are v5 hashes baked server-side and never rendered by
	// CountryLookup), built from the real UN M49 codes so they stay traceable to the row they stand in for.
	static readonly RegionResponse _americas = new()
	{
		Id = new Guid("00000019-0000-0000-0000-000000000019"),
		Code = "019",
		Name = "Americas",
		Subregion = new SubregionResponse
		{
			Id = new Guid("00000021-0000-0000-0000-000000000021"),
			Code = "021",
			Name = "Northern America",
			IntermediateRegion = null
		}
	};

	public Task<Outcome<CountryResponse>> GetCountry(CountryRequest request, CancellationToken cancellationToken = default) =>
		Task.FromResult(scenario.Value switch
		{
			ReferenceScenario.Success => request.Code.TryGetValue(out Success<IsoCountryCode> success)
				? Outcome<CountryResponse>.Ok(Resolve(success.Value))
				: throw new InvalidOperationException("CountryRequestValidator should have blocked this code before submit."),
			ReferenceScenario.Fault =>
				Outcome<CountryResponse>.Err(ErrorCategory.Fault, correlationId: CatalogCorrelationId),
			_ => throw new InvalidOperationException($"Scenario {scenario.Value} does not apply to GetCountry."),
		});

	static CountryResponse Resolve(IsoCountryCode code)
	{
		var row = Iso3166.All.First(r => r.Code == code);
		return new CountryResponse
		{
			Id = row.Id,
			Alpha2 = row.Alpha2,
			Alpha3 = row.Alpha3,
			Name = row.Name,
			Code = row.Code,
			Classification = Classification.None,
			Region = code == IsoCountryCode.UnitedStatesOfAmerica ? _americas : null
		};
	}
}
