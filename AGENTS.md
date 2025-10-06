# AGENTS.md

This repository uses Godot 4 + C# (Godot.NET). Agents working here should validate builds frequently to catch errors early.

## Required Practice

- After every non-trivial code change (C# or scene script), run a fast build of the solution and fix any compile errors before proceeding.
- Keep build output concise in messages: report success/failure and the first few errors with file:line references.

## Build Command

- Preferred: run a .NET build of the Godot project file.
  - Command: `dotnet build MultiplayerFPSTutorial.csproj -c Debug`
  - Rationale: quick compiler feedback without needing the Godot editor/CLI.

## When To Build

- Always after applying a patch that changes `.cs` files.
- When changing Godot scene/script wiring that may affect C# compilation (e.g., new script filenames, moved classes).
- Group small related edits, then build once to reduce churn.

## Handling Failures

- Treat compile errors as blocking. Apply minimal, targeted fixes to resolve them.
- Prefer surgical changes; do not refactor unrelated code to silence warnings.
- Common issues:
  - Godot API types (e.g., `CollisionMask` is `uint` in Godot 4). Use explicit casts and correct types.
  - Missing symbols due to file moves/renames. Update `using` directives and namespaces to match project conventions.

## Unrelated Errors Policy

- If a build surfaces errors unrelated to your current task, do not refactor or “fix the world.”
- Note the first 1–2 unrelated errors with `file:line` in your handoff, then continue your scoped work.
- Only touch unrelated code when it directly blocks your change (keep edits surgical and minimal).

## Build Retry Policy

- If a build fails and you still have clear next steps, make progress on those steps first, then retry the build later.
- If the build is the final blocking step and you suspect transient noise, wait ~30–60 seconds and retry once.
- Keep retries limited; if it still fails, report the first error(s) and your intended next fix.

## Collaboration

- Prefer additive, narrowly scoped changes to avoid stepping on parallel work.
- Communicate intent via short plans; group small edits and build once per group.
- Avoid mass renames/formatting outside your task scope.

## Tooling & Performance

- Use `rg` for searching and file lists: it’s faster and keeps the console responsive.
  - Examples: `rg -n "symbol"`, `rg --files`.
- Read files in chunks (<= 250 lines per read) to avoid output truncation.

## Approvals and Sandboxing

- Follow the harness’ approval policy. If write/network actions require escalation, request it and explain why.
- Avoid network access for builds; `dotnet build` must run offline against the project’s already-restored packages.

## Optional Extras

- Before handing off large changes, a Release build is acceptable but not required:
  - `dotnet build MultiplayerFPSTutorial.csproj -c Release`
- If build noise is suspected from stale artifacts, a quick `dotnet clean` followed by a rebuild can help.

## Warnings Policy

- Treat important C# warnings as actionable and fix them promptly. Common ones here:
  - CS0108 (member hides inherited): add `new` if intentional (e.g., `Player.Velocity`).
  - CS1998 (async method without await): remove `async` if no awaits are used.
  - CS4014 (fire-and-forget Task): explicitly discard with `_ = SomeAsyncCall();` when intended.
  - Nullable ref warnings (CS86xx):
    - Use `#nullable enable` in files that use `?` annotations.
    - Mark optional Godot references and materials as nullable (e.g., `Material?`).
    - Avoid assigning `null` to non-nullable locals/fields; prefer nullable types or explicit defaulting.
- Quick local check for warnings: `dotnet build MultiplayerFPSTutorial.csproj -c Debug -warnaserror:CS*` to escalate C# warnings without breaking on NuGet audit noise.
- Note: Offline environments may emit `NU1900` (NuGet audit). Don’t block on it; focus on CS warnings.

## Status Reporting Template

- After each change, report:
  - Build command run
  - Result (success/failure)
  - If failed: first error(s) with file:line summary and your next fix step
