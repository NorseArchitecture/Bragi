using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Norse.AuthN.Components;
using Norse.AuthN.Components.FluentUI;
using Norse.Reference.Components.FluentUI;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Reference;
using Norse.DesignSystem.Stories.Scenarios;

namespace Norse.DesignSystem.Stories.Tests;

/// <summary>
///     The nested-doll lock. A driven story that reaches the fake's <c>Success</c> branch records a
///     suppressed transition instead of navigating; these tests are where pin loss stays loud. See
///     KNOWN-ISSUES.md.
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

	RecordingSessionTransition SessionTransitions =>
		Services.GetRequiredService<RecordingSessionTransition>();

	// Login / "Validation Errors" -- SubmitOnly, no ScenarioScope, so the ambient scenario is Success.
	// Nothing but the client-side gate stands between an empty submit and a forced navigation to "/".
	[Fact]
	async Task Login_validation_errors_renders_messages_without_navigating()
	{
		var story = Render<Login>();

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
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
		SessionTransitions.Transitions.ShouldBeEmpty();
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
		SessionTransitions.Transitions.ShouldBeEmpty();
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
		SessionTransitions.Transitions.ShouldBeEmpty();
	}

	// Inverted 2026-08-11 -- the ignition is neutered, exactly as the old characterization test's own
	// comment demanded. An unpinned driven Login story that reaches the fake's Success arm now begins
	// a session transition the catalog suppresses and records: pin loss stays a loud CI failure HERE,
	// and the canvas stops paying for it with a nested catalog.
	[Fact]
	async Task An_unpinned_driven_login_story_begins_a_session_transition_the_catalog_suppresses()
	{
		var story = Render<Login>();
		Fill(story, "designer@example.com", "aaaaaaaa");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/");
	}

	// Register never transitions: its handler signs nobody in, so Success is an ordinary soft
	// navigation to the server-resolved hop, not a forced reload -- correct for a real host. This
	// assertion only proves that much; it does NOT prove the catalog is safe. A live BlazingStory
	// iframe is its own running app instance, and a genuine NavigateTo from inside it resolves
	// against the catalog's own routes -- reproduced live, 2026-08-11, the identical nested-doll
	// symptom this file's own suppression tests exist to prevent. bUnit's BunitNavigationManager
	// cannot see this: it captures the call instead of letting a real router act on it, which is
	// exactly the gap that let this pass. Open, deferred, not fixed -- see KNOWN-ISSUES.md
	// "Register: still open".
	[Fact]
	async Task An_unpinned_driven_register_story_soft_navigates_and_never_transitions()
	{
		var story = Render<Register>();
		Fill(story, "designer@example.com", "aaaaaaaa");

		await story.Find("form").SubmitAsync();

		SessionTransitions.Transitions.ShouldBeEmpty();
		var navigation = Navigation.History.ShouldHaveSingleItem();
		navigation.Options.ForceLoad.ShouldBeFalse();
		navigation.Uri.ShouldBe("/");
	}

	// The suppressed-success state renders identically to the confirm state, so the catalog stages
	// no story for it -- this fact is where that state lives, loudly.
	[Fact]
	void A_confirmed_logout_begins_a_suppressed_session_transition()
	{
		var page = Render<Logout>();

		page.Find("button").Click();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/");
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
		SessionTransitions.Transitions.ShouldBeEmpty();
		story.Markup.ShouldContain("Enter a country code.");
	}

	[Fact]
	async Task A_resolved_country_lookup_story_does_not_navigate()
	{
		var story = Render<CountryLookup>();
		Fill(story, "US");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
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
		SessionTransitions.Transitions.ShouldBeEmpty();
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
