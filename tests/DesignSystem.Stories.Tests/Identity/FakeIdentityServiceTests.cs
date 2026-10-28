using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Identity;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.Primitives;

namespace Norse.DesignSystem.Stories.Tests.Identity;

public sealed class FakeIdentityServiceTests
{
	static FakeIdentityService CreateFake(IdentityScenario scenario) =>
		new(new Scenario<IdentityScenario>(scenario));

	static GetMaskedPersonalDataRequest AnyMasked() =>
		new() { SubjectId = FakeAuthenticationService.CatalogCorrelationId };

	[Fact]
	async Task GetMyPersonalDataAsync_under_Success_discloses_the_catalog_subject_unmasked()
	{
		var outcome = await CreateFake(IdentityScenario.Success).GetMyPersonalDataAsync(new(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<PersonalDataResponse> success).ShouldBeTrue();
		success.Value.ShouldBe(new PersonalDataResponse { Email = "designer@example.com", PhoneNumber = "+15551234567" });
	}

	[Fact]
	async Task GetMyPersonalDataAsync_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(IdentityScenario.Fault).GetMyPersonalDataAsync(new(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task GetMyPersonalDataAsync_throws_loudly_on_the_sentinel() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(IdentityScenario.Unspecified).GetMyPersonalDataAsync(new(), TestContext.Current.CancellationToken));

	// The masks the PII primitives themselves produce (EmailAddress.Masked, PhoneNumber.Masked) — the
	// catalog never authors one.
	[Fact]
	async Task GetMaskedPersonalDataAsync_under_Success_discloses_the_primitives_masks()
	{
		var outcome = await CreateFake(IdentityScenario.Success).GetMaskedPersonalDataAsync(AnyMasked(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<MaskedPersonalDataResponse> success).ShouldBeTrue();
		success.Value.ShouldBe(new MaskedPersonalDataResponse { Email = "d***@e***.com", PhoneNumber = "***4567" });
	}

	[Fact]
	async Task GetMaskedPersonalDataAsync_under_Fault_carries_the_fixed_catalog_correlation_id()
	{
		var outcome = await CreateFake(IdentityScenario.Fault).GetMaskedPersonalDataAsync(AnyMasked(), TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.Fault);
		failed.Problem.CorrelationId.ShouldBe(FakeAuthenticationService.CatalogCorrelationId);
	}

	[Fact]
	async Task GetMaskedPersonalDataAsync_throws_loudly_on_the_sentinel() =>
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateFake(IdentityScenario.Unspecified).GetMaskedPersonalDataAsync(AnyMasked(), TestContext.Current.CancellationToken));
}
