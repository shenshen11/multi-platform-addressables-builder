# Multi Platform Addressables Builder

A Unity Editor package that orchestrates multi-platform Addressables builds (e.g. Android + QNX) within a single editor session.

This package does **not** replace Unity Addressables. It prepares the editor platform context and Addressables configuration, calls the standard `BuildPlayerContent` API, writes a JSON report, and restores the original editor state — including survival across domain reloads.

> **Status:** v0.1.0 — functional and used in internal testing. Automated test coverage is not yet included.

---

## Requirements

- Unity 2022.3 or later
- `com.unity.addressables` 1.22.3 or later

---

## Installation

**Option 1 — Local path** (this repo already includes the package):

```text
Packages/com.company.multi-platform-addressables-builder
```

**Option 2 — Git URL** (add to your project's `Packages/manifest.json`):

```json
"com.company.multi-platform-addressables-builder": "https://github.com/<you>/multi-platform-addressables-builder.git?path=Packages/com.company.multi-platform-addressables-builder"
```

---

## Quick Start

1. Open **Tools > Multi Platform Addressables Builder**.
2. Click **Create Default Config** if no config asset exists yet.
3. Adjust the config at `Assets/Build/MultiPlatformAddressablesBuildConfig.asset`.
4. Select platforms and resource scope in the editor window.
5. Click **Validate**, then **Build Selected**.

---

## Configuration

The config asset is a ScriptableObject at `Assets/Build/MultiPlatformAddressablesBuildConfig.asset`. Key settings:

| Field | Description |
|-------|-------------|
| Platform Configs | Per-platform: BuildTarget, Addressables profile name, output path, platform switch mode |
| Group Rules | Wildcard patterns → `Common`, `PlatformSpecific`, or `Ignored` |
| Clean Before Build | Delete previous output before each platform build |
| Restore On Complete | Restore original platform and Addressables state after build |

### Platform Switch Modes

Each platform config can use one of three switch modes:

| Mode | Behavior |
|------|----------|
| `UnityBuildTarget` | Calls `EditorUserBuildSettings.SwitchActiveBuildTarget` with parsed target names |
| `CurrentEditor` | No platform switch; uses the active editor platform as-is |
| `CustomHandler` | Instantiate a class implementing `IMpabPlatformSwitchHandler` for vendor-specific targets |

`CustomHandler` is the recommended mode for platforms like QNX that are not built-in Unity targets.

### Group Rules

Rules use wildcard patterns (e.g. `Android*`, `*Shared*`). Evaluation order:

1. Explicit name match (highest priority)
2. Wildcard pattern match

Groups not matched by any rule are included with a warning in the build report.

### Resource Scopes

| Scope | Groups included |
|-------|----------------|
| `CommonOnly` | Shared groups only |
| `PlatformOnly` | Platform-specific groups only |
| `CommonAndPlatform` | Both shared and platform-specific groups |
| `AllIncludedByPlatform` | All groups not explicitly `Ignored` |

---

## CLI / Batch Mode

The same controller used by the editor window is available in Unity batch mode:

```bash
Unity.exe -batchmode -quit \
  -projectPath <project_path> \
  -executeMethod Company.MultiPlatformAddressablesBuilder.Editor.MultiPlatformAddressablesBuilderCli.BuildFromDefaultConfig \
  -mpabConfig Assets/Build/MultiPlatformAddressablesBuildConfig.asset \
  -mpabPlatforms Android,QNX \
  -mpabScope CommonAndPlatform
```

| Argument | Description |
|----------|-------------|
| `-mpabConfig` | Path to the config asset |
| `-mpabPlatforms` | Comma-separated platform names to build |
| `-mpabScope` | `ResourceScope` enum value |

---

## Output Locations

| Artifact | Default Path |
|----------|-------------|
| Android Addressables | `BuildOutput/Android/` (configurable) |
| QNX Addressables | `BuildOutput/QNX/` (configurable) |
| Build reports (JSON) | `BuildOutput/MultiPlatformAddressablesBuilder/Reports/` |
| Session state | `Library/MultiPlatformAddressablesBuilder/build_session.json` |

---

## Architecture Overview

### Two-Context Build Model

Each platform build requires two independent contexts switched in sequence:

1. **Unity platform context** — `BuildTarget`, shader variants, import settings (managed by `MpabPlatformSwitcher`)
2. **Addressables context** — profile, group inclusion/exclusion, output paths (managed by `MpabAddressablesEditorAdapter`)

### Build State Machine

The orchestrator (`MpabBuildOrchestrator`) runs a 15-step sequence per platform, designed to survive domain reloads:

```
Idle → Prepare → Validate → SwitchPlatform → WaitForCompilation → CheckCompilation
     → ApplyAddressablesConfig → SaveModifiedConfig → BuildAddressables → CollectResult
     → NextPlatform → Restore → SaveRestoredConfig → GenerateReport → Done / Failed
```

State restoration is attempted even on failure. If the editor is restarted mid-build, the session file (`Library/`) enables detection and recovery.

### Module Map

| Module | Key File | Responsibility |
|--------|----------|----------------|
| Entry points | `MultiPlatformAddressablesBuildController.cs` | `Validate()` and `RunBuild()` shared by UI and CLI |
| Orchestration | `MpabBuildOrchestrator.cs` | 15-step state machine |
| Platform switching | `MpabPlatformSwitcher.cs` | Three switch modes |
| Addressables adapter | `MpabAddressablesEditorAdapter.cs` | Wraps all Addressables API calls |
| Group rules | `MpabAddressablesGroupRuleEvaluator.cs` | Wildcard-based group inclusion/exclusion |
| Config | `MultiPlatformAddressablesBuildConfig.cs` | ScriptableObject config |
| Validation | `MpabBuildValidator.cs` | Pre-build checks |
| Session persistence | `MpabBuildSessionStore.cs` | Survives domain reloads |
| Reporting | `MpabBuildReportWriter.cs` | JSON report generation |
| UI | `MultiPlatformAddressablesBuilderWindow.cs` | EditorWindow (no build logic) |
| CLI | `MultiPlatformAddressablesBuilderCli.cs` | Batch mode arg parsing |

---

## QNX Support

QNX is not assumed to be a built-in Unity `BuildTarget`. Use `CustomHandler` mode and implement `IMpabPlatformSwitchHandler`:

```csharp
public class MyQnxSwitchHandler : IMpabPlatformSwitchHandler
{
    public bool SwitchPlatform(MpabPlatformConfig config)
    {
        // vendor-specific switch logic
        return true;
    }

    public void RestorePlatform()
    {
        // restore original state
    }
}
```

Set the handler type name in the platform config. This keeps all vendor-specific code outside this package.

---

## Known Limitations

- No automated test suite. `MpabTestSetup.cs` provides helpers for creating test assets, but test cases are not yet written.
- Addressables package version is not queried dynamically and appears as `"unknown"` in build reports.
- `WaitForCompilation()` has no timeout; extremely long compilations will block silently.

---

## License

MIT
