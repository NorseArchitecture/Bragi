# Known Issues

## BlazingStory canvas boots itself nested inside itself on re-entering a driven story

**Status:** Open, root cause confirmed, no fix landed. Root cause lives in the third-party `BlazingStory` package, not in this repo's code — read to the end before re-investigating from scratch.

**First observed:** 2026-08-08, while smoke-testing the story catalog after the `storyDriver.js` scenario-fake pattern shipped the same day.

**Package version implicated:** `BlazingStory` `1.0.0-preview.91` (floating via `Version="1.*-*"` in `src/DesignSystem.Stories/DesignSystem.Stories.csproj`). Check whether a newer preview has touched `PooledIFrame.razor.js` before re-investigating — this may simply get fixed out from under us by an upstream release.

### Symptom

Navigate to a `StoryDriver`-driven story (any story wrapped in `<StoryDriver>` — e.g. `Authentication/Login → Locked Out`, `Authentication/Register → Validation Errors`, `Authentication/Register → Invalid Password`), away from it to a sibling story, then back to the original driven story. Instead of re-rendering the target story, the canvas shows a **second, fully-booted instance of the entire BlazingStory catalog shell** — its own logo, its own sidebar, landing on the "Scenarios" custom page — nested inside the canvas area that should hold only the isolated component preview. A screenshot of this exact state is preserved in the conversation that produced this document (session transcript, 2026-08-08/09).

A **cold, direct load** of a driven story (e.g. pasting its URL fresh, or a hard browser refresh) reliably works. The bug is specific to **re-entering** a driven story within an already-booted iframe — i.e. it depends on the iframe-pooling optimization described below, not on the story itself.

### Root cause (confirmed via decompilation, not guessed)

`BlazingStory`'s `PreviewFrame` component renders the canvas's `<iframe>` through a component literally named `PooledIFrame` (`BlazingStory.Internals.Components.Layouts.PooledIFrame`), backed by a JS module shipped as a **static web asset inside the NuGet package** (not embedded in the DLL, not something we author or build):

```
~/.nuget/packages/blazingstory/1.0.0-preview.91/staticwebassets/Internals/Components/Layouts/PooledIFrame.razor.js
```

That module maintains a pool of up to 5 already-booted `<iframe>` elements (`maxIframesInPool = 5`, 1-minute TTL) and **reuses** them across story navigations instead of always creating a fresh iframe. The relevant function:

```js
export const navigate = async (containerOrIFrame, url, baseUri) => {
    const iframe = (containerOrIFrame.tagName !== "IFRAME" ? containerOrIFrame.querySelector("iframe") : containerOrIFrame);
    if (iframe === null)
        throw new Error("iframe not found");
    const { contentWindow, blazor } = await waitForIFrameReady(iframe);
    if (!contentWindow.location.href.startsWith(baseUri)) {
        contentWindow.location.href = url;
    } else {
        const navigateUrl = url.startsWith(baseUri) ? ("./" + url.substring(baseUri.length)) : url;
        blazor.navigateTo(navigateUrl);   // <-- fires on "return to a previously-visited story"
    }
};
```

When the target iframe is already on the same origin (the common case for "navigate back to a story you've already visited"), it does **not** do a real page load — it calls `window.Blazor.navigateTo(...)` directly on the **already-running WASM instance inside the pooled iframe**, asking Blazor's own client-side router to switch routes in place, without a document reload.

`Hosting.Stories.Client`'s `App.razor` composes `<BlazingStoryApp Assemblies="[...]" />`, which relies on BlazingStory's **own custom router** (`BlazingStory.Internals.Components.Router.IdQueryRouter` / `PathQueryRouter`) that hand-parses `viewMode=`/`id=`/`path=` **query string** parameters rather than using conventional Blazor `@page` route templates. Something about how `blazor.navigateTo()` — Blazor's own low-level, standard-route-template-oriented navigation primitive — interacts with that custom query-string router, on an iframe instance being recycled from the pool, causes the already-booted instance to fall back to its default/root route (`/?path=/custom/scenarios`) instead of picking up the new story id. That fallback renders the **full catalog shell** (sidebar, logo, Scenarios page) inside what should be an isolated single-component canvas — the "nested doll" appearance.

This is a plausible but **not fully proven** account of the exact failure inside `blazor.navigateTo()`/the custom router; what *is* proven, via direct instrumentation, is:
- The failure correlates with iframe **reuse** (pool hit), never with a cold/fresh iframe.
- `StoryDriver`'s `form.requestSubmit()` call and a genuine browser-level `beforeunload`/`pagehide` pair fire within ~40ms of each other on the broken runs — i.e. a real navigation occurs, not just a virtual DOM update.
- The end URL is bare `https://<host>/` (or `/?path=/custom/scenarios`) — no `iframe.html`, no query string at all.

### What was tried, and ruled out

Investigated and fixed as part of this same pass (see git history around 2026-08-08/09 for the actual diffs — the two fixes below are real, independent, and worth keeping regardless of this open issue):

1. **`storyDriver.js` native-submit race** (separate, real bug, now fixed) — `form.requestSubmit()` could occasionally race the browser's own default form-submission action before any Blazor listener attached, especially on a genuinely cold boot. Fixed by registering a synchronous capture-phase `submit` listener that unconditionally calls `event.preventDefault()` immediately before calling `requestSubmit()` — this is race-free by construction (JS is single-threaded; the listener is registered before dispatch) and does not stop propagation, so Blazor's own handling still runs normally. **This fix is real and should stay** — it eliminated a distinct, reproducible failure mode (confirmed via 24 stress-test iterations, 0 failures post-fix, versus reliable failure pre-fix). It does **not**, by itself, fix the re-entry/pooling issue documented here — that was discovered afterward, as a second, independent mechanism producing the same visible symptom.

2. **`data-enhance="false"` on `Login.razor`/`Register.razor`'s `EditForm`** (theory: Blazor's "enhanced navigation" was intercepting the submit) — **wrong theory, reverted.** `Hosting.Stories.Client` uses classic `WebAssemblyHostBuilder.CreateDefault()`, which ships `blazor.webassembly.js`. Enhanced navigation (and the `data-enhance` opt-out) is exclusively a `blazor.web.js` (Blazor Web App hybrid-hosting) feature and does not exist in this hosting model at all. The attribute rendered onto the DOM correctly but was inert; 10/10 stress-test repro still failed identically with it in place. Do not re-attempt this angle.

3. **Our own C# submit-gating logic** — read end to end, confirmed correct. `OutcomeFormComponentBase.SubmitAsync` (`Heimdall/src/AuthN.Components/OutcomeFormComponentBase.cs`) only invokes the success continuation (which calls `NavigationManager.NavigateTo(..., forceLoad: true)`) on `Success<T>`, never on `Failed`. `Register.razor`'s `HandleRegisterAsync` calls `await editContext.ValidateAsync()` and returns early on failure, before ever reaching `SubmitAsync`. There is no code path in our own components that would call `NavigateTo` for an invalid or rejected submission. The navigation is not coming from our C#.

### Possible fix directions (none attempted beyond what's above)

- **File upstream against BlazingStory.** This is the most durable fix — the bug lives entirely in `PooledIFrame.razor.js`'s interaction with the package's own custom router; we cannot patch a NuGet package's static web asset in place (it's overwritten on every restore) without an MSBuild-level content-override hack, which is fragile and not worth it for a preview-quality dependency likely to move fast anyway.
- **Local mitigation, untested:** the pool's `navigate()` call and `StoryDriver.OnAfterRenderAsync(firstRender: true)`'s `requestSubmit()` call are not currently sequenced against each other. Delaying `StoryDriver`'s drive until the pool's own route transition has demonstrably settled (not just "the form exists in the DOM," which is already true today) might avoid landing the auto-submit mid-transition. This was proposed but explicitly **not** attempted — it would be trading a confirmed root cause for an unconfirmed timing patch, and Buvy's call (2026-08-08/09 session) was to punt rather than layer another guess on top of two already-failed fix attempts.
- **Check whether disabling/bypassing the pool is possible** via `BlazingStoryOptions` or similar public configuration surface — not investigated. If the pool can be turned off (even if it costs a fresh WASM boot per story switch), that would sidestep the entire mechanism rather than fix it.
- **Track upstream releases.** Re-check this issue against whatever `BlazingStory` version is current before re-investigating from scratch — `PooledIFrame.razor.js` could plausibly change or the bug could already be fixed upstream.

### Investigation trail, for anyone re-opening this

Full instrumented repro method (useful if this needs re-verifying against a newer package version): using Playwright, monkey-patch `HTMLFormElement.prototype.requestSubmit`, and hook `submit` (capture), `beforeunload`, and `pagehide` via `page.addInitScript` (applies to the top frame and all iframes). Drive the SPA: load a driven story cold (works), click to a sibling story, click back to the original driven story (breaks). Compare `beforeunload`/`pagehide` firing (or not) and the resulting `iframe.contentDocument.location.href` against `viewMode=story&id=...` to detect the nested-catalog state programmatically.

To re-decompile BlazingStory if the package version changes:

```bash
ilspycmd -l c <path-to-BlazingStory.dll>                          # list types
ilspycmd -t "BlazingStory.Internals.Components.Layouts.PooledIFrame" <path-to-BlazingStory.dll>
```

(`ilspycmd` is already installed as a global .NET tool in this dev environment.) The JS itself is not in the DLL — it's a plain file under the package's `staticwebassets/` folder, readable directly with no decompilation needed.
