using Bunit;
using Norse.DesignSystem.Stories.Scenarios;

namespace Norse.DesignSystem.Stories.Tests.Scenarios;

public sealed class StoryDriverTests : BunitContext
{
	[Fact]
	void Fill_and_submit_invokes_the_module_with_fill_true_and_the_fixtures()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		module.Setup<bool>("drive", true, "taken@example.com", "aaaaaaaa").SetResult(true);
		Render<StoryDriver>(parameters =>
			parameters
				.Add(p => p.Mode, StoryDriverMode.FillAndSubmit)
				.Add(p => p.Email, "taken@example.com")
				.Add(p => p.Password, "aaaaaaaa"));
		module.VerifyInvoke("drive");
	}

	[Fact]
	void Submit_only_invokes_the_module_with_fill_false()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		module.Setup<bool>("drive", false, "designer@example.com", "aaaaaaaa").SetResult(true);
		Render<StoryDriver>(parameters =>
			parameters.Add(p => p.Mode, StoryDriverMode.SubmitOnly));
		module.VerifyInvoke("drive");
	}

	[Fact]
	void Unspecified_mode_throws_instead_of_silently_rendering_an_undriven_story()
	{
		Should.Throw<InvalidOperationException>(() =>
			Render<StoryDriver>(parameters =>
				parameters.Add(p => p.Mode, StoryDriverMode.Unspecified)));
	}
}
