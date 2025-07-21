# UtilityBelt Tech Stack

## Primary Technology
- **Language**: C# (.NET Framework 4.8)
- **C# Language Version**: 10.0
- **Target Platform**: x86 (32-bit)
- **Project Type**: Class Library (DLL)

## Development Environment
- **IDE**: Visual Studio 2017+ (Visual Studio Version 17.3.32811.315)
- **Build System**: MSBuild with .NET SDK-style project format
- **Version Control**: Git (GitLab hosted)

## Key Dependencies
### Asheron's Call Integration
- Decal.Adapter - Core Decal plugin API
- Decal.FileService, Decal.Interop.Core, Decal.Interop.D3DService
- Decal.Interop.Filters, Decal.Interop.Input, Decal.Interop.Render

### Third-Party Tools Integration
- utank2-i - uTank2 integration
- VCS5, VirindiHotkeySystem, VirindiHUDs, VirindiViewService - Virindi tools
- VTClassic - VTClassic integration

### Core Libraries
- Microsoft.DirectX, Microsoft.DirectX.Direct3D - DirectX integration
- MoonSharp.Interpreter - Lua scripting support
- Newtonsoft.Json - JSON serialization
- LiteDB - Embedded database
- Lib.Harmony - Runtime code patching
- System.Reactive - Reactive programming

### UtilityBelt Ecosystem
- UtilityBelt.Helper, UtilityBelt.Common, UtilityBelt.Networking
- UtilityBelt.Service - Background service component

### Build Tools
- Antlr4 - Parser/lexer generation for expressions
- GitVersion.MsBuild - Automatic versioning
- NSIS-Tool - Windows installer creation
- MSBuildTasks - Additional build tasks