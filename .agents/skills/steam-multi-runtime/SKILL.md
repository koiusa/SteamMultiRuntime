---
name: steam-multi-runtime
description: Implement, diagnose, review, or document the SteamMultiRuntime Unity project, especially player movement and skills, local/network ownership, Input System bindings, pause/debug menus, character selection, lobby UI, guard effects, and package boundaries. Use for changes under Assets/SteamMultiRuntime or Documentation in this repository.
---

# SteamMultiRuntime

Work from the repository's current code and serialized assets; do not infer prefab, scene, or Input Action state from C# alone. Target the Unity version in `ProjectSettings/ProjectVersion.txt` and the package versions already pinned by the project.

## Workflow

1. Check `git status --short` and preserve unrelated or pre-existing changes.
2. Read the relevant architecture document under `Documentation/`.
3. Inspect the implementation plus the serialized and boundary assets involved: `.asmdef`, prefab/scene, UXML/USS, `.inputactions`, and package metadata as applicable.
4. Trace both Local and Network paths before changing shared player, input, UI, ownership, lobby, or skill behavior.
5. Preserve package dependency direction and follow the reflection policy in `Documentation/PackageArchitecture.md`.
6. Make file edits with `apply_patch`.
7. Update the matching documentation whenever bindings, serialized ownership, menu responsibilities, or architecture change.
8. Verify with focused searches and the narrowest available compile or Unity validation. State explicitly when Play Mode, serialization/import, host/client, or Steam validation was not run.

## No-reflection policy

- Treat `Documentation/PackageArchitecture.md` as the source of truth. Do not duplicate or weaken its rules in task-local code or documentation.
- Do not add reflection-like dispatch to first-party Runtime code. Prefer typed contracts and explicit adapters.
- Apply only the documented Editor and vendored Thirdparty exceptions. When reviewing reflection findings, distinguish delegate `Invoke` and ordinary `object.GetType()` from member inspection.

## Runtime update policy

- Apply push-based design across the project, not only to input. Prefer callbacks, events, async completion, explicit commands, and lifecycle transitions for discrete changes in UI, networking, ownership, loading, configuration, collections, and gameplay state.
- Before adding `Update`, `FixedUpdate`, `LateUpdate`, a coroutine loop, timer, or repeated query, verify that the behavior genuinely requires continuous sampling, elapsed-time progression, or a specific Unity player-loop phase. Use an existing notification when one is available.
- Limit polling to continuous behavior such as movement, physics, interpolation, animation following, and held-action repeat. Centralize necessary polling in the system that owns the behavior; do not duplicate it in every consumer.
- Start and stop subscriptions, timers, and polling with explicit lifecycle ownership. Avoid scene-wide searches, repeated `GetComponent`, allocation, and unchanged-state work inside frame loops.
- Do not introduce an interface or event bus only to appear decoupled. Use typed events or contracts when there are multiple implementations, a package boundary, or a real producer-consumer lifetime boundary.
- When polling is retained in new or substantially changed code, make its necessity apparent in code structure or documentation and verify its frequency and shutdown behavior.

## Project rules

- Treat `Assets/SteamMultiRuntime/Runtime/Configs/Input/SteamMultiRuntime_InputActions.inputactions` as the production input source of truth.
- Use `PlayerCharacterCoordinator` or `IPlayerSkillCoordinator` as the skill entry point. Do not call concrete skill features from input or network code.
- Keep server-authoritative skill eligibility, hit detection, damage, and healing in Network play.
- Use `ILocalPlayerOwnership` for local-owner decisions. Do not restore member-name probing or ownership reflection.
- Keep Player core free of `Unity.Netcode`; Netcode packages may depend on their non-Netcode counterparts, never the reverse.
- Do not introduce `[DefaultExecutionOrder]` to solve update-order or moving-platform issues. Fix ownership, interpolation, or phase boundaries directly.
- Keep runtime-created UI and effect objects inactive when their owning player/session context is absent.
- Treat code under `Runtime/Packages/Thirdparty` as vendored: do not refactor it opportunistically. Wrap it with typed first-party adapters when integration behavior must change.
- Do not restore the removed Build Profile creation tool, sample, presets, or documentation. Unity's non-public Build Profile API wrapper was intentionally removed.

Read [references/current-state.md](references/current-state.md) before changing input, UI, character selection, lobby, ownership, or guard behavior.
