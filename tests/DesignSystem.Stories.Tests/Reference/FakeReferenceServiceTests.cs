using Norse.Abstractions.Contracts;
using Norse.DesignSystem.Stories.Reference;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.Primitives;
using Norse.Reference;

namespace Norse.DesignSystem.Stories.Tests.Reference;

public sealed class FakeReferenceServiceTests
{
	// One holder + fake per test, pinned to the scenario under test — the fake itself is stateless;
	// all behavior selection lives in the ambient value.
	static FakeReferenceService CreateFake(ReferenceScenario scenario) =>
		new(new Scenario<ReferenceScenario>(scenario));

	static CountryRequest AnyCode(string code = "US") =>
		new() { CodeInput = code };

	[Fact]
	async Task GetCountry_under_Success_resolves_the_real_iso3166_row()
	{
		var outcome = await CreateFake(ReferenceScenario.Success).GetCountry(AnyCode("US"), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<CountryResponse> success).ShouldBeTrue();
		success.Value.Alpha2.ShouldBe("US");
		success.Value.Alpha3.ShouldBe("USA");
		success.Value.Name.ShouldBe("United States of America");
		success.Value.Code.ShouldBe(IsoCountryCode.UnitedStatesOfAmerica);
		success.Value.Id.ShouldBe(Iso3166.Ids[IsoCountryCode.UnitedStatesOfAmerica]);
	}

	[Fact]
	async Task GetCountry_under_Success_attaches_the_one_hand_fixtured_ancestry_chain_for_the_united_states()
	{
		var outcome = await CreateFake(ReferenceScenario.Success).GetCountry(AnyCode("US"), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<CountryResponse> success).ShouldBeTrue();
		success.Value.Region.ShouldNotBeNull();
		success.Value.Region.Name.ShouldBe("Americas");
		success.Value.Region.Subregion.ShouldNotBeNull();
		success.Value.Region.Subregion.Name.ShouldBe("Northern America");
		success.Value.Region.Subregion.IntermediateRegion.ShouldBeNull();
	}

	[Fact]
	async Task GetCountry_under_Success_leaves_region_null_for_every_other_code_including_antarcticas_real_absence()
	{
		var outcome = await CreateFake(ReferenceScenario.Success).GetCountry(AnyCode("AQ"), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<CountryResponse> success).ShouldBeTrue();
		success.Value.Name.ShouldBe("Antarctica");
		success.Value.Region.ShouldBeNull();
	}

	[Fact]
	async Task GetCountry_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(ReferenceScenario.Fault).GetCountry(AnyCode(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeReferenceService.CatalogCorrelationId);
	}

	[Fact]
	async Task GetCountry_throws_loudly_on_unspecified() =>
		await Should.ThrowAsync<InvalidOperationException>(() => CreateFake(ReferenceScenario.Unspecified).GetCountry(AnyCode(), TestContext.Current.CancellationToken));
}
