using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Norse.AuthN.Components;
using Norse.AuthN.Services;

namespace Norse.DesignSystem.Stories.Tests;

public sealed class ServiceCollectionExtensionsTests
{
	// Mirrors the story host's composition: WASM's default host provides logging; the extension
	// provides everything the catalog's forms need — fake, scenario, and the real client-side
	// validators Blazilla resolves from DI (a form that can't validate is a catalog that lies).
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
	void Registers_the_fake_and_its_scenario_as_the_same_singletons()
	{
		using var provider = Build();
		provider.GetRequiredService<IAuthenticationService>().ShouldBeSameAs(provider.GetRequiredService<IAuthenticationService>());
	}
}
