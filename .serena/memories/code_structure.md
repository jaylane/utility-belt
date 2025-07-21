# UtilityBelt Code Structure

## Project Layout
```
UtilityBelt/
├── UtilityBelt.cs          # Main plugin class
├── Tools/                  # Individual tool implementations (40+ files)
├── Lib/                    # Core library classes
├── Views/                  # UI view definitions (XML)
├── Resources/              # Embedded resources (icons, data, tiles)
├── scripts/                # Build scripts
└── installer.nsi          # NSIS installer configuration

UBLoader/                   # Loader project
deps/                       # External dependencies
changelog/                  # Version changelogs
```

## Main Components

### Core Plugin Architecture
- **UtilityBelt.cs**: Main plugin entry point with command/expression system
- **Command System**: Attribute-based command registration with regex patterns
- **Expression System**: Custom expression language for automation

### Tool Architecture
Each tool in the `Tools/` directory is a separate feature module:
- **Automation Tools**: AutoTrade, AutoSalvage, AutoVendor, AutoXp, AutoTinker
- **UI Tools**: OverlayMap, DungeonMaps, GUI, Nametags, Arrow
- **Information Tools**: Assessor, ItemInfo, Player, GameEvents
- **Management Tools**: InventoryManager, EquipmentManager, FellowshipManager

### Key Architectural Patterns
- **Plugin Pattern**: Main class inherits from Decal plugin base
- **Attribute-Based Commands**: `[CommandPattern]`, `[Usage]`, `[Example]` attributes
- **Expression Language**: Custom scripting with `[ExpressionMethod]` attributes
- **Event-Driven**: Extensive use of Decal event system
- **Modular Design**: Each tool is self-contained with its own settings