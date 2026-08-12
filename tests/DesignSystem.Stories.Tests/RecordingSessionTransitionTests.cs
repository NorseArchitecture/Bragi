namespace Norse.DesignSystem.Stories.Tests;

public sealed class RecordingSessionTransitionTests
{
	[Fact]
	void Records_every_begun_transition_in_order()
	{
		RecordingSessionTransition transition = new();

		transition.Begin(new() { NextUrl = "/" });
		transition.Begin(new() { NextUrl = "/Account/LoginWith2fa" });

		transition.Transitions.Select(t => t.NextUrl).ShouldBe(["/", "/Account/LoginWith2fa"]);
	}
}
