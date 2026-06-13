# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity Editor package (`com.company.multi-platform-addressables-builder`) that orchestrates multi-platform Addressables builds (Android + QNX) within a single editor session. It does **not** replace Unity Addressables — it prepares context, calls the standard Addressables API, writes reports, and restores editor state.

- Unity 2022.3+ required
- Package source: `Packages/com.company.multi-platform-addressables-builder/`
- All code lives under `Editor/` (editor-only assembly)

## Running the Tool

**In Editor:** `Tools > Multi Platform Addressables Builder`

**CLI / Batch mode:**
```bash
Unity.exe -batchmode -quit \
  -projectPath <project_path> \
  -executeMethod Company.MultiPlatformAddressablesBuilder.Editor.MultiPlatformAddressablesBuilderCli.BuildFromDefaultConfig \
  -mpabConfig Assets/Build/MultiPlatformAddressablesBuildConfig.asset \
  -mpabPlatforms Android,QNX \
  -mpabScope CommonAndPlatform
```

CLI args: `-mpabConfig`, `-mpabPlatforms` (comma-separated), `-mpabScope` (enum value from `ResourceScope`)

## Architecture

### Two-Context Build Model

Addressables builds require two separate contexts managed in sequence:
1. **Unity platform context** — BuildTarget, imports, shader cache (switched via `MpabPlatformSwitcher`)
2. **Addressables context** — Profile, group inclusion/exclusion, output paths (managed by `MpabAddressablesEditorAdapter`)

### Core Module Map

| Module | Key File(s) | Responsibility |
|--------|------------|----------------|
| Entry points | `MultiPlatformAddressablesBuildController.cs` | Static `Validate()` and `RunBuild()` shared by UI and CLI |
| Orchestration | `MpabBuildOrchestrator.cs` | 15-step state machine driving the full build loop |
| Platform switching | `MpabPlatformSwitcher.cs`, `IMpabPlatformSwitchHandler.cs` | Three switch modes: `UnityBuildTarget`, `CurrentEditor`, `CustomHandler` |
| Addressables adapter | `MpabAddressablesEditorAdapter.cs` | Profile switching, group state capture/restore, `BuildPlayerContent` invocation |
| Group rules | `MpabAddressablesGroupRuleEvaluator.cs` | Wildcard-based group inclusion/exclusion per platform + resource scope |
| Config | `MultiPlatformAddressablesBuildConfig.cs`, `MpabEnums.cs` | ScriptableObject config; enums for `ResourceScope`, `GroupRuleKind`, `PlatformSwitchMode` |
| Validation | `MpabBuildValidator.cs`, `MpabValidationResult.cs` | Pre-build checks (Play Mode, config, profiles, output isolation) |
| Session persistence | `MpabBuildSessionStore.cs`, `MpabBuildSessionState.cs` | Survives domain reloads; state at `Library/MultiPlatformAddressablesBuilder/build_session.json` |
| Reporting | `MpabBuildReportWriter.cs` | JSON reports at `BuildOutput/MultiPlatformAddressablesBuilder/Reports/` |
| UI | `MultiPlatformAddressablesBuilderWindow.cs` | EditorWindow — fully decoupled from core logic |
| CLI | `MultiPlatformAddressablesBuilderCli.cs` | Batch mode arg parsing |

### State Machine (MpabBuildOrchestrator)

The orchestrator runs a 15-step sequence per platform, designed to survive Unity domain reloads:

```
Idle → Prepare → Validate → SwitchPlatform → WaitForCompilation → CheckCompilation
     → ApplyAddressablesConfig → SaveModifiedConfig → BuildAddressables → CollectResult
     → NextPlatform → Restore → SaveRestoredConfig → GenerateReport → Done / Failed
```

State restoration is attempted even on failure. On editor restart, the session file enables detection and recovery from interrupted builds.

### Platform Switch Modes

- `UnityBuildTarget` — standard `EditorUserBuildSettings.SwitchActiveBuildTarget`
- `CurrentEditor` — no platform switch (use current editor platform as-is)
- `CustomHandler` — implement `IMpabPlatformSwitchHandler` for vendor-specific targets (e.g., QNX)

### Configuration

Config is a ScriptableObject at `Assets/Build/MultiPlatformAddressablesBuildConfig.asset`. It defines:
- Per-platform configs (BuildTarget, Addressables profile name, output path)
- Group rules (name wildcard patterns → `Common`, `PlatformSpecific`, or `Ignored`)
- Build defaults (clean before build, restore states, etc.)

## Output Locations

| Artifact | Path |
|----------|------|
| Android Addressables | `BuildOutput/Android/` (configurable) |
| QNX Addressables | `BuildOutput/QNX/` (configurable) |
| Build reports | `BuildOutput/MultiPlatformAddressablesBuilder/Reports/` |
| Session state | `Library/MultiPlatformAddressablesBuilder/build_session.json` |

## Key Design Decisions

- The UI (`EditorWindow`) has no build logic — all logic routes through `MultiPlatformAddressablesBuildController`, enabling CLI reuse.
- Group rules use wildcard pattern matching, not exact names, so they remain stable as group names evolve.
- The `MpabAddressablesEditorAdapter` wraps all Addressables API calls so the rest of the codebase never imports Addressables namespaces directly.
- Session state serialization uses `JsonUtility` (no external deps) and writes to `Library/` (git-ignored by Unity convention).
