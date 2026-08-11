using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Norse.AuthN.Components.FluentUI;
using Norse.Reference.Components.FluentUI;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Reference;
using Norse.DesignSystem.Stories.Scenarios;

namespace Norse.DesignSystem.Stories.Tests;

/// <summary>
///     The nested-doll lock. A driven story that reaches the fake's <c>Success</c> branch gets back
///     <c>NextUrl = "/"</c>, and both auth forms navigate it with <c>forceLoad: true</c> — a real
///     document load, inside BlazingStory's canvas iframe, which boots the whole catalog inside the
///     preview pane. Browser-only as a symptom; the ignition is plain C#, and
///     <see cref="BunitNavigationManager" /> records navigation instead of performing it, so every
///     driven story can assert it here. See KNOWN-ISSUES.md.
/// </summary>
public sealed class DrivenStoryNavigationTests : BunitContext
{
	public DrivenStoryNavigationTests()
	{
		JSInterop.Mode = JSRuntimeMode.Loose;
		Services.AddFluentUIComponents();
		Services.AddLogging();
		Services.AddNorseStoryFakes();
	}

	BunitNavigationManager Navigation =>
		Services.GetRequiredService<BunitNavigationManager>();

	// Login / "Validation Errors" -- SubmitOnly, no ScenarioScope, so the ambient scenario is Success.
	// Nothing but the client-side gate stands between an empty submit and a forced navigation to "/".
	[Fact]
	async Task Login_validation_errors_renders_messages_without_navigating()
	{
		var story = Render<Login>();

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		story.Markup.ShouldContain("must not be empty");
	}

	// Register / "Validation Errors" -- the same shape, and the one that was actually broken: its only
	// guard was an extension-style editContext.ValidateAsync(), which .NET 11's instance overload
	// shadows into returning true for everything. The empty form reached the fake, got Success, and
	// force-navigated.
	[Fact]
	async Task Register_validation_errors_renders_messages_without_navigating()
	{
		var story = Render<Register>();

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		story.Markup.ShouldContain("must not be empty");
	}

	[Theory]
	[InlineData(AuthenticationScenario.InvalidCredentials)]
	[InlineData(AuthenticationScenario.LockedOut)]
	[InlineData(AuthenticationScenario.NotAllowed)]
	async Task A_pinned_login_story_renders_its_state_without_navigating(AuthenticationScenario scenario)
	{
		var story = Render<ScenarioScope<AuthenticationScenario>>(parameters => parameters
			.Add(scope => scope.Value, scenario)
			.AddChildContent<Login>());
		Fill(story, "designer@example.com", "aaaaaaaa");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(AuthenticationScenario.RegistrationConflict, "taken@example.com")]
	[InlineData(AuthenticationScenario.RegistrationValidation, "designer@example.com")]
	async Task A_pinned_register_story_renders_its_state_without_navigating(
		AuthenticationScenario scenario, string email)
	{
		var story = Render<ScenarioScope<AuthenticationScenario>>(parameters => parameters
			.Add(scope => scope.Value, scenario)
			.AddChildContent<Register>());
		Fill(story, email, "aaaaaaaa");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
	}

	// Characterization, not aspiration: this documents the ignition the other tests exist to keep
	// unreachable. Success is simultaneously the scenario a released pin restores and the only one
	// that navigates, so a driven story that loses its pin does not render wrong -- it boots the
	// catalog inside its own canvas. Deleting this test does not make the hazard go away; making the
	// catalog's navigation inert would.
	[Fact]
	async Task An_unpinned_driven_story_force_navigates_which_is_what_boots_the_catalog_nested()
	{
		var story = Render<Register>();
		Fill(story, "designer@example.com", "aaaaaaaa");

		await story.Find("form").SubmitAsync();

		var navigation = Navigation.History.ShouldHaveSingleItem();
		navigation.Uri.ShouldBe("/");
		navigation.Options.ForceLoad.ShouldBeTrue();
	}

	// The Reference family has no navigating success path today -- CountryLookup's continuation only
	// sets local state. These hold that line: the moment a reference story gains one, it inherits the
	// whole nested-doll hazard, and this is where that shows up.
	[Fact]
	async Task Country_lookup_validation_errors_renders_messages_without_navigating()
	{
		var story = Render<CountryLookup>();

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		story.Markup.ShouldContain("Enter a country code.");
	}

	[Fact]
	async Task A_resolved_country_lookup_story_does_not_navigate()
	{
		var story = Render<CountryLookup>();
		Fill(story, "US");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		story.Markup.ShouldContain("United States");
	}

	[Fact]
	async Task A_pinned_country_lookup_fault_story_does_not_navigate()
	{
		var story = Render<ScenarioScope<ReferenceScenario>>(parameters => parameters
			.Add(scope => scope.Value, ReferenceScenario.Fault)
			.AddChildContent<CountryLookup>());
		Fill(story, "US");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
	}

	// Mirrors storyDriver.js: fill every text input the story exposes, in order.
	static void Fill<TComponent>(IRenderedComponent<TComponent> story, params string[] values)
		where TComponent : Microsoft.AspNetCore.Components.IComponent
	{
		var inputs = story.FindAll("fluent-text-input");
		for (var index = 0; index < values.Length; index++)
			inputs[index].Change(values[index]);
	}
}
