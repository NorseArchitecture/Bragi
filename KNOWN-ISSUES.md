# Known Issues

## BlazingStory canvas boots itself nested inside itself on a driven story

**Status:** **Closed 2026-08-11.** Root cause was re-diagnosed the same day (see below) and both mechanisms that produced it were fixed upstream. The hazard that survived both fixes — described further down — is now also closed: the catalog's navigation is inert. Confirmed by a real browser run against the built catalog; see "Browser confirmation" at the bottom of this entry.

**First observed:** 2026-08-08, while smoke-testing the story catalog after the `storyDriver.js` scenario-fake pattern shipped the same day.

### Symptom

Navigate to a `StoryDriver`-driven story (`Authentication/Login → Locked Out`, `Authentication/Register → Validation Errors`, `Authentication/Register → Invalid Password`). Instead of the isolated component preview, the canvas shows a **second, fully-booted instance of the entire BlazingStory catalog shell** — its own logo, its own sidebar, landing on the "Scenarios" custom page — nested inside the preview pane.

### Actual root cause

`FakeAuthenticationService` returns `NextUrl = "/"` on its `Success` branch, and both auth forms navigate that result with `forceLoad: true`:

```csharp
result => Navigation.NavigateTo(result.NextUrl, forceLoad: true)
```

`forceLoad: true` is a **real document load**. Performed inside BlazingStory's canvas iframe, it navigates that iframe to the app root — which boots the whole catalog inside the preview pane. That is the nested doll. There is no third-party bug involved.

So the question was only ever: *how did a driven story reach the `Success` branch?* Two ways, both real:

**1. The shadowed validation call (`Register → Validation Errors`).** That story pins no scenario at all — it runs on the ambient default, which is `Success`. Its only protection was Register's own guard:

```razor
if (!await editContext.ValidateAsync())   // extension style
    return;
```

.NET 11 added an instance `EditContext.ValidateAsync(CancellationToken)`. C# binds instance methods ahead of extension methods, so this silently retargeted away from Blazilla's `EditContextExtensions.ValidateAsync` and returned `true` for everything. The empty form sailed through to the fake, got `Success`, and force-navigated. Verified by inspection of the shipped artifact: this line was live in Heimdall's committed history from `cbfbd91` until `7cc696f`, i.e. in **every published package up to and including v0.0.14**.

