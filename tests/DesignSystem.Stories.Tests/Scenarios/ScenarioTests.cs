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
	void Pinning_issues_fresh_reference_identity_tokens()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success);
		var firstPin = scenario.Pin(AuthenticationScenario.LockedOut);
		var secondPin = scenario.Pin(AuthenticationScenario.LockedOut);
		object firstIdentity = firstPin;
		object secondIdentity = secondPin;

		firstIdentity.GetType().IsValueType.ShouldBeFalse();
		ReferenceEquals(firstIdentity, secondIdentity).ShouldBeFalse();
	}

	[Fact]
	void Releasing_the_current_pin_restores_the_initial_value()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success);
		var pin = scenario.Pin(AuthenticationScenario.Fault);
		scenario.Release(pin);
		scenario.Value.ShouldBe(AuthenticationScenario.Success);
	}

	[Fact]
	void Releasing_an_old_pin_twice_cannot_affect_a_later_owner()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success);
		var oldPin = scenario.Pin(AuthenticationScenario.LockedOut);
		scenario.Release(oldPin);
		var successorPin = scenario.Pin(AuthenticationScenario.Fault);

		scenario.Release(oldPin);

		scenario.Value.ShouldBe(AuthenticationScenario.Fault);
		scenario.Release(successorPin);
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
