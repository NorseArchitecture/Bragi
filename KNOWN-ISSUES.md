# Known Issues

## BlazingStory canvas boots itself nested inside itself on a driven story

**Status:** Root cause **re-diagnosed 2026-08-10**. The original entry blamed a third-party package; that was wrong. The cause was ours, in three independently reproduced mechanisms, all now closed. **Browser-confirmed closed 2026-08-10** against Asgard v0.0.26 / Heimdall v0.0.15 / Bragi v0.0.10. The separate architectural hazard described below remains live.

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
- Bragi's source `PackageReference` now pins BlazingStory `1.0.0-preview.91` exactly, preventing future package builds from drifting beyond the runtime version audited by this investigation.
- The `storyDriver.js` capture-phase `preventDefault()` fix (a genuinely separate native-submit race, described in the original entry) stays. It was real and independent of everything above.

### Browser closure evidence

The Yggdrasil Chromium smoke ran twice independently, without retries, against the released package graph (`UseProjectReferences=false`): Asgard v0.0.26, Heimdall v0.0.15, Bragi v0.0.10, and BlazingStory 1.0.0-preview.91. Both runs passed in 44–45 seconds with the same observations:

- A **cold direct load** of `Authentication/Register → Validation Errors` submitted the empty form, rendered the password-required and minimum-length messages, and stayed at `iframe.html?viewMode=story&id=authentication-register--validation-errors`. It produced exactly the expected outer-plus-canvas WebAssembly bootstraps and no nested catalog bootstrap. This proves mechanism 1 is closed in the current released packages.
- The lifecycle probe does not register on transient `about:blank` bootstrap documents; every app-document observation carries explicit top/child provenance. It observed zero `beforeunload` or `pagehide` events during cold validation, pin re-entry, and the full sweep.
- The pin re-entry repro drove `Authentication/Login → Locked Out`, visited `Not Allowed`, and returned to `Locked Out`. The locked-out state rendered again with zero new WebAssembly bootstraps and zero lifecycle events.
- The dynamically discovered sweep crossed the former Default-to-Validation-Errors transition, rendered all 25 states (15 Authentication, 5 Primitives, 5 Reference), completed 11 driver-backed states, stayed at a maximum of two live frames and two total WebAssembly bootstraps, and observed no lifecycle event or recursive catalog document.

The reproduced issue is therefore closed. This evidence does **not** claim that `forceLoad: true` is wrong in the real Login/Register flows, nor that the catalog has been made safe against every future pinning gap; it proves that the three released fixes keep the known driven stories away from that destructive success path.

The browser run does **not** resolve the historical contradiction in the pre-fix evidence that said a cold direct load "reliably works". No pre-fix package was rerun, so that observation remains unresolved and may have been confounded with the native-submit race that changed in the same period. Current-package GREEN evidence cannot explain pre-fix behavior retroactively.

### The hazard that survives all three fixes

`Success` is simultaneously the value a released pin restores **and** the only scenario that performs a destructive navigation. So the failure mode of "this story lost its pin" is not a wrong render — it is the nested doll, again. All three mechanisms above were different routes to that same single point of ignition.

The durable fix is to make the catalog's navigation inert, so that a pinning gap degrades to a boring wrong-render instead of re-booting the catalog. `DrivenStoryNavigationTests.An_unpinned_driven_story_force_navigates_which_is_what_boots_the_catalog_nested` is a characterization test pinning this behavior deliberately — it documents the live hazard rather than endorsing it, and should be inverted if and when the navigation is neutered.

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
