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
	void A_story_can_change_the_ambient_value()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success) { Value = AuthenticationScenario.LockedOut };
		scenario.Value.ShouldBe(AuthenticationScenario.LockedOut);
	}

	[Fact]
	void Reset_restores_the_initial_value()
	{
		Scenario<AuthenticationScenario> scenario = new(AuthenticationScenario.Success) { Value = AuthenticationScenario.Fault };
		scenario.Reset();
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
