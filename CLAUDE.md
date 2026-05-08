# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A modular, plugin-based toolkit for inspecting and editing Halo cache files (Halo 3, ODST, Reach, Halo Online/MS23). Two main components:

- **Epsilon** — WPF GUI application with a MEF plugin architecture
- **TagTool** — CLI tool and library for cache file research/modification (git submodule from `BIRD-COMMAND/TagTool-Misc-Builds`)

## Build Commands

Target framework is `.NET 10.0-windows`, platform `x64`. The CI uses Visual Studio / MSBuild.

```bash
# Restore packages
nuget restore Epsilon/Epsilon.slnx
nuget restore TagTool/TagTool.slnx

# Build (Release)
msbuild Epsilon/Epsilon.slnx -t:rebuild -property:Platform=x64 -property:Configuration=Release
msbuild TagTool/TagTool.slnx -t:rebuild -property:Platform=x64 -property:Configuration=Release

# Run
./Epsilon/bin/x64/Release/net10.0-windows/Epsilon.exe
./TagTool/TagTool/bin/x64/Release/net10.0-windows/TagTool.exe <cache_file> [command]
```

Build configuration is centralized in `Epsilon/Directory.Build.props`. There is no automated test suite; correctness is validated through CI builds on push to master.

## Architecture

### Epsilon (GUI)

- **Entry/Bootstrapper**: `Bootstrapper.cs` initializes MEF composition and the Stylet MVVM framework.
- **Plugin Loader**: `PluginLoader.cs` scans a `plugins/` directory at startup, resolves assembly dependencies via reflection, and composes plugins into the MEF container. Plugins follow naming convention `plugins/{Name}/{Name}.dll`. Mark an assembly with `[DisabledPluginAttribute]` to exclude it.
- **Editor Service**: `EditorService.cs` uses a provider pattern — plugins register `IEditorProvider` implementations that declare which file/tag types they handle. Opening a file dispatches to the correct provider.
- **Shell Abstraction**: `IShell` (in `Shared.dll`) is the central interface used by plugins to interact with the host (open editors, show dialogs, access settings).
- **Docking UI**: AvalonDock (customized copy in `Libraries/`) manages the document/tool-window layout.
- **EpsilonLib**: Shared WPF library (~769 .cs files) containing behaviors, converters, custom controls, menu system, settings infrastructure (`ISettingsCollection`), and XAML themes.

### TagTool (CLI/Library)

- **Command System**: Commands implement `Command.cs` and are organized into a `CommandContextStack`. The CLI enters nested contexts (e.g., opening a cache puts you in a tag-browsing context).
- **Cache Support**: Generation-specific code lives under directories named by generation (`Gen1/`, `Gen2/`, `Gen3/`, `HaloOnline/`, `Gen4/`, `MCC/`). Version detection is in `CacheVersionDetection.cs`.
- **Tag Pipeline**: `TagCache.cs` / `CachedTag.cs` manage in-memory tag data. Assets (bitmaps, models, audio, geometry, animations) have dedicated extraction and generation commands (~50+ command subdirectories).
- **Shader Generation**: `HaloShaderGenerator` project produces templated HLSL shaders for Halo Online from `.fx`/`.hlsl` templates under `ShaderGenerator/halo_online_shaders/`.
- **Scripting**: Embeds `Microsoft.CodeAnalysis.CSharp.Scripting` for automation scripts.
- **External Tools**: Assimp (model import), LZ4, Havok physics, FMOD audio, `meshoptimizer.dll`, `JsonMoppNet.dll` are bundled in `TagTool/Tools/` or as NuGet references.

### Key Shared Boundaries

| Interface | Where defined | Purpose |
|-----------|--------------|---------|
| `IShell` | `Shared.dll` | Plugin → host communication |
| `IEditor` | `Shared.dll` / `EpsilonLib` | Document editor abstraction |
| `IEditorProvider` | `EpsilonLib` | Factory for opening file/tag types |
| `ISettingsCollection` | `EpsilonLib` | Persistent keyed settings per plugin |

Plugins depend only on `Shared.dll` and `EpsilonLib`, never on `Epsilon.exe` directly.
