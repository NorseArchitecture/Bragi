using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;

namespace Norse.DesignSystem.Stories.Tests.Scenarios;

public sealed class ScenarioScopeTests : BunitContext
{
	readonly Scenario<AuthenticationScenario> _scenario = new(AuthenticationScenario.Success);

	public ScenarioScopeTests() =>
		Services.AddSingleton(_scenario);

	[Fact]
	void Rendering_pins_the_ambient_scenario_to_the_declared_value()
	{
		Render<ScenarioScope<AuthenticationScenario>>(parameters =>
			parameters.Add(p => p.Value, AuthenticationScenario.LockedOut));
		_scenario.Value.ShouldBe(AuthenticationScenario.LockedOut);
	}

	[Fact]
	void Re_rendering_with_a_new_value_re_pins_every_time_so_a_persistent_canvas_cannot_leak()
	{
		var cut = Render<ScenarioScope<AuthenticationScenario>>(parameters =>
			parameters.Add(p => p.Value, AuthenticationScenario.LockedOut));
		cut.Render(parameters =>
			parameters.Add(p => p.Value, AuthenticationScenario.Fault));
		_scenario.Value.ShouldBe(AuthenticationScenario.Fault);
	}

	[Fact]
	void Disposal_resets_the_ambient_scenario_to_its_initial_value()
	{
		var cut = Render<ScenarioScope<AuthenticationScenario>>(parameters =>
			parameters.Add(p => p.Value, AuthenticationScenario.LockedOut));
		cut.Instance?.Dispose();
		_scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	[Fact]
	void Child_content_renders_inside_the_scope()
	{
		var cut = Render<ScenarioScope<AuthenticationScenario>>(parameters =>
			parameters
				.Add(p => p.Value, AuthenticationScenario.Success)
				.AddChildContent("<p>canvas</p>"));
		cut.Markup.ShouldContain("<p>canvas</p>");
	}
}
