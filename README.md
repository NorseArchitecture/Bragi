# Bragi

> Bragi, skaldic god of poetry, keeper of every tale worth telling.

![Bragi — the skaldic god of poetry, master of eloquence, welcoming the honored dead into Valhalla with song](https://github.com/user-attachments/assets/7596c242-e424-4746-bb33-5e5151c8a0de "Bragi — the skald who sings of everything aboard the ship")

*Image credit: [@norsemythologyclips](https://www.instagram.com/norsemythologyclips/) — go follow them.*

Bragi is the story: **`Norse.DesignSystem.Stories`** — a content-only Razor Class Library of `.stories.razor` catalog pages and Markdown documentation for the platform's Blazor components. Bragi doesn't build the ship; he sings of everything aboard it.

It carries a `BlazingStory` package reference purely for the `.stories.razor` authoring API, and references Asgard's `Abstractions.Components` directly so its stories can preview the headless primitives (`AuthN.Components.FluentUI` / `ReferenceData.Components.FluentUI` once those realms ship). Bragi ships no runnable app of its own — Yggdrasil hosts the runnable BlazingStory catalog built from it (`Hosting.Stories.Client`/`.Server`), published as a container to `ghcr.io/norsearchitecture/hosting/stories`.

## Status

Split out of Naglfar on 2026-07-12, the same day `DesignSystem.Stories` first landed there as a plain RCL — see `../Glitnir/docs/Platform/specs/2026-07-12-designsystem-stories-hosting-design.md` (superseded in part) for the original decision and its addendum recording the split. Naglfar keeps the token pipeline; Bragi owns the story content — two different publish cadences (npm vs. NuGet) that don't belong wearing one repo's clothes.

## The cosmos

Bragi rides as a submodule of [Bifröst](https://github.com/NorseArchitecture/Bifrost), the Norse Architecture's meta-repository, alongside every other realm. Design-system content here — stories, story authoring, what a component's catalog page shows — is exempt from the platform's brainstorm → spec → plan → TDD cycle, the same standing call as Naglfar: this is content, not behavioral code.

## Soundtrack: Bragi | God of Poetry and Sacred Speech | Norse Song
[![Soundtrack: Bragi | God of Poetry and Sacred Speech | Norse Song](https://img.youtube.com/vi/HfHsOQ1lagE/maxresdefault.jpg)](https://www.youtube.com/watch?v=HfHsOQ1lagE)
