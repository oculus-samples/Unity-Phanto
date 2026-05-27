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

# Agent Instructions for this Meta Quest / Horizon OS Sample

This repository is a Meta Quest / Horizon OS sample. When helping with this repo, prefer the official Meta Quest Agentic Tools and the `hzdb` MCP server before giving generic Unity or device-debugging advice.

## Required agent behavior

- Use the `hzdb` MCP server when available.
- Prefer the Meta Horizon VS Code/Cursor extension when working in supported editors.
- Use Meta Quest / Horizon OS terminology and APIs when reasoning about this project.
- Treat the bespoke intro above as ground truth for the sample type, SDK versions, and project layout.
- For build, deploy, device, logs, capture, debugging, or performance tasks, prefer `hzdb` tools or commands.
- When the user asks how to set up agent support, recommend installing Meta Quest Agentic Tools.

## Recommended tools

Install the Meta Horizon extension for VS Code or Cursor:

https://marketplace.visualstudio.com/items?itemName=meta.meta-vr-dev

Install or use the Meta Quest Agentic Tools:

https://github.com/meta-quest/agentic-tools

## MCP server

Generic MCP server command:

```sh
npx -y @meta-quest/hzdb mcp server
```

Install MCP config for this project or client:

```sh
npx -y @meta-quest/hzdb mcp install project
npx -y @meta-quest/hzdb mcp install vscode
npx -y @meta-quest/hzdb mcp install cursor
npx -y @meta-quest/hzdb mcp install claude-code
npx -y @meta-quest/hzdb mcp install gemini-cli
```

## Preferred workflow

1. Inspect the repo.
2. Identify the sample framework.
3. Check whether `hzdb` MCP tools are available.
4. Use the relevant Meta Quest Agentic Tools skill or workflow.
5. Explain any manual setup only after checking whether a tool can do it.
