# Known Issues

## BlazingStory canvas boots itself nested inside itself on a driven story

**Status:** **Partially closed 2026-08-11.** Root cause was re-diagnosed the same day (see below) and both original mechanisms were fixed upstream. The hazard that survived both fixes is closed for Login and Logout, confirmed by a real browser run. **It is not closed for Register — a third mechanism, distinct from the first two, reproduces the identical symptom via Register's own (correct, non-forced) soft navigation. Deferred, not fixed. See "Register: still open" below.**

**First observed:** 2026-08-08, while smoke-testing the story catalog after the `storyDriver.js` scenario-fake pattern shipped the same day.

### Symptom

Navigate to a `StoryDriver`-driven story (`Authentication/Login → Locked Out`, `Authentication/Register → Validation Errors`, `Authentication/Register → Invalid Password`). Instead of the isolated component preview, the canvas shows a **second, fully-booted instance of the entire BlazingStory catalog shell** — its own logo, its own sidebar, landing on the "Scenarios" custom page — nested inside the preview pane.

### Actual root cause

`FakeAuthenticationService` returns `NextUrl = "/"` on its `Success` branch, and both auth forms navigate that result with `forceLoad: true`:

```csharp
result => Navigation.NavigateTo(result.NextUrl, forceLoad: true)
```

`forceLoad: true` is a **real document load**. Performed inside BlazingStory's canvas iframe, it navigates that iframe to the app root — which boots the whole catalog inside the preview pane. That is the nested doll. There is no third-party bug involved.

So the question was only ever: *how did a driven story reach the `Success` branch?* Three ways, all real:

**1. The shadowed validation call (`Register → Validation Errors`).** That story pins no scenario at all — it runs on the ambient default, which is `Success`. Its only protection was Register's own guard:

```razor
if (!await editContext.ValidateAsync())   // extension style
    return;
```

.NET 11 added an instance `EditContext.ValidateAsync(CancellationToken)`. C# binds instance methods ahead of extension methods, so this silently retargeted away from Blazilla's `EditContextExtensions.ValidateAsync` and returned `true` for everything. The empty form sailed through to the fake, got `Success`, and force-navigated. Verified by inspection of the shipped artifact: this line was live in Heimdall's committed history from `cbfbd91` until `7cc696f`, i.e. in **every published package up to and including v0.0.14**.

**2. Scenario pin loss (`Login → Locked Out`, `Register → Invalid Password`).** These stories do pin a failing scenario, and a failing scenario returns `Failed` and never navigates. But before the single-slot pin ownership fix (PR #24), a disposing predecessor scope could reset the slot — and the value it reset to is the constructor initial value, which is `Success`. A story running unpinned gets `Success`, gets `NextUrl = "/"`, and force-navigates. **This is what produced the re-entry correlation**: dispose ordering only bites when you navigate between stories, which is exactly the condition the original entry attributed to iframe pooling.

**3. Document-global driver selection (`Register → Default` to `Register → Validation Errors`).** Before Bragi v0.0.9, the incoming driver's `document.querySelector("form")` could select the departing Default story's form during a persistent-canvas transition. The driver submitted that stale form instead of its own Validation Errors form; the departing Blazor handler ran under ambient `Success`, received `NextUrl = "/"`, and followed the same `forceLoad: true` path. Bragi v0.0.9 passed the rendered StoryDriver wrapper into JavaScript and changed discovery to wrapper-scoped `root.querySelector('form')`, so an incoming driver cannot submit a departing sibling story's form.

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
- **Mechanism 3 is closed** by Bragi v0.0.9's wrapper-scoped form discovery. Its real-module two-form regression mounts an external stale form beside the incoming wrapper and proves only the wrapper-owned form is submitted.
- **All three are now locked by tests.** `DrivenStoryNavigationTests` renders each driven story's real composition and asserts `BunitNavigationManager` recorded no navigation. `BunitNavigationManager` captures `NavigateTo` instead of performing it, which turns the force-load ignition into an ordinary unit assertion; the StoryDriver module regression independently locks the form-selection boundary.
- The `storyDriver.js` capture-phase `preventDefault()` fix (a genuinely separate native-submit race, described in the original entry) stays. It was real and independent of everything above.

### The hazard that survived both fixes — closed for Login/Logout, open for Register (2026-08-11)

`Success` was simultaneously the value a released pin restores **and** the only scenario that performed a destructive navigation. So the failure mode of "this story lost its pin" was not a wrong render — it was the nested doll, again. Both mechanisms above were different routes to that same single point of ignition.

**The durable fix landed for principal transitions:** `RecordingSessionTransition` (`Norse.DesignSystem.Stories`) is the catalog's `ISessionTransition` — suppress-and-record instead of a real forced document load — registered by `AddNorseStoryFakes()` alongside the fake's own `Logout` arm. Login and Logout route their success continuation through the seam, and `RecordingSessionTransition.Begin()` never calls `NavigationManager` at all — it appends to a list and returns. There is no code path left on those two that could reach real navigation, forced or soft. Confirmed by browser run: see below. Full design: `../Glitnir/docs/Asgard/specs/2026-08-11-session-transition-seam-design.md`.

**Register was deliberately left outside the seam** — its handler signs nobody in, so no principal transition occurs, and `ISessionTransition` (a contract for "the principal changed") doesn't fit. That reasoning holds for a real deployment. It does not hold inside the story catalog — see "Register: still open" below.

The characterization test is inverted, exactly as its own comment promised: `DrivenStoryNavigationTests.An_unpinned_driven_story_force_navigates_which_is_what_boots_the_catalog_nested` is gone, replaced by `An_unpinned_driven_login_story_begins_a_session_transition_the_catalog_suppresses` (plus siblings for Register's soft-nav and a confirmed Logout) — each asserting `SessionTransitions.Transitions` recorded what the suppressed seam saw, and `Navigation.History` stayed empty. These are `bUnit` assertions against `BunitNavigationManager`, which captures `NavigateTo` instead of performing it — accurate for what they test, but see below for what they cannot.

### Register: still open — a third mechanism (2026-08-11)

**Reproduced live, twice, deliberately.** `Register → Default` (ambient `Success`), submitted with a fresh email inside a real `Hosting.Stories.Server`/`Hosting.Stories.Client` pair: within ~500ms the story iframe's own `contentWindow.location.href` becomes the catalog's root path, and a second, fully-booted `Blazing Story` shell renders inside the preview pane, landing on "Scenarios" — the identical symptom this entire entry is about.

**Why this isn't the same bug, and why the seam doesn't cover it:** Register's continuation is `Navigation.NavigateTo(result.NextUrl)` — no `forceLoad`, an honest soft navigation. In a real host, that is completely correct: it takes the user to the app root. Inside BlazingStory, each story preview iframe is a **live instance of the same WASM app** (`Hosting.Stories.Client`), scoped to story-preview mode only via the `viewMode=story&id=...` query on `iframe.html`. That scoping holds on the initial render. It does not survive a genuine client-side `NavigateTo` from a component inside it: the iframe's own router resolves the target path against its own route table, which is the catalog's own routes — landing on the catalog root, not anywhere Register intended.

**Why the earlier "zero events" verification missed it:** the Investigation trail method below (and the automated sweep run at this gate's close) hooks `beforeunload`/`pagehide`/`submit`/`requestSubmit` — the signatures of a **forced** document reload. A `pushState`-based client-side route change inside a live SPA never fires any of those events; that is the entire point of a soft navigation. The method is sound for what it was built to catch (mechanisms 1 and 2, both forced reloads) and structurally incapable of catching this one. The original browser-confirmation pass for this entry claimed Register's path was clean on exactly this basis — that claim was wrong, and has been removed below; the corrected findings replace it.

**Not fixed.** Candidate approaches considered but not attempted: routing Register through `ISessionTransition` (rejected — semantically wrong, no principal transition occurs); a catalog-scoped `NavigationManager` wrapper that suppresses real navigation the way `RecordingSessionTransition` does for the seam (plausible, but risks intercepting BlazingStory's own in-iframe toolbar/routing state, which rides the same `NavigationManager` — needs real design, not a quick patch). Left open deliberately rather than rushed.

### Browser confirmation (2026-08-11)

Ran the Investigation trail method below for real, against a freshly built `Hosting.Stories.Server`/`Hosting.Stories.Client` pair, hooking `beforeunload`/`pagehide`/`submit`/`requestSubmit` in the top frame **and** every iframe via `addInitScript`:

- **Cold loads, all eight driven stories** (`Login → Locked Out/Invalid Credentials/Validation Errors/Not Allowed`, `Register → Validation Errors/Email Taken/Invalid Password`, `Logout → Sign-out Failed`) — zero `beforeunload`/`pagehide`/`submit`/`requestSubmit` events, in either frame. Every story iframe's `contentWindow.location.href` stayed on its own `viewMode=story&id=...` URL — no nested catalog boot on cold load, for any story, including `Register → Default`.
- **Sibling re-entry** (`Login → Locked Out` → `Login → Invalid Credentials` → back to `Locked Out`, the exact sequence mechanism 2 needed) — zero events.
- **`Logout → Sign-out Failed`** renders its alert: `"Sign-out failed — you are still signed in."` — confirmed by reading the iframe's DOM directly.
- **Register's Success path is NOT clean** — see "Register: still open" above. A step-by-step reproduction (cold load → confirm clean → fill and submit → sample the iframe's DOM every 500ms) shows the nested shell present within one sample (~500ms) of the click. The absence of `beforeunload`/`pagehide` events during this same window is real and expected — it is not evidence of safety, because this failure mode doesn't produce those events in the first place.

One loose end from the original evidence — a cold, direct load "reliably worked" even before mechanisms 1 and 2 were fixed — is resolved for those two mechanisms (neither reproduces on cold load post-fix) and is consistent with the newly found mechanism 3, which likewise never fires on cold load — only after a real submit.

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
