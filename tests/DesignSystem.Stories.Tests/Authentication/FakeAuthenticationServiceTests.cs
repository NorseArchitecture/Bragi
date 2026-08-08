using Norse.Abstractions.Contracts;
using Norse.AuthN.Services;
using Norse.DesignSystem.Stories.Authentication;
using Norse.Primitives;

namespace Norse.DesignSystem.Stories.Tests.Authentication;

public sealed class FakeAuthenticationServiceTests
{
	[Fact]
	async Task Login_succeeds_with_the_root_next_url_for_any_ordinary_email()
	{
		FakeAuthenticationService fake = new();
		var outcome = await fake.Login(new LoginRequest { Email = "designer@example.com", Password = "irrelevant" }, TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<LoginResult> success).ShouldBeTrue();
		success.Value.NextUrl.ShouldBe("/");
	}

	[Fact]
	async Task Login_fails_with_invalid_credentials_when_the_sentinel_email_is_typed()
	{
		// Upper-cased deliberately — the sentinel comparison is OrdinalIgnoreCase, and a designer
		// typing FAIL@example.com should get the same error state as fail@example.com.
		FakeAuthenticationService fake = new();
		var outcome = await fake.Login(new LoginRequest { Email = "FAIL@example.com", Password = "irrelevant" }, TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Failed failed).ShouldBeTrue();
		failed.Problem.Category.ShouldBe(ErrorCategory.InvalidCredentials);
	}

	[Fact]
	async Task Register_always_succeeds()
	{
		FakeAuthenticationService fake = new();
		var outcome = await fake.Register(new RegisterRequest { Email = "designer@example.com", Password = "irrelevant" }, TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<RegisterResult> success).ShouldBeTrue();
		success.Value.Succeeded.ShouldBeTrue();
	}

	[Fact]
	async Task EmailExists_always_reports_not_taken()
	{
		FakeAuthenticationService fake = new();
		var outcome = await fake.EmailExists(new EmailExistsRequest { Email = "anyone@example.com" }, TestContext.Current.CancellationToken);
		outcome.TryGetValue(out Success<BoolResponse> success).ShouldBeTrue();
		success.Value.Value.ShouldBeFalse();
	}

	[Fact]
	async Task Logout_throws_because_a_non_visual_component_never_earns_a_story()
	{
		FakeAuthenticationService fake = new();
		await Should.ThrowAsync<NotImplementedException>(() => fake.Logout(TestContext.Current.CancellationToken));
	}
}
