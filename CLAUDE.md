# CLAUDE.md — Bragi (`Norse.DesignSystem.Stories`)

## 0. Wrong Root — Halt

If you are reading this because **Bragi itself is the Claude Code session root** — someone ran `claude` from inside this directory instead of `../Bifrost` — stop here. Do not read further, do not propose changes, do not run anything.

Tell the user: every Norse Architecture session starts from **Bifrost**. Org-wide settings (the `superpowers` plugin, permission rules) only apply when Bifrost is the actual session root — Claude Code never merges a submodule's own `.claude/settings.json` into a parent-launched session. Exit, `cd ../Bifrost`, and run `claude` there instead.

This repo's own `.claude/settings.json` carries a `SessionStart` hook that should already have blocked this session before this file was ever read. If you're reading this anyway, hooks were bypassed, disabled, or failed — halt regardless; this rule does not depend on the hook to hold.

---

> **Do not commit, push, or rewrite git history.** Stage edits (`git add`), show the diff, and stop — the human reviews and commits.

> **Use US English spelling** in code, identifiers, comments, docs, and commit/PR copy.

## 1. What This Repository Is

Bragi is the story: **`Norse.DesignSystem.Stories`** — a content-only Razor Class Library of `.stories.razor` catalog pages and Markdown documentation (via `MD2RazorGenerator`) for the platform's Blazor components. It carries a `BlazingStory` package reference purely for the `.stories.razor` authoring API — that's a content-authoring dependency, not a hosting one — and a `NorseRef` to Asgard's `Abstractions.Components` so its stories can preview the headless primitives directly (`AuthN.Components.FluentUI` / `ReferenceData.Components.FluentUI` once those realms ship).

Bragi ships no runnable app of its own. Yggdrasil owns the runtime: `Hosting.Stories.Client` (`Microsoft.NET.Sdk.BlazorWebAssembly`) is the **only** project holding the `NorseRef` to this repo's `DesignSystem.Stories` — everything Bragi references rides in transitively — and `Hosting.Stories.Server` is the ordinary ASP.NET Core process that serves it, dockerized and published to `ghcr.io/norsearchitecture/hosting/stories`.

**Split from Naglfar on 2026-07-12**, the same day `DesignSystem.Stories` first landed there as a plain RCL. See `../Glitnir/docs/Platform/specs/2026-07-12-designsystem-stories-hosting-design.md` (superseded in part — its §1.1/§2.1/§4 Naglfar-ownership decision, addended the same day) for the original hosting design and the note recording this split. Naglfar keeps the npm/Style Dictionary token pipeline; Bragi owns the story content, NuGet-published as `Norse.DesignSystem.Stories`. The two don't share a publish cadence or toolchain (npm vs. NuGet) and don't belong wearing one repo's clothes.

**Design-system content here is exempt from the platform's brainstorm → spec → plan → TDD cycle** — the same standing call as Naglfar (`../Bifrost/CLAUDE.md` §6, and the platform's design-system-exemption precedent). Stories, story authoring, and what a component's catalog page shows are content decisions, not behavioral code. If genuine behavioral logic (not just markup/story wiring) ever lands here, reassess — that would warrant the standard TDD discipline like any other realm.

**Ungated CI** — like Naglfar before it, little unit-testable logic lives in this repo directly (Asgard's components are already gated in their own repo); the `gate / build` check runs but isn't required by branch protection. Revisit if that changes.

See `../Bifrost/CLAUDE.md` (§2 The Naming Model) and `../Glitnir/CLAUDE.md` (§3 Bounded Context Map) for the full realm table and how Bragi fits the rest of the cosmos.
