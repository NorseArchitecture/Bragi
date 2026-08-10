using Bunit;
using Microsoft.JSInterop;
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
	void A_successfully_driven_story_stays_pending_until_drive_settles_then_reports_complete()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		var drive = module.Setup<bool>("drive", true, "taken@example.com", "aaaaaaaa");

		var component = Render<StoryDriver>(parameters =>
			parameters
				.Add(p => p.Mode, StoryDriverMode.FillAndSubmit)
				.Add(p => p.Email, "taken@example.com")
				.AddChildContent("scenario"));

		var marker = component.Find("[data-norse-story-driver-state]");
		marker.GetAttribute("style").ShouldBe("display: contents;");
		marker.GetAttribute("data-norse-story-driver-state").ShouldBe("pending");
		marker.TextContent.ShouldContain("scenario");

		drive.SetResult(true);
		component.WaitForAssertion(() =>
			component.Find("[data-norse-story-driver-state]")
				.GetAttribute("data-norse-story-driver-state").ShouldBe("complete"));
	}

	[Fact]
	void A_driver_that_finds_no_form_throws()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		module.Setup<bool>("drive", false, "designer@example.com", "aaaaaaaa").SetResult(false);

		Should.Throw<InvalidOperationException>(() =>
			Render<StoryDriver>(parameters => parameters.Add(p => p.Mode, StoryDriverMode.SubmitOnly)))
			.Message.ShouldBe("StoryDriver found no form to drive.");
	}

	[Fact]
	void A_driver_settlement_failure_surfaces_the_javascript_exception()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		module
			.Setup<bool>("drive", true, "designer@example.com", "aaaaaaaa")
			.SetException(new JSException("StoryDriver observed no settled post-submit DOM activity."));

		Should.Throw<JSException>(() =>
			Render<StoryDriver>(parameters => parameters.Add(p => p.Mode, StoryDriverMode.FillAndSubmit)))
			.Message.ShouldContain("no settled post-submit DOM activity");
	}

	[Fact]
	void Unspecified_mode_throws_instead_of_silently_rendering_an_undriven_story()
	{
		Should.Throw<InvalidOperationException>(() =>
			Render<StoryDriver>(parameters =>
				parameters.Add(p => p.Mode, StoryDriverMode.Unspecified)));
	}
}
