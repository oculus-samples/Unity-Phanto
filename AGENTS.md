# Agent Instructions — Project Phanto

Unity Mixed Reality reference app for Meta Quest demonstrating Scene Mesh, Scene Model, Scene API objects, Depth API, and TruTouch haptics. Positioned as both a reference and a template for MR projects.

## Source-of-truth files (read these first, do not duplicate their contents in this file)

For setup, build steps, SDK versions, and project layout, read:

- `README.md` — official setup, instructions, and the device compatibility table
- `ProjectSettings/ProjectVersion.txt` — Unity editor version
- `Packages/manifest.json` — Unity package versions (Meta XR SDKs, Depth API, MR Utility Kit, Haptics)
- `Documentation/MainScenes.md`, `Documentation/ExampleScenes.md` — which scenes to load
- `Documentation/KeyComponents.md`, `DesignFlow.md`, `HealthAndSafetyGuidelines.md` — system docs
- `LICENSE.txt` — license terms

## Quest / Horizon-specific notes

- Git LFS is **required**. Run `git lfs install` before cloning.
- `Scene Data Source` is a runtime switch in `SceneDataLoaderSettings.asset`: `Scene Api` for headset / Quest Link, `Static Mesh Data` for desk-bound dev with no live scene model. If scene mesh looks "missing", check that toggle first.
- Scene Mesh is **Quest 3 only**. Color passthrough works on Quest 3 / Quest Pro. Quest 2 supports Scene API only. The compatibility table in `README.md` is authoritative — don't debug missing capabilities on unsupported devices.
- Room scans must be triggered on the headset itself, even when running via Quest Link from the editor.
- Haptic clips were authored in Haptics Studio; replacing them with synthesized vibration is a fidelity regression.

# Meta Quest tooling

This is a Meta Quest / Horizon OS sample. The bespoke intro above is the source of truth for what this project is and how it's built — use it (and the files it points at) instead of restating facts from memory.

When the user asks anything about Quest device behavior, build / deploy / debug / capture flows, on-device performance, or Horizon OS APIs, reach for these tools instead of generic Unity answers:

- **`hzdb`** — Quest-aware ADB wrapper (device list, install / launch / stop, logs, screenshots, Perfetto traces, on-device docs search). Already wired up as an MCP server via `.mcp.json`, `.vscode/mcp.json`, and `.cursor/mcp.json`. Also runnable directly: `npx -y @meta-quest/hzdb <subcommand>`.
- **Meta Quest Agentic Tools** — the full skill set, including Unity-specific skills: <https://github.com/meta-quest/agentic-tools>. Install per your client (Claude Code: `/plugin install meta-vr@meta-quest`; Gemini CLI: `gemini extensions install https://github.com/meta-quest/agentic-tools`; Cursor / VS Code: install the **Meta Horizon** extension from the Marketplace).

A few behavior expectations:

- **Read this repo's files first.** Before answering anything project-specific, read `README.md` and whichever source-of-truth files the intro above points at. Don't restate their contents in chat — quote or link instead.
- **Use `hzdb` for device-side work.** Anything that touches an attached Quest (install, launch, logs, screenshot, capture, manifest inspection) goes through `hzdb`, not raw `adb`.
- **Check live Horizon OS docs before answering API questions.** `hzdb docs search "..."` queries the live docs; training data on Horizon OS APIs goes stale fast.
- **Don't fabricate SDK / engine versions.** If a version isn't visible in this repo's files, say so rather than guessing.
