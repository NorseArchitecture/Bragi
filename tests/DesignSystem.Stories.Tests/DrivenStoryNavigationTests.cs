using Bunit;
using Bunit.Rendering;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Norse.Abstractions.Contracts;
using Norse.AuthN.Components;
using Norse.AuthN.Components.FluentUI;
using Norse.AuthN.Components.FluentUI.Manage;
using Norse.AuthN.Services;
using Norse.Reference.Components.FluentUI;
using Norse.DesignSystem.Stories.Account;
using Norse.DesignSystem.Stories.Authentication;
using Norse.DesignSystem.Stories.Reference;
using Norse.DesignSystem.Stories.Scenarios;
using Norse.DesignSystem.Stories.TwoFactor;
using Norse.Primitives;

namespace Norse.DesignSystem.Stories.Tests;

/// <summary>
///     The nested-doll lock. A driven story that reaches the fake's <c>Success</c> branch records a
///     suppressed transition instead of navigating; these tests are where pin loss stays loud. See
///     KNOWN-ISSUES.md.
/// </summary>
public sealed class DrivenStoryNavigationTests : InteractiveBunitContext
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
		// WaitForAssertion, not a direct check: Blazilla 2.4.1's FluentValidator.OnValidationRequested
		// races its own re-render trigger (a fire-and-forget InvokeAsync(NotifyValidationStateChanged))
		// against the same awaited task SubmitAsync resolves on. The message store is populated
		// deterministically by the time SubmitAsync returns; whether the render committing it into
		// Markup has landed yet is not. Same shape at every other post-submit Markup assertion below.
		story.WaitForAssertion(() => story.Markup.ShouldContain("must not be empty"));
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
		// Same Blazilla FluentValidator re-render race as Login's version of this test, above.
		story.WaitForAssertion(() => story.Markup.ShouldContain("must not be empty"));
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

	// Train 1 surfaces. TwoFactorForm, RecoveryCodeForm and ConfirmEmailChangePanel change the principal,
	// so their Success arms begin a session transition the catalog suppresses and records -- the same lock
	// Login carries. ForgotPassword and ResetPasswordForm change no principal: Success is an ordinary
	// soft navigation, the Register shape, with the same live-iframe caveat (KNOWN-ISSUES.md), which is
	// why the catalog stages no driven success story for either.
	[Fact]
	async Task A_two_factor_success_records_one_transition_and_no_history()
	{
		var story = Render<TwoFactorForm>(parameters => parameters.Add(form => form.Challenge, "story"));
		Fill(story, "123456");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/");
	}

	[Theory]
	[InlineData(AuthenticationScenario.InvalidTwoFactorCode)]
	[InlineData(AuthenticationScenario.LockedOut)]
	async Task A_pinned_two_factor_story_renders_its_state_without_navigating(AuthenticationScenario scenario)
	{
		var story = Render<ScenarioScope<AuthenticationScenario>>(parameters => parameters
			.Add(scope => scope.Value, scenario)
			.AddChildContent<TwoFactorForm>(form => form.Add(twoFactor => twoFactor.Challenge, "story")));
		Fill(story, "000000");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
	}

	// Login / "Two-Factor Required" -- FillAndSubmit under TwoFactorRequired: the fake answers Ok with the
	// challenge hop, Login begins a session transition to it, and the catalog suppresses and records it.
	[Fact]
	async Task A_two_factor_required_login_story_records_one_transition_to_the_challenge()
	{
		var story = Render<ScenarioScope<AuthenticationScenario>>(parameters => parameters
			.Add(scope => scope.Value, AuthenticationScenario.TwoFactorRequired)
			.AddChildContent<Login>());
		Fill(story, "designer@example.com", "aaaaaaaa");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/Account/LoginWith2fa?challenge=story");
	}

	[Fact]
	async Task A_recovery_code_success_records_one_transition()
	{
		var story = Render<RecoveryCodeForm>(parameters => parameters.Add(form => form.Challenge, "story"));
		Fill(story, "3XK9-7QRT");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/");
	}

	[Fact]
	async Task A_forgot_password_success_records_one_soft_navigation()
	{
		var story = Render<ForgotPassword>();
		Fill(story, "designer@example.com");

		await story.Find("form").SubmitAsync();

		SessionTransitions.Transitions.ShouldBeEmpty();
		var navigation = Navigation.History.ShouldHaveSingleItem();
		navigation.Options.ForceLoad.ShouldBeFalse();
		navigation.Uri.ShouldBe("/Account/ForgotPasswordConfirmation");
	}

	// ForgotPassword / "Validation Errors" -- SubmitOnly under the ambient Success. The email gate must
	// stop an empty submit client-side: the substituted service proves nothing was dispatched, and the
	// empty history and transition list guard that no navigation happened either.
	[Fact]
	async Task An_empty_forgot_password_submit_renders_the_email_required_message()
	{
		var authentication = Substitute.For<IAuthenticationService>();
		Services.AddScoped(_ => authentication);
		var story = Render<ForgotPassword>();

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
		_ = authentication.DidNotReceiveWithAnyArgs().ForgotPassword(default!, Xunit.TestContext.Current.CancellationToken);
		// Same Blazilla FluentValidator re-render race as Login's version of this test, above.
		story.WaitForAssertion(() => story.Markup.ShouldContain("Enter your email address."));
	}

	[Fact]
	async Task A_reset_password_success_records_one_soft_navigation()
	{
		var story = Render<ResetPasswordForm>(parameters => parameters.Add(form => form.Code, "story"));
		Fill(story, "designer@example.com", "aaaaaaaa");

		await story.Find("form").SubmitAsync();

		SessionTransitions.Transitions.ShouldBeEmpty();
		var navigation = Navigation.History.ShouldHaveSingleItem();
		navigation.Options.ForceLoad.ShouldBeFalse();
		navigation.Uri.ShouldBe("/Account/ResetPasswordConfirmation");
	}

	[Fact]
	void A_confirm_email_click_renders_in_place_with_no_navigation()
	{
		var story = Render<ConfirmEmailPanel>(parameters => parameters
			.Add(panel => panel.UserId, "story")
			.Add(panel => panel.Code, "story"));

		story.Find("fluent-button").Click();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
		story.Markup.ShouldContain("Thank you for confirming your email.");
	}

	[Fact]
	void A_confirm_email_change_click_records_one_transition()
	{
		var story = Render<ConfirmEmailChangePanel>(parameters => parameters
			.Add(panel => panel.UserId, "story")
			.Add(panel => panel.Email, "designer@example.com")
			.Add(panel => panel.Code, "story"));

		story.Find("fluent-button").Click();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/Account/Login");
	}

	[Fact]
	void A_failed_confirm_email_story_records_nothing()
	{
		var story = Render<ScenarioScope<AuthenticationScenario>>(parameters => parameters
			.Add(scope => scope.Value, AuthenticationScenario.EmailConfirmationFailed)
			.AddChildContent<ConfirmEmailPanel>(panel => panel
				.Add(confirm => confirm.UserId, "story")
				.Add(confirm => confirm.Code, "story")));

		story.Find("fluent-button").Click();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
		story.Markup.ShouldContain("Error confirming your email.");
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
		// Same Blazilla FluentValidator re-render race as Login's version of this test, above.
		story.WaitForAssertion(() => story.Markup.ShouldContain("Enter a country code."));
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

	// Train 2 surfaces. ProfileForm, ChangePasswordForm, SetPasswordForm, ExternalLogins and
	// DeletePersonalDataForm refresh or end the sign-in, so their Success arms begin a session transition the
	// catalog suppresses and records -- the Login lock. ChangeEmailForm only sends a link: Success renders in
	// place and navigates nowhere.
	[Fact]
	async Task A_profile_save_records_one_transition_and_no_history()
	{
		var story = Render<ProfileForm>(parameters => parameters
			.Add(form => form.Email, "designer@example.com")
			.Add(form => form.PhoneNumber, ""));
		// The phone is the form's only editable text input; the read-only email precedes it.
		await story.FindAll("fluent-text-input")[1].ChangeAsync(new() { Value = "+15551234567" });

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/Account/Manage?status=phone-updated");
	}

	[Fact]
	async Task A_change_password_success_records_one_transition()
	{
		var story = Render<ChangePasswordForm>();
		Fill(story, "aaaaaaaa", "bbbbbbbb");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/Account/Manage/ChangePassword?status=password-changed");
	}

	[Theory]
	[InlineData(AccountScenario.IncorrectPassword, "Incorrect password.")]
	[InlineData(AccountScenario.PasswordPolicy, "Passwords must have at least one digit")]
	[InlineData(AccountScenario.Fault, "0badc0de-0bad-c0de-0bad-c0de0badc0de")]
	async Task A_pinned_change_password_story_renders_its_state_without_navigating(AccountScenario scenario, string expected)
	{
		var story = Render<ScenarioScope<AccountScenario>>(parameters => parameters
			.Add(scope => scope.Value, scenario)
			.AddChildContent<ChangePasswordForm>());
		Fill(story, "aaaaaaaa", "bbbbbbbb");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
		story.WaitForAssertion(() => story.Markup.ShouldContain(expected));
	}

	// SetPassword / "Set" -- staged under ExternalOnly, the account that actually sets a password.
	[Fact]
	async Task A_set_password_success_records_one_transition()
	{
		var story = Render<ScenarioScope<AccountScenario>>(parameters => parameters
			.Add(scope => scope.Value, AccountScenario.ExternalOnly)
			.AddChildContent<SetPasswordForm>());
		Fill(story, "aaaaaaaa");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/Account/Manage/ChangePassword?status=password-set");
	}

	[Fact]
	async Task A_change_email_success_renders_in_place_with_no_navigation()
	{
		var story = Render<ChangeEmailForm>(parameters => parameters
			.Add(form => form.Email, "designer@example.com")
			.Add(form => form.EmailConfirmed, true));
		// The new email is the form's only text input; the read-only current email sits above the form.
		await story.FindAll("fluent-text-input")[1].ChangeAsync(new() { Value = "new@example.com" });

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
		story.WaitForAssertion(() => story.Markup.ShouldContain("Confirmation link to change email sent. Please check your email."));
	}

	[Fact]
	void Removing_an_external_login_records_one_transition()
	{
		var page = Render<ExternalLogins>();

		page.Find("fluent-button").Click();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/Account/Manage/ExternalLogins?status=external-login-removed");
	}

	[Fact]
	void An_external_only_account_renders_no_remove_button()
	{
		var story = Render<ScenarioScope<AccountScenario>>(parameters => parameters
			.Add(scope => scope.Value, AccountScenario.ExternalOnly)
			.AddChildContent<ExternalLogins>());

		story.Markup.ShouldContain("Google");
		story.FindAll("fluent-button").ShouldBeEmpty();
	}

	[Fact]
	async Task A_delete_personal_data_success_records_one_transition()
	{
		var story = Render<DeletePersonalDataForm>(parameters => parameters.Add(form => form.HasPassword, true));
		Fill(story, "aaaaaaaa");

		await story.Find("form").SubmitAsync();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/");
	}

	// The download only saves a file, so the catalog stages no driven "Download" story -- this fact is
	// where that state lives: the click hands the catalog subject to the download module and navigates nowhere.
	[Fact]
	void A_personal_data_download_saves_the_subject_without_navigating()
	{
		var module = JSInterop.SetupModule("./_content/Norse.AuthN.Components.FluentUI/downloadFile.js");
		module.SetupVoid("downloadJson", _ => true).SetVoidResult();
		var page = Render<PersonalData>();

		page.Find("fluent-button").Click();

		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
		page.WaitForAssertion(() =>
		{
			var invocation = module.VerifyInvoke("downloadJson");
			invocation.Arguments[0].ShouldBe("PersonalData.json");
			invocation.Arguments[1].ShouldBeOfType<PersonalDataResponse>().Email.ShouldBe("designer@example.com");
		});
	}

	// External Logins / "Removed" -- the compiled story itself (StoryCatalog), not a hand-built copy, so a deleted
	// story, a lost driver or a changed Mode fails here. The driver must take the ClickOnly path (driveClick, never
	// drive) over the page; the click, played as driveClick plays it, leaves the canvas byte-identical -- why the
	// click settles without a mutation (T3-7) -- and records the one suppressed transition.
	[Fact]
	void The_external_logins_removed_story_click_drives_the_page_and_records_one_transition()
	{
		var driver = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		var driveClick = driver.Setup<bool>("driveClick", _ => true);

		var story = StoryCatalog.RenderStory<Norse.DesignSystem.Stories.Account.ExternalLogins_stories>(this, "Removed");

		story.FindComponent<StoryDriver>().FindComponent<ExternalLogins>();
		driver.Invocations["driveClick"].ShouldHaveSingleItem();
		driver.Invocations["drive"].ShouldBeEmpty();
		var before = story.Markup;
		ClickAsTheDriverDoes(story);
		story.Markup.ShouldBe(before);
		driveClick.SetResult(true);

		story.WaitForAssertion(() => DriverState(story).ShouldBe("complete"));
		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe("/Account/Manage/ExternalLogins?status=external-login-removed");
	}

	// Personal Data / "Download" -- the compiled story itself: a ClickOnly driver over the page, whose first button is
	// Download, and whose click leaves the canvas byte-identical. What the download saves is
	// A_personal_data_download_saves_the_subject_without_navigating's lock, not this one's.
	[Fact]
	void The_personal_data_download_story_click_drives_the_download_and_leaves_the_canvas_unchanged()
	{
		JSInterop.SetupModule("./_content/Norse.AuthN.Components.FluentUI/downloadFile.js")
			.SetupVoid("downloadJson", _ => true).SetVoidResult();
		var driver = JSInterop.SetupModule("./_content/Norse.DesignSystem.Stories/storyDriver.js");
		var driveClick = driver.Setup<bool>("driveClick", _ => true);

		var story = StoryCatalog.RenderStory<Norse.DesignSystem.Stories.Identity.PersonalData_stories>(this, "Download");

		story.FindComponent<StoryDriver>().FindComponent<PersonalData>();
		driver.Invocations["driveClick"].ShouldHaveSingleItem();
		driver.Invocations["drive"].ShouldBeEmpty();
		var before = story.Markup;
		ClickAsTheDriverDoes(story).TextContent.Trim().ShouldBe("Download");
		story.Markup.ShouldBe(before);
		driveClick.SetResult(true);

		story.WaitForAssertion(() => DriverState(story).ShouldBe("complete"));
		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
	}

	// Train 3 surfaces. Forget, Reset, Disable and Enable rotate the stamp or drop a cookie, so each Success arm
	// begins a session transition the catalog suppresses and records -- the Login lock -- and the canvas stays as it
	// was. Every fact renders the compiled story (StoryCatalog), so a deleted story, a lost pin or a changed Mode
	// fails here.
	[Theory]
	[InlineData("Forget This Browser", "Forget this browser", "/Account/Manage/TwoFactorAuthentication?status=browser-forgotten")]
	[InlineData("Reset", "Reset authenticator key", "/Account/Manage/EnableAuthenticator?status=authenticator-reset")]
	[InlineData("Two-Factor Disabled", "Reset authenticator key", "/Account/Manage/EnableAuthenticator?status=authenticator-reset")]
	[InlineData("Disabled", "Disable 2FA", "/Account/Manage/TwoFactorAuthentication?status=two-factor-disabled")]
	void A_train_three_click_story_records_one_transition_to_the_fixture_hop(string name, string button, string nextUrl)
	{
		var (driver, driveClick) = SetupClickDriver();

		var story = name switch
		{
			"Forget This Browser" => StoryCatalog.RenderStory<TwoFactorAuthentication_stories>(this, name),
			"Disabled" => StoryCatalog.RenderStory<Disable2fa_stories>(this, name),
			_ => StoryCatalog.RenderStory<ResetAuthenticator_stories>(this, name),
		};

		AssertTheClickPath(driver);
		var before = story.Markup;
		ClickAsTheDriverDoes(story).TextContent.Trim().ShouldBe(button);
		story.Markup.ShouldBe(before);
		driveClick.SetResult(true);

		story.WaitForAssertion(() => DriverState(story).ShouldBe("complete"));
		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl.ShouldBe(nextUrl);
	}

	// Enable Authenticator / "Verified" -- FillAndSubmit with the code in the Email slot. The Success account still
	// holds recovery codes, so the fake answers the authenticator-verified hop, not the recovery-code handoff (that
	// hop is TwoFactorDisabled's, whose account holds none).
	[Fact]
	async Task The_enable_authenticator_verified_story_records_one_transition_to_the_verified_hop()
	{
		var (driver, drive) = SetupSubmitDriver();

		var story = StoryCatalog.RenderStory<EnableAuthenticator_stories>(this, "Verified");

		story.FindComponent<StoryDriver>().FindComponent<VerifyAuthenticatorForm>();
		await FillAndSubmitAsTheDriverDoes(story, driver, "123456");
		drive.SetResult(true);

		story.WaitForAssertion(() => DriverState(story).ShouldBe("complete"));
		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldHaveSingleItem().NextUrl
			.ShouldBe("/Account/Manage/TwoFactorAuthentication?status=authenticator-verified");
	}

	// A rename touches no sign-in, so success is an ordinary soft navigation -- the Forgot Password shape, with the
	// same live-iframe caveat (KNOWN-ISSUES.md), which is why the catalog stages no driven "Renamed" story and this
	// fact holds the state over the page itself.
	[Fact]
	async Task A_passkey_rename_records_one_soft_navigation_to_the_updated_list()
	{
		var page = Render<RenamePasskey>(parameters => parameters.Add(rename => rename.Id, "cred-one"));
		Fill(page, "Home desktop");

		await page.Find("form").SubmitAsync();

		SessionTransitions.Transitions.ShouldBeEmpty();
		var navigation = Navigation.History.ShouldHaveSingleItem();
		navigation.Options.ForceLoad.ShouldBeFalse();
		navigation.Uri.ShouldBe("/Account/Manage/Passkeys?status=passkey-updated");
	}

	// Generate Recovery Codes / "Generated" -- generation rotates no stamp: the ten codes render in place and the
	// story records nothing.
	[Fact]
	async Task The_generated_recovery_codes_story_renders_ten_codes_and_records_nothing()
	{
		var stock = await StockRecoveryCodesAsync();
		var (driver, driveClick) = SetupClickDriver();

		var story = StoryCatalog.RenderStory<GenerateRecoveryCodes_stories>(this, "Generated");

		AssertTheClickPath(driver);
		ClickAsTheDriverDoes(story).TextContent.Trim().ShouldBe("Generate Recovery Codes");
		driveClick.SetResult(true);

		story.WaitForAssertion(() => DriverState(story).ShouldBe("complete"));
		story.WaitForAssertion(() => RecoveryCodes(story).ShouldBe(stock));
		story.Markup.ShouldContain("You have generated new recovery codes.");
		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
	}

	// Recovery Codes Handoff / "Revealed" -- what the page renders on a successful reveal: the fake's ten codes under
	// the verified status. The canvas cannot reach the page's own reveal (the handoff rides the query, and the page
	// strips it by navigating), so the next two facts hold the page itself.
	[Fact]
	async Task The_revealed_recovery_codes_story_renders_ten_codes_and_records_nothing()
	{
		var stock = await StockRecoveryCodesAsync();
		var story = StoryCatalog.RenderStory<RecoveryCodesHandoff_stories>(this, "Revealed");

		RecoveryCodes(story).ShouldBe(stock);
		story.Markup.ShouldContain("Your authenticator app has been verified.");
		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
	}

	// The handoff hop the fake's Enable answers under TwoFactorDisabled, redeemed by the real page: ten codes, no
	// transition, and the page's own strip of the handoff as the only navigation after arrival.
	[Fact]
	async Task The_handoff_page_reveals_the_fakes_ten_codes_and_records_no_transition()
	{
		var stock = await StockRecoveryCodesAsync();
		var page = RenderHandoffPageWith(await HandoffHopAsync(), AccountScenario.Success);

		page.WaitForAssertion(() => RecoveryCodes(page).ShouldBe(stock));
		AssertOnlyTheHandoffStrip();
		SessionTransitions.Transitions.ShouldBeEmpty();
	}

	[Fact]
	async Task A_faulted_handoff_reveal_renders_no_codes_and_records_no_transition()
	{
		var page = RenderHandoffPageWith(await HandoffHopAsync(), AccountScenario.Fault);

		page.WaitForAssertion(() => page.Markup.ShouldContain("Your recovery codes could not be shown."));
		RecoveryCodes(page).ShouldBeEmpty();
		AssertOnlyTheHandoffStrip();
		SessionTransitions.Transitions.ShouldBeEmpty();
	}

	// Failures record nothing: each click story pinned to a refusal or a fault renders it in place.
	[Theory]
	[InlineData("Reset", "Fault", "Reset authenticator key", "Your authenticator key could not be reset.")]
	[InlineData("Disable", "Not Enabled", "Disable 2FA", "Two-factor authentication is not enabled.")]
	[InlineData("Disable", "Fault", "Disable 2FA", "Two-factor authentication could not be disabled.")]
	[InlineData("Generate", "Two-Factor Disabled", "Generate Recovery Codes",
		"Cannot generate recovery codes because two-factor authentication is not enabled.")]
	[InlineData("Generate", "Fault", "Generate Recovery Codes", "Your recovery codes could not be generated.")]
	void A_failed_train_three_click_story_renders_in_place_and_records_nothing(string surface, string name, string button,
		string expected)
	{
		var (driver, driveClick) = SetupClickDriver();

		var story = surface switch
		{
			"Reset" => StoryCatalog.RenderStory<ResetAuthenticator_stories>(this, name),
			"Disable" => StoryCatalog.RenderStory<Disable2fa_stories>(this, name),
			_ => StoryCatalog.RenderStory<GenerateRecoveryCodes_stories>(this, name),
		};

		AssertTheClickPath(driver);
		ClickAsTheDriverDoes(story).TextContent.Trim().ShouldBe(button);
		driveClick.SetResult(true);

		story.WaitForAssertion(() => DriverState(story).ShouldBe("complete"));
		story.WaitForAssertion(() => story.Markup.ShouldContain(expected));
		RecoveryCodes(story).ShouldBeEmpty();
		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("Enable", "Invalid Code", "123456", "Verification code is invalid.")]
	[InlineData("Rename", "Fault", "Home desktop", "0badc0de-0bad-c0de-0bad-c0de0badc0de")]
	async Task A_failed_train_three_submit_story_renders_in_place_and_records_nothing(
		string surface, string name, string fill, string expected)
	{
		var (driver, drive) = SetupSubmitDriver();

		var story = surface == "Enable" ?
			StoryCatalog.RenderStory<EnableAuthenticator_stories>(this, name) :
			StoryCatalog.RenderStory<RenamePasskey_stories>(this, name);

		await FillAndSubmitAsTheDriverDoes(story, driver, fill);
		drive.SetResult(true);

		story.WaitForAssertion(() => DriverState(story).ShouldBe("complete"));
		story.WaitForAssertion(() => story.Markup.ShouldContain(expected));
		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
	}

	// Login With Passkey / "Fault" -- no driver (the ceremony is WebAuthn), but the faulted options read answers the
	// click before any ceremony runs, so the pinned state is one click away and records nothing.
	[Fact]
	void The_login_with_passkey_fault_story_renders_the_fault_on_click_and_records_nothing()
	{
		var passkeys = JSInterop.SetupModule("./_content/Norse.AuthN.Components.FluentUI/passkeys.js");
		passkeys.Setup<bool>("isSupported").SetResult(true);

		var story = StoryCatalog.RenderStory<LoginWithPasskey_stories>(this, "Fault");

		story.WaitForAssertion(() => story.Find("fluent-button[data-action=passkey]").HasAttribute("disabled").ShouldBeFalse());
		story.Find("fluent-button[data-action=passkey]").Click();

		story.WaitForAssertion(() => story.Markup.ShouldContain("Passkey sign-in failed. Please try again."));
		passkeys.Invocations["request"].ShouldBeEmpty();
		Navigation.History.ShouldBeEmpty();
		SessionTransitions.Transitions.ShouldBeEmpty();
	}

	// Login / "External Providers" -- the provider link is the server's start URL verbatim, carrying the resolved
	// landing, and a plain top-level anchor, since only a full page load reaches the start endpoint.
	[Fact]
	void Login_external_providers_story_renders_the_server_shaped_start_link()
	{
		var story = StoryCatalog.RenderStory<Login_stories>(this, "External Providers");

		var start = story.Find("a[target=_top]");
		start.GetAttribute("href").ShouldBe("/Account/PerformExternalLogin?provider=Example&returnUrl=%2F");
		start.TextContent.Trim().ShouldBe("Sign in with Example provider");
	}

	// Every Login story renders the passkey link from the fake, in the server's shape.
	[Theory]
	[InlineData("Default")]
	[InlineData("Validation Errors")]
	[InlineData("Invalid Credentials")]
	[InlineData("Locked Out")]
	[InlineData("Not Allowed")]
	[InlineData("Two-Factor Required")]
	[InlineData("External Providers")]
	void Every_login_story_renders_the_passkey_link_from_the_fake(string name)
	{
		SetupSubmitDriver();

		var story = StoryCatalog.RenderStory<Login_stories>(this, name);

		story.Find("a[href^='/Account/LoginWithPasskey']").GetAttribute("href").ShouldBe("/Account/LoginWithPasskey?returnUrl=%2F");
	}

	// The driver module with driveClick pending until the fact settles it.
	(BunitJSModuleInterop Driver, JSRuntimeInvocationHandler<bool> DriveClick) SetupClickDriver()
	{
		var driver = JSInterop.SetupModule(StoryDriverModule);
		return (driver, driver.Setup<bool>("driveClick", _ => true));
	}

	// The driver module with drive pending until the fact settles it: driveClick must never be called.
	(BunitJSModuleInterop Driver, JSRuntimeInvocationHandler<bool> Drive) SetupSubmitDriver()
	{
		var driver = JSInterop.SetupModule(StoryDriverModule);
		return (driver, driver.Setup<bool>("drive", _ => true));
	}

	// A ClickOnly driver: driveClick invoked once, drive never.
	static void AssertTheClickPath(BunitJSModuleInterop driver)
	{
		driver.Invocations["driveClick"].ShouldHaveSingleItem();
		driver.Invocations["drive"].ShouldBeEmpty();
	}

	// Mirrors storyDriver.js's drive under FillAndSubmit: the one invocation must carry fill = true and the story's
	// Email value, which then goes into every text input, before the form is submitted.
	static async Task FillAndSubmitAsTheDriverDoes(IRenderedComponent<ContainerFragment> story, BunitJSModuleInterop driver, string email)
	{
		var invocation = driver.Invocations["drive"].ShouldHaveSingleItem();
		driver.Invocations["driveClick"].ShouldBeEmpty();
		invocation.Arguments[1].ShouldBe(true);
		invocation.Arguments[2].ShouldBe(email);
		story.WaitForAssertion(() => story.Find("[data-norse-story-driver-state] form"));
		foreach (var input in story.FindAll("[data-norse-story-driver-state] form fluent-text-input"))
			await input.ChangeAsync(new() { Value = email });
		await story.Find("[data-norse-story-driver-state] form").SubmitAsync();
	}

	// The fake's handoff hop: Enable under TwoFactorDisabled, whose account holds no codes.
	static async Task<string> HandoffHopAsync()
	{
		var outcome = await new FakeTwoFactorService(new Scenario<AccountScenario>(AccountScenario.TwoFactorDisabled))
			.EnableAuthenticator(new() { Code = "123456" }, Xunit.TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<NavigationResult> hop).ShouldBeTrue();
		return hop.Value.NextUrl;
	}

	IRenderedComponent<ScenarioScope<AccountScenario>> RenderHandoffPageWith(string hop, AccountScenario scenario)
	{
		Navigation.NavigateTo(hop);
		return Render<ScenarioScope<AccountScenario>>(parameters => parameters
			.Add(scope => scope.Value, scenario)
			.AddChildContent<RecoveryCodesHandoff>());
	}

	// The strip replaces the arrival's history entry, so it is the one entry left: the bare route, replaced.
	void AssertOnlyTheHandoffStrip()
	{
		var strip = Navigation.History.ShouldHaveSingleItem();
		strip.Uri.ShouldBe("Account/Manage/ShowRecoveryCodes");
		strip.Options.ReplaceHistoryEntry.ShouldBeTrue();
	}

	// The fake's stock batch of ten, as Generate answers it under Success.
	static async Task<string[]> StockRecoveryCodesAsync()
	{
		var outcome = await new FakeTwoFactorService(new Scenario<AccountScenario>(AccountScenario.Success))
			.GenerateRecoveryCodes(Xunit.TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<RecoveryCodesResponse> codes).ShouldBeTrue();
		codes.Value.RecoveryCodes.Length.ShouldBe(10);
		return codes.Value.RecoveryCodes;
	}

	static string[] RecoveryCodes<TComponent>(IRenderedComponent<TComponent> story)
		where TComponent : Microsoft.AspNetCore.Components.IComponent =>
		[.. story.FindAll("code.recovery-code").Select(code => code.TextContent)];

	const string StoryDriverModule = "./_content/Norse.DesignSystem.Stories/storyDriver.js";

	// Mirrors storyDriver.js's driveClick: the first button or fluent-button beneath the driver's wrapper.
	static AngleSharp.Dom.IElement ClickAsTheDriverDoes(IRenderedComponent<ContainerFragment> story)
	{
		var button = story.Find("[data-norse-story-driver-state]").QuerySelector("button, fluent-button").ShouldNotBeNull();
		button.Click();
		return button;
	}

	static string? DriverState(IRenderedComponent<ContainerFragment> story) =>
		story.Find("[data-norse-story-driver-state]").GetAttribute("data-norse-story-driver-state");

	// Profile / "Default" -- the page through ManageLayout, which nests the host's IAppShellLayout: the
	// catalog's StoryShellLayout must resolve, and the loader must reach the form through the fakes.
	[Fact]
	void The_manage_layout_renders_through_the_story_shell()
	{
		var story = Render<LayoutView>(parameters => parameters
			.Add(view => view.Layout, typeof(ManageLayout))
			.AddChildContent<Norse.AuthN.Components.FluentUI.Manage.Index>());

		story.Markup.ShouldContain("Manage your account");
		story.Markup.ShouldContain("designer@example.com");
	}

	// Mirrors storyDriver.js: fill every text input the story exposes, in order. The driver also skips
	// disabled inputs; callers with a read-only display field target the editable one directly.
	static void Fill<TComponent>(IRenderedComponent<TComponent> story, params string[] values)
		where TComponent : Microsoft.AspNetCore.Components.IComponent
	{
		var inputs = story.FindAll("fluent-text-input");
		for (var index = 0; index < values.Length; index++)
			inputs[index].Change(values[index]);
	}
}
