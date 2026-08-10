using AngleSharp.Dom;
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
		var drive = module.Setup<bool>("drive", _ => true);
		var component = Render<StoryDriver>(parameters =>
			parameters
				.Add(p => p.Mode, StoryDriverMode.FillAndSubmit)
				.Add(p => p.Email, "taken@example.com")
				.Add(p => p.Password, "aaaaaaaa"));
		AssertDriveArguments(
			module.Invocations["drive"].Single(),
			component.Find("[data-norse-story-driver-state]"),
			true,
			"taken@example.com",
			"aaaaaaaa");
		var initialRenderCount = component.RenderCount;
		drive.SetResult(true);
		component.WaitForState(() => component.RenderCount > initialRenderCount);
	}

	[Fact]
	void Submit_only_invokes_the_module_with_fill_false()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		var drive = module.Setup<bool>("drive", _ => true);
		var component = Render<StoryDriver>(parameters =>
			parameters.Add(p => p.Mode, StoryDriverMode.SubmitOnly));
		AssertDriveArguments(
			module.Invocations["drive"].Single(),
			component.Find("[data-norse-story-driver-state]"),
			false,
			"designer@example.com",
			"aaaaaaaa");
		var initialRenderCount = component.RenderCount;
		drive.SetResult(true);
		component.WaitForState(() => component.RenderCount > initialRenderCount);
	}

	[Fact]
	void A_successfully_driven_story_stays_pending_until_drive_settles_then_reports_complete()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		var drive = module.Setup<bool>("drive", _ => true);

		var component = Render<StoryDriver>(parameters =>
			parameters
				.Add(p => p.Mode, StoryDriverMode.FillAndSubmit)
				.Add(p => p.Email, "taken@example.com")
				.AddChildContent("scenario"));

		var marker = component.Find("[data-norse-story-driver-state]");
		marker.GetAttribute("style").ShouldBe("display: contents;");
		marker.GetAttribute("data-norse-story-driver-state").ShouldBe("pending");
		marker.TextContent.ShouldContain("scenario");
		AssertDriveArguments(
			module.Invocations["drive"].Single(),
			marker,
			true,
			"taken@example.com",
			"aaaaaaaa");

		drive.SetResult(true);
		component.WaitForAssertion(() =>
			component.Find("[data-norse-story-driver-state]")
				.GetAttribute("data-norse-story-driver-state").ShouldBe("complete"));
	}

	[Fact]
	async Task A_driver_that_finds_no_form_throws()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		var drive = module.Setup<bool>("drive", _ => true);
		var component = Render<StoryDriver>(parameters =>
			parameters.Add(p => p.Mode, StoryDriverMode.SubmitOnly));
		var marker = component.Find("[data-norse-story-driver-state]");
		AssertDriveArguments(
			module.Invocations["drive"].Single(),
			marker,
			false,
			"designer@example.com",
			"aaaaaaaa");

		drive.SetResult(false);
		(await Renderer.UnhandledException)
			.ShouldBeOfType<InvalidOperationException>()
			.Message.ShouldBe("StoryDriver found no form to drive.");
	}

	[Fact]
	async Task A_driver_settlement_failure_surfaces_the_javascript_exception()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		var drive = module.Setup<bool>("drive", _ => true);
		var component = Render<StoryDriver>(parameters =>
			parameters.Add(p => p.Mode, StoryDriverMode.FillAndSubmit));
		var marker = component.Find("[data-norse-story-driver-state]");
		AssertDriveArguments(
			module.Invocations["drive"].Single(),
			marker,
			true,
			"designer@example.com",
			"aaaaaaaa");

		drive.SetException(new JSException("StoryDriver observed no settled post-submit DOM activity."));
		(await Renderer.UnhandledException)
			.ShouldBeOfType<JSException>()
			.Message.ShouldContain("no settled post-submit DOM activity");
	}

	[Fact]
	void Unspecified_mode_throws_instead_of_silently_rendering_an_undriven_story()
	{
		Should.Throw<InvalidOperationException>(() =>
			Render<StoryDriver>(parameters =>
				parameters.Add(p => p.Mode, StoryDriverMode.Unspecified)));
	}

	static void AssertDriveArguments(
		JSRuntimeInvocation invocation,
		IElement expectedWrapper,
		bool fill,
		string email,
		string password)
	{
		invocation.Arguments.Count.ShouldBe(4);
		invocation.Arguments[0].ShouldBeElementReferenceTo(expectedWrapper);
		invocation.Arguments[1].ShouldBe(fill);
		invocation.Arguments[2].ShouldBe(email);
		invocation.Arguments[3].ShouldBe(password);
	}
}
