using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Norse.AuthN.Components;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.Reference;
using Norse.Reference.Components;

namespace Norse.DesignSystem.Stories.Tests;

public sealed class ServiceCollectionExtensionsTests
{
	// Mirrors the story host's composition: the host's own service registration provides logging,
	// and this extension provides everything the catalog's forms need — fake, scenario, and the real
	// client-side validators Blazilla resolves from DI (a form that can't validate is a catalog that lies).
	static ServiceProvider Build()
	{
		ServiceCollection services = new();
		services.AddLogging();
		services.AddNorseStoryFakes();
		return services.BuildServiceProvider();
	}

	[Fact]
	void Registers_the_login_validator_blazilla_resolves()
	{
		using var provider = Build();
		provider.GetRequiredService<IValidator<LoginRequest>>().ShouldBeOfType<LoginRequestValidator>();
	}

	[Fact]
	void Registers_the_register_validator_with_the_fake_behind_its_email_check()
	{
		using var provider = Build();
		provider.GetRequiredService<IValidator<RegisterRequest>>().ShouldBeOfType<RegisterRequestValidator>();
	}

	[Fact]
	void Registers_the_fake_as_the_same_instance_within_one_scope()
	{
		using var provider = Build();
		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<IAuthenticationService>()
			.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
	}

	[Fact]
	void A_new_scope_gets_its_own_authentication_fake_instance()
	{
		using var provider = Build();
		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		scopeA.ServiceProvider.GetRequiredService<IAuthenticationService>()
			.ShouldNotBeSameAs(scopeB.ServiceProvider.GetRequiredService<IAuthenticationService>());
	}

	[Fact]
	void A_new_scope_gets_its_own_authentication_scenario_instance()
	{
		using var provider = Build();
		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		scopeA.ServiceProvider.GetRequiredService<Scenario<AuthenticationScenario>>()
			.ShouldNotBeSameAs(scopeB.ServiceProvider.GetRequiredService<Scenario<AuthenticationScenario>>());
	}

	[Fact]
	void Registers_the_country_request_validator_blazilla_resolves()
	{
		using var provider = Build();
		provider.GetRequiredService<IValidator<CountryRequest>>().ShouldBeOfType<CountryRequestValidator>();
	}

	[Fact]
	void Registers_the_reference_fake_as_the_same_instance_within_one_scope()
	{
		using var provider = Build();
		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<IReferenceService>()
			.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<IReferenceService>());
	}

	[Fact]
	void A_new_scope_gets_its_own_reference_fake_instance()
	{
		using var provider = Build();
		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		scopeA.ServiceProvider.GetRequiredService<IReferenceService>()
			.ShouldNotBeSameAs(scopeB.ServiceProvider.GetRequiredService<IReferenceService>());
	}

	[Fact]
	void Registers_the_recorder_as_the_catalogs_session_transition()
	{
		using var provider = Build();
		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<ISessionTransition>()
			.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<RecordingSessionTransition>());
	}

	[Fact]
	void A_new_scope_gets_its_own_session_transition_recorder()
	{
		using var provider = Build();
		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		scopeA.ServiceProvider.GetRequiredService<RecordingSessionTransition>()
			.ShouldNotBeSameAs(scopeB.ServiceProvider.GetRequiredService<RecordingSessionTransition>());
	}
}
