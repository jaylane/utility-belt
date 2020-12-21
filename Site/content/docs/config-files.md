
---
title: Configuration Files
---

### Setting Types
Settings can be one of the following types:
- **Global** - Stores things needed before the main plugin is initialized. Shared between all clients.
- **Profile** - Stores the main plugin settings. These can be shared by multiple characters through the use of profiles. By default they will be stored per character until a new profile is selected.
- **State** - Stores character specific settings like plugin window locations or arrow target coordinates. These will never be shared between characters.

Global and shared profile settings will automatically update on all open clients when changed from any other client (or the file contents change). Individual settings will only be serialized to disk if they have changed.

### Global Settings
This is an (optional) settings file, that goes in the same folder as UtilityBelt.dll. It allows you to change the location that all of the other UtilityBelt files are read from/saved to. Default is `%USERPROFILE%\Documents\Decal Plugins\UtilityBelt`.  You can also specify a `DataBaseFile` path to customize where the database will be saved.

### Default Profile Settings
To use default profile settings, create a file called `settings.default.json`. This is an (optional) JSON file, that goes in the same folder as UtilityBelt.dll. This file is always loaded first, and allows you to set defaults for new characters. The format is the same as the character-specific settings.json below.

### Profile Settings
This a JSON file, that saves character specific settings. When the currently selected profile is set to `[character]`, it is located at `<PluginDirectory>\<Server Name>\<Character Name>\settings.json`. When set to `[character]` these settings will be specific to the logged in character.  You can choose to load a shared profile as well. Shared profiles are stored in `<PluginDirectory>\profiles\*.settings.json`.

### State Settings (state.json)
This a JSON file, that saves character state specific settings. It is located at `<PluginDirectory>\<Server Name>\<Character Name>\state.json`. These settings will never be shared between characters.