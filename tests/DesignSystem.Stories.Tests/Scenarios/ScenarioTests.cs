using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Scenarios;

namespace Norse.DesignSystem.Stories.Tests.Scenarios;

public sealed class ScenarioTests
{
	[Fact]
	void Starts_at_the_initial_value_supplied_at_construction()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success);
		scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	[Fact]
	void Pinning_changes_the_ambient_value()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success);
		scenario.Pin(AuthenticationScenario.LockedOut);
		scenario.Value.ShouldBe(AuthenticationScenario.LockedOut);
	}

	[Fact]
	void Disposing_a_superseded_pin_leaves_the_current_pin_intact()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success);
		var firstPin = scenario.Pin(AuthenticationScenario.LockedOut);
		var secondPin = scenario.Pin(AuthenticationScenario.LockedOut);

		ReferenceEquals(firstPin, secondPin).ShouldBeFalse();
		firstPin.Dispose();
		scenario.Value.ShouldBe(AuthenticationScenario.LockedOut);
		secondPin.Dispose();
		scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	[Fact]
	void Disposing_the_current_pin_restores_the_initial_value()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success);
		var pin = scenario.Pin(AuthenticationScenario.Fault);
		pin.Dispose();
		scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	[Fact]
	void Disposing_an_old_pin_twice_cannot_affect_a_later_owner()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success);
		var oldPin = scenario.Pin(AuthenticationScenario.LockedOut);
		oldPin.Dispose();
		var successorPin = scenario.Pin(AuthenticationScenario.Fault);

		oldPin.Dispose();

		scenario.Value.ShouldBe(AuthenticationScenario.Fault);
		successorPin.Dispose();
		scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	[Fact]
	void Disposing_the_newest_pin_restores_the_initial_value_not_the_superseded_pin()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success);
		var supersededPin = scenario.Pin(AuthenticationScenario.LockedOut);
		var newestPin = scenario.Pin(AuthenticationScenario.Fault);

		newestPin.Dispose();

		scenario.Value.ShouldBe(AuthenticationScenario.Success);
		supersededPin.Dispose();
		scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	// Platform enum law: 0 is the unspecified sentinel, real states start at 1, every value explicit.
	// Pinned so a reordering can never silently renumber a scenario.
	[Theory]
	[InlineData(AuthenticationScenario.Unspecified, 0)]
	[InlineData(AuthenticationScenario.Success, 1)]
	[InlineData(AuthenticationScenario.InvalidCredentials, 2)]
	[InlineData(AuthenticationScenario.LockedOut, 3)]
	[InlineData(AuthenticationScenario.NotAllowed, 4)]
	[InlineData(AuthenticationScenario.RegistrationConflict, 5)]
	[InlineData(AuthenticationScenario.RegistrationValidation, 6)]
	[InlineData(AuthenticationScenario.Fault, 7)]
	void Every_scenario_carries_its_ruled_explicit_value(AuthenticationScenario scenario, int value) =>
		((int)scenario).ShouldBe(value);
}