**2. Scenario pin loss (`Login → Locked Out`, `Register → Invalid Password`).** These stories do pin a failing scenario, and a failing scenario returns `Failed` and never navigates. But before the single-slot pin ownership fix (PR #24), a disposing predecessor scope could reset the slot — and the value it reset to is the constructor initial value, which is `Success`. A story running unpinned gets `Success`, gets `NextUrl = "/"`, and force-navigates. **This is what produced the re-entry correlation**: dispose ordering only bites when you navigate between stories, which is exactly the condition the original entry attributed to iframe pooling.

### Why the original diagnosis was wrong

The original entry decompiled `BlazingStory.Internals.Components.Layouts.PooledIFrame` and built a theory around its iframe pool calling `blazor.navigateTo()` on a recycled instance. That investigation was real and the code it describes exists, but it was pointed at the wrong suspect, because it explicitly cleared our own code on this basis:

> `Register.razor`'s `HandleRegisterAsync` calls `await editContext.ValidateAsync()` and returns early on failure, before ever reaching `SubmitAsync`. There is no code path in our own components that would call `NavigateTo` for an invalid or rejected submission.

That guard is mechanism 1. The exoneration rested on the bug.

The instrumented evidence the original entry collected actually fits `NavigateTo("/", forceLoad: true)` better than it fits the pool:

| Recorded observation | Explanation |
|---|---|
| genuine `beforeunload`/`pagehide` ~40ms after `requestSubmit` | a forced document load, which is what `forceLoad: true` performs |
| end URL is bare `https://<host>/`, no query string | `NextUrl = "/"` exactly |
| then `/?path=/custom/scenarios` | the catalog booting at `/` and its router falling to its default page |

### What is now in place

- **Mechanism 1 is closed twice over.** Asgard's `OutcomeFormComponentBase.SubmitAsync` now owns the validation gate for every form on the platform, calling Blazilla in static-invocation style so the shadowing cannot recur, and refusing to dispatch a form that has no `FormValidator` attached (a validator-less form validates to `true` with zero messages — indistinguishable from a valid one after the fact). Heimdall's hand-rolled guard is deleted; page authors no longer write validation logic at all. Asgard v0.0.26 / Heimdall v0.0.15 / Mímir v0.0.6.
- **Mechanism 2 is closed** by single-slot pin ownership with reference-identity release (PR #24) — a superseded scope's disposal is a no-op, so a predecessor can no longer reset a live successor's pin.
- **Both are now locked by tests.** `DrivenStoryNavigationTests` renders each driven story's real composition and asserts `BunitNavigationManager` recorded no navigation. `BunitNavigationManager` captures `NavigateTo` instead of performing it, which turns this browser-only symptom into an ordinary unit assertion.
- The `storyDriver.js` capture-phase `preventDefault()` fix (a genuinely separate native-submit race, described in the original entry) stays. It was real and independent of everything above.

### The hazard that survived both fixes — now closed (2026-08-11)

`Success` was simultaneously the value a released pin restores **and** the only scenario that performed a destructive navigation. So the failure mode of "this story lost its pin" was not a wrong render — it was the nested doll, again. Both mechanisms above were different routes to that same single point of ignition.

**The durable fix landed:** the catalog's navigation is now inert. `RecordingSessionTransition` (`Norse.DesignSystem.Stories`) is the catalog's `ISessionTransition` — suppress-and-record instead of a real forced document load — registered by `AddNorseStoryFakes()` alongside the fake's own `Logout` arm. Login and Logout route their success continuation through the seam; Register never did (its handler signs nobody in, so it was always an ordinary soft `NavigateTo`, never a forced reload). A pinning gap now degrades to a boring wrong-render — a recorded `Begin` call nobody acts on, or a soft nav to a route the story sandbox doesn't mount — never a document load, never the nested doll. Full design: `../Glitnir/docs/Asgard/specs/2026-08-11-session-transition-seam-design.md`.

The characterization test is inverted, exactly as its own comment promised: `DrivenStoryNavigationTests.An_unpinned_driven_story_force_navigates_which_is_what_boots_the_catalog_nested` is gone, replaced by `An_unpinned_driven_login_story_begins_a_session_transition_the_catalog_suppresses` (plus siblings for Register's soft-nav and a confirmed Logout) — each asserting `SessionTransitions.Transitions` recorded what the suppressed seam saw, and `Navigation.History` stayed empty.

### Browser confirmation (2026-08-11)

Ran the Investigation trail method below for real, against a freshly built `Hosting.Stories.Server`/`Hosting.Stories.Client` pair, hooking `beforeunload`/`pagehide`/`submit`/`requestSubmit` in the top frame **and** every iframe via `addInitScript`:

- **Cold loads, all eight driven stories** (`Login → Locked Out/Invalid Credentials/Validation Errors/Not Allowed`, `Register → Validation Errors/Email Taken/Invalid Password`, `Logout → Sign-out Failed`) — zero `beforeunload`/`pagehide`/`submit`/`requestSubmit` events, in either frame. Every story iframe's `contentWindow.location.href` stayed on its own `viewMode=story&id=...` URL — no nested catalog boot, anywhere.
- **Sibling re-entry** (`Login → Locked Out` → `Login → Invalid Credentials` → back to `Locked Out`, the exact sequence mechanism 2 needed) — zero events.
- **`Logout → Sign-out Failed`** renders its alert: `"Sign-out failed — you are still signed in."` — confirmed by reading the iframe's DOM directly.
- **Register's Success path** (`Register → Default`, ambient `Success`, submitted with a fresh email) — zero events, a JS marker set before submit survived after it (proof no document anywhere reloaded), top-level URL never left the story path. The soft-nav-to-nowhere is exactly the "boring wrong-render" the durable fix promised, not a forced reload.

One loose end from the original evidence — a cold, direct load "reliably worked" even before either mechanism was fixed — is no longer worth chasing: both mechanisms are gone, and the browser run above found no residual forced-navigation behavior under any tested condition, cold or warm.

### Investigation trail, for anyone re-opening this

Full instrumented repro method: using Playwright, monkey-patch `HTMLFormElement.prototype.requestSubmit`, and hook `submit` (capture), `beforeunload`, and `pagehide` via `page.addInitScript` (applies to the top frame and all iframes). Drive the SPA: load a driven story cold, click to a sibling story, click back to the original driven story. Compare `beforeunload`/`pagehide` firing (or not) and the resulting `iframe.contentDocument.location.href` against `viewMode=story&id=...` to detect the nested-catalog state programmatically.

To re-decompile BlazingStory should it ever genuinely become a suspect again:

```bash
ilspycmd -l c <path-to-BlazingStory.dll>
ilspycmd -t "BlazingStory.Internals.Components.Layouts.PooledIFrame" <path-to-BlazingStory.dll>
```

(`ilspycmd` is installed as a global .NET tool in this dev environment.) The pool's JS is not in the DLL — it is a plain file under the package's `staticwebassets/` folder.

### Ruled out, do not re-attempt

**`data-enhance="false"` on the `EditForm`** (theory: Blazor's enhanced navigation was intercepting the submit). `Hosting.Stories.Client` uses `WebAssemblyHostBuilder.CreateDefault()`, which ships `blazor.webassembly.js`. Enhanced navigation and the `data-enhance` opt-out are exclusively `blazor.web.js` features and do not exist in this hosting model. The attribute rendered correctly but was inert; 10/10 stress-test repro still failed identically.
