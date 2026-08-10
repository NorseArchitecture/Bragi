using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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
		var host = Render<ScenarioScopeHost>(parameters =>
			parameters
				.Add(p => p.ShowA, true)
				.Add(p => p.ValueA, AuthenticationScenario.LockedOut));
		host.Render(parameters =>
			parameters
				.Add(p => p.ShowA, true)
				.Add(p => p.ValueA, AuthenticationScenario.Fault));
		_scenario.Value.ShouldBe(AuthenticationScenario.Fault);

		host.Render(parameters =>
			parameters.Add(p => p.ShowA, false));

		_scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	[Fact]
	void Disposal_resets_the_ambient_scenario_to_its_initial_value()
	{
		var host = Render<ScenarioScopeHost>(parameters =>
			parameters
				.Add(p => p.ShowA, true)
				.Add(p => p.ValueA, AuthenticationScenario.LockedOut));

		host.Render(parameters =>
			parameters.Add(p => p.ShowA, false));

		_scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	[Fact]
	void Disposing_a_stale_scope_cannot_reset_a_successor_pinned_to_a_different_value()
	{
		var host = Render<ScenarioScopeHost>(parameters =>
			parameters
				.Add(p => p.ShowA, true)
				.Add(p => p.ValueA, AuthenticationScenario.LockedOut));
		host.Render(parameters =>
			parameters
				.Add(p => p.ShowA, true)
				.Add(p => p.ShowB, true)
				.Add(p => p.ValueA, AuthenticationScenario.LockedOut)
				.Add(p => p.ValueB, AuthenticationScenario.Fault));
		_scenario.Value.ShouldBe(AuthenticationScenario.Fault);

		host.Render(parameters =>
			parameters
				.Add(p => p.ShowA, false)
				.Add(p => p.ShowB, true)
				.Add(p => p.ValueB, AuthenticationScenario.Fault));

		_scenario.Value.ShouldBe(AuthenticationScenario.Fault);

		host.Render(parameters =>
			parameters
				.Add(p => p.ShowA, false)
				.Add(p => p.ShowB, false));

		_scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	[Fact]
	void Disposing_a_stale_scope_cannot_reset_a_successor_pinned_to_the_same_value()
	{
		var host = Render<ScenarioScopeHost>(parameters =>
			parameters
				.Add(p => p.ShowA, true)
				.Add(p => p.ValueA, AuthenticationScenario.LockedOut));
		host.Render(parameters =>
			parameters
				.Add(p => p.ShowA, true)
				.Add(p => p.ShowB, true)
				.Add(p => p.ValueA, AuthenticationScenario.LockedOut)
				.Add(p => p.ValueB, AuthenticationScenario.LockedOut));
		_scenario.Value.ShouldBe(AuthenticationScenario.LockedOut);

		host.Render(parameters =>
			parameters
				.Add(p => p.ShowA, false)
				.Add(p => p.ShowB, true)
				.Add(p => p.ValueB, AuthenticationScenario.LockedOut));

		_scenario.Value.ShouldBe(AuthenticationScenario.LockedOut);

		host.Render(parameters =>
			parameters
				.Add(p => p.ShowA, false)
				.Add(p => p.ShowB, false));

		_scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	[Fact]
	void Re_rendering_a_superseded_scope_throws_without_stealing_the_successor_pin()
	{
		var host = Render<ScenarioScopeHost>(parameters =>
			parameters
				.Add(p => p.ShowA, true)
				.Add(p => p.ValueA, AuthenticationScenario.LockedOut));
		host.Render(parameters =>
			parameters
				.Add(p => p.ShowA, true)
				.Add(p => p.ShowB, true)
				.Add(p => p.ValueA, AuthenticationScenario.LockedOut)
				.Add(p => p.ValueB, AuthenticationScenario.Fault));

		Should.Throw<InvalidOperationException>(() =>
			host.Render(parameters =>
				parameters
					.Add(p => p.ShowA, true)
					.Add(p => p.ShowB, true)
					.Add(p => p.ValueA, AuthenticationScenario.NotAllowed)
					.Add(p => p.ValueB, AuthenticationScenario.Fault)));

		_scenario.Value.ShouldBe(AuthenticationScenario.Fault);
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

sealed class ScenarioScopeHost : ComponentBase
{
	[Parameter]
	public bool ShowA { get; set; }

	[Parameter]
	public bool ShowB { get; set; }

	[Parameter]
	public AuthenticationScenario ValueA { get; set; }

	[Parameter]
	public AuthenticationScenario ValueB { get; set; }

	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		if (ShowA)
		{
			builder.OpenComponent<ScenarioScope<AuthenticationScenario>>(0);
			builder.SetKey(nameof(ShowA));
			builder.AddAttribute(1, nameof(ScenarioScope<>.Value), ValueA);
			builder.CloseComponent();
		}

		if (ShowB)
		{
			builder.OpenComponent<ScenarioScope<AuthenticationScenario>>(2);
			builder.SetKey(nameof(ShowB));
			builder.AddAttribute(3, nameof(ScenarioScope<>.Value), ValueB);
			builder.CloseComponent();
		}
	}
}
