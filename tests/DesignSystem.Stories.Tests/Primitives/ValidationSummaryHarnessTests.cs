using Bunit;
using Microsoft.FluentUI.AspNetCore.Components;
using Norse.DesignSystem.Stories.Primitives;

namespace Norse.DesignSystem.Stories.Tests.Primitives;

public sealed class ValidationSummaryHarnessTests : BunitContext
{
	public ValidationSummaryHarnessTests()
	{
		Services.AddFluentUIComponents();
		// FluentUI components make JS interop calls bunit has no way to know about in advance —
		// loose mode is bunit's own documented answer (Heimdall's ModelValidationSummaryTests precedent).
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Fact]
	void Renders_the_seeded_model_level_messages()
	{
		var cut = Render<ValidationSummaryHarness>(parameters =>
			parameters.Add(p => p.Messages, ["first message", "second message"]));
		cut.Markup.ShouldContain("first message");
		cut.Markup.ShouldContain("second message");
	}

	[Fact]
	void Parameter_changes_replace_the_messages_instead_of_accumulating()
	{
		var cut = Render<ValidationSummaryHarness>(parameters =>
			parameters.Add(p => p.Messages, ["first message"]));
		cut.Render(parameters =>
			parameters.Add(p => p.Messages, ["replacement message"]));
		cut.Markup.ShouldContain("replacement message");
		cut.Markup.ShouldNotContain("first message");
	}
}
