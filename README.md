# Bragi

> Bragi, skaldic god of poetry, keeper of every tale worth telling.

<p align="center">
  <img src="https://github.com/user-attachments/assets/7596c242-e424-4746-bb33-5e5151c8a0de" alt="Bragi — the skaldic god of poetry, master of eloquence, welcoming the honored dead into Valhalla with song" title="Bragi — the skald who sings of everything aboard the ship" />
</p>

*Image credit: [@norsemythologyclips](https://www.instagram.com/norsemythologyclips/) — go follow them.*

Bragi is the story: **`Norse.DesignSystem.Stories`** — a Razor Class Library of `.stories.razor` catalog pages and Markdown documentation (via `MD2RazorGenerator`) for the platform's Blazor components. Bragi doesn't build the ship; he sings of everything aboard it.

Bragi ships no runnable app of its own. Yggdrasil hosts the runnable BlazingStory catalog built from it (`Hosting.Stories.Client`/`.Server`), published as a container to `ghcr.io/norsearchitecture/hosting/stories` — the `BlazingStory` package reference here is purely the `.stories.razor` authoring API, a content-authoring dependency, not a hosting one.

## The catalog

Ten stories ship today:

- **Authentication** (nine, over Heimdall's `AuthN.Components.FluentUI`): [Login](src/DesignSystem.Stories/Authentication/Login.stories.razor), [Register](src/DesignSystem.Stories/Authentication/Register.stories.razor), [Lockout](src/DesignSystem.Stories/Authentication/Lockout.stories.razor), [AccessDenied](src/DesignSystem.Stories/Authentication/AccessDenied.stories.razor), [StatusMessage](src/DesignSystem.Stories/Authentication/StatusMessage.stories.razor), [ShowRecoveryCodes](src/DesignSystem.Stories/Authentication/ShowRecoveryCodes.stories.razor), [ForgotPasswordConfirmation](src/DesignSystem.Stories/Authentication/ForgotPasswordConfirmation.stories.razor), [ResetPasswordConfirmation](src/DesignSystem.Stories/Authentication/ResetPasswordConfirmation.stories.razor), and [InvalidPasswordReset](src/DesignSystem.Stories/Authentication/InvalidPasswordReset.stories.razor)
- **Primitives** (one, over Asgard's `Abstractions.Components`): [Loader](src/DesignSystem.Stories/Primitives/Loader.stories.razor)

`Reference.Components.FluentUI` (Mímir) stories remain future work once that realm's components ship.

## How the stories fake a running backend

If you're a designer landing here, this is the part that won't be obvious, and it's the part that makes the catalog worth your time.

**The components in the catalog are the real production components.** Not mockups, not copies kept in sync by hand — the same Login and Register that ship, with the same markup, the same validation rules, the same error rendering. When one of them changes in its home repo, the catalog changes with it.

**But nothing real is running behind them.** In production, submitting Login calls an actual authentication service and actually signs someone in. The catalog has no servers, no database, no accounts — it's a static site running entirely in your browser. So how does an interactive Login story work at all?

The trick is that these components never talk to a service directly — they talk to an *interface*, a contract that says "something that can log people in stands here." At startup, the app decides what that something is. The production app plugs in the real service. The catalog plugs in [`FakeAuthenticationService`](src/DesignSystem.Stories/Authentication/FakeAuthenticationService.cs) — a small scripted stand-in that answers instantly, in-browser, with responses shaped exactly like the real service's. The component can't tell the difference, and that's the point: **the actors on stage are real; only the world backstage is scripted.**

What that buys you: every state a component can be in becomes triggerable on demand, with zero setup. Type `fail@example.com` into the Login story's own email field and the real component renders the real invalid-credentials failure, drawn exactly as production would draw it — no test account, no server, no engineer on call. You're styling the true component in the true state, with none of the machinery.

Today one sentinel exists (that email address). The intent is a full inventory — a trigger for every failure the real service can produce, plus a catalog page listing them so the triggers live where you already are — and that's designed work, deliberately pending. The plumbing itself stays out of sight: the host wires the fakes with a single call ([`AddNorseStoryFakes()`](src/DesignSystem.Stories/Authentication/ServiceCollectionExtensions.cs)) and knows nothing else about them.

## Build and test

```shell
dotnet build Bragi.slnx   # warnings are errors — a single warning fails
dotnet test Bragi.slnx    # xUnit v3 + Shouldly on Microsoft.Testing.Platform
```

Requires the .NET 11 preview SDK pinned by `global.json`. The realm builds standalone — it is its own clone target, not only a Bifröst submodule.

## Status

Split out of [Naglfar](https://github.com/NorseArchitecture/Naglfar) on 2026-07-12, the same day `DesignSystem.Stories` first landed there as a plain RCL — see the [hosting design](https://github.com/NorseArchitecture/Glitnir/blob/master/docs/Platform/specs/2026-07-12-designsystem-stories-hosting-design.md) (superseded in part) for the original decision and its addendum recording the split. Naglfar keeps the token pipeline; Bragi owns the story content — two different publish cadences (npm vs. NuGet) that don't belong wearing one repo's clothes.

## The cosmos

Bragi rides as a submodule of [Bifröst](https://github.com/NorseArchitecture/Bifrost), the Norse Architecture's meta-repository, alongside every other realm. Story content here — stories, story authoring, what a component's catalog page shows — is exempt from the platform's brainstorm → spec → plan → TDD cycle, the same standing call as Naglfar. The one behavioral exception is the fake behind the stories: when its design session runs, that piece rides the full discipline like any other realm's code.

## Soundtrack: Bragi | God of Poetry and Sacred Speech | Norse Song
[![Soundtrack: Bragi | God of Poetry and Sacred Speech | Norse Song](https://img.youtube.com/vi/HfHsOQ1lagE/maxresdefault.jpg)](https://www.youtube.com/watch?v=HfHsOQ1lagE)
