using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
	void Passes_the_new_password_as_the_fifth_argument()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		var drive = module.Setup<bool>("drive", _ => true);
		var component = Render<StoryDriver>(parameters =>
			parameters
				.Add(p => p.Mode, StoryDriverMode.FillAndSubmit)
				.Add(p => p.Password, "aaaaaaaa")
				.Add(p => p.NewPassword, "cccccccc"));
		AssertDriveArguments(
			module.Invocations["drive"].Single(),
			component.Find("[data-norse-story-driver-state]"),
			true,
			"designer@example.com",
			"aaaaaaaa",
			"cccccccc");
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

	// A rejected drive is the story's state, not the circuit's end: the marker reads "failed" and the
	// renderer sees no unhandled exception, so one stalled story never tears the canvas down. The reason is
	// kept, not swallowed: the marker carries the message and the rejection is logged at Warning, so a broken
	// play function stays distinguishable from a settled one.
	[Fact]
	void A_rejected_drive_marks_the_driver_failed_without_throwing()
	{
		RecordingLogger<StoryDriver> logger = new();
		Services.AddSingleton<ILogger<StoryDriver>>(logger);
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

		JSException rejection = new("StoryDriver observed no settled post-submit DOM activity.");
		drive.SetException(rejection);
		component.WaitForAssertion(() =>
			component.Find("[data-norse-story-driver-state]")
				.GetAttribute("data-norse-story-driver-state").ShouldBe("failed"));
		Renderer.UnhandledException.IsCompleted.ShouldBeFalse();
		component.Find("[data-norse-story-driver-state]").GetAttribute("data-failure-reason")
			.ShouldBe("StoryDriver observed no settled post-submit DOM activity.");
		var entry = logger.Entries.ShouldHaveSingleItem();
		entry.Level.ShouldBe(LogLevel.Warning);
		entry.Exception.ShouldBeSameAs(rejection);
	}

	[Fact]
	void A_driven_story_carries_no_failure_reason()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		module.Setup<bool>("drive", _ => true).SetResult(true);
		var component = Render<StoryDriver>(parameters =>
			parameters.Add(p => p.Mode, StoryDriverMode.SubmitOnly));

		component.WaitForAssertion(() =>
			component.Find("[data-norse-story-driver-state]")
				.GetAttribute("data-norse-story-driver-state").ShouldBe("complete"));
		component.Find("[data-norse-story-driver-state]").HasAttribute("data-failure-reason").ShouldBeFalse();
	}

	sealed class RecordingLogger<T> : ILogger<T>
	{
		public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull =>
			null;

		public bool IsEnabled(LogLevel logLevel) =>
			true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter) =>
			Entries.Add((logLevel, exception));
	}

	[Fact]
	void Unspecified_mode_throws_instead_of_silently_rendering_an_undriven_story()
	{
		Should.Throw<InvalidOperationException>(() =>
			Render<StoryDriver>(parameters =>
				parameters.Add(p => p.Mode, StoryDriverMode.Unspecified)));
	}

	[Fact]
	void Click_only_invokes_the_click_driver_and_reports_complete()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		var driveClick = module.Setup<bool>("driveClick", _ => true);
		var component = Render<StoryDriver>(parameters =>
			parameters
				.Add(p => p.Mode, StoryDriverMode.ClickOnly)
				.AddChildContent("<button>Log out</button>"));

		var marker = component.Find("[data-norse-story-driver-state]");
		marker.GetAttribute("data-norse-story-driver-state").ShouldBe("pending");
		var invocation = module.Invocations["driveClick"].Single();
		invocation.Arguments.Count.ShouldBe(1);
		invocation.Arguments[0].ShouldBeElementReferenceTo(marker);

		driveClick.SetResult(true);
		component.WaitForAssertion(() =>
			component.Find("[data-norse-story-driver-state]")
				.GetAttribute("data-norse-story-driver-state").ShouldBe("complete"));
	}

	[Fact]
	async Task A_click_driver_that_finds_no_button_throws()
	{
		var module = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		var driveClick = module.Setup<bool>("driveClick", _ => true);
		Render<StoryDriver>(parameters =>
			parameters.Add(p => p.Mode, StoryDriverMode.ClickOnly));

		driveClick.SetResult(false);
		(await Renderer.UnhandledException)
			.ShouldBeOfType<InvalidOperationException>()
			.Message.ShouldBe("StoryDriver found no button to drive.");
	}

	static void AssertDriveArguments(
		JSRuntimeInvocation invocation,
		IElement expectedWrapper,
		bool fill,
		string email,
		string password,
		string newPassword = "bbbbbbbb")
	{
		invocation.Arguments.Count.ShouldBe(5);
		invocation.Arguments[0].ShouldBeElementReferenceTo(expectedWrapper);
		invocation.Arguments[1].ShouldBe(fill);
		invocation.Arguments[2].ShouldBe(email);
		invocation.Arguments[3].ShouldBe(password);
		invocation.Arguments[4].ShouldBe(newPassword);
	}
}
