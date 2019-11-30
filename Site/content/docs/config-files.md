---
title: Configuration Files
---


### UtilityBelt.dll.config
This is an (optional) XML file, that goes in the same folder as UtilityBelt.dll. It allows you to change the location that all of the other UtilityBelt files are read from/saved to. Default is `%USERPROFILE%\Documents\Decal Plugins\UtilityBelt`
Example:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <appSettings>
        <add key="PluginDirectory" value="C:\Projects\UtilityBelt\bin" />
    </appSettings>
</configuration>
```



### settings.default.json
This is an (optional) JSON file, that goes in the same folder as UtilityBelt.dll. This file is always loaded first, and allows you to set defaults for new characters. The format is the same as the character-specific settings.json below.



### settings.json
This a JSON file, that saves character specific settings. It is located at `<PluginDirectory>\<Server Name>\<Character Name>\settings.json`
Example:
```json
{
  "Plugin": {
    "CheckForUpdates": true,
    "Debug": true,
    "WindowPositionX": 220,
    "WindowPositionY": 388,
    "PortalThink": true,
    "PortalTimeout": 5000,
    "PortalAttempts": 3,
    "VideoPatch": false,
    "PCap": true,
    "PCapBufferDepth": 5000
  },
  "Assessor": {},
  "AutoImbue": {},
  "AutoSalvage": {
    "Think": true,
    "OnlyFromMainPack": false
  },
  "AutoTinker": {
    "MinPercentage": 0.0
  },
  "AutoTrade": {
    "Enabled": false,
    "TestMode": false,
    "Think": false,
    "OnlyFromMainPack": false,
    "AutoAccept": false,
    "AutoAcceptChars": []
  },
  "AutoVendor": {
    "Enabled": true,
    "Think": false,
    "TestMode": false,
    "ShowMerchantInfo": true,
    "OnlyFromMainPack": false,
    "Tries": 4,
    "TriesTime": 5000
  },
  "ChatLogger": {
    "SaveToFile": true,
    "Rules": [
      {
        "Types": [
          "Tell"
        ],
        "MessageFilter": ".*"
      }
    ]
  },
  "Counter": {},
  "DoorWatcher": {},
  "DungeonMaps": {
    "Enabled": true,
    "DrawWhenClosed": true,
    "ShowVisitedTiles": true,
    "ShowCompass": true,
    "Opacity": 16,
    "MapWindowX": 40,
    "MapWindowY": 150,
    "MapWindowWidth": 300,
    "MapWindowHeight": 280,
    "MapZoom": 3.24999952,
    "Display": {
      "Walls": {
        "Enabled": true,
        "Color": -16714113
      },
      "InnerWalls": {
        "Enabled": true,
        "Color": -8404993
      },
      "RampedWalls": {
        "Enabled": true,
        "Color": -11622657
      },
      "Stairs": {
        "Enabled": true,
        "Color": -16760961
      },
      "Floors": {
        "Enabled": true,
        "Color": -16744513
      },
      "VisualNavStickyPoint": {
        "Enabled": true,
        "Color": -5374161
      },
      "VisualNavLines": {
        "Enabled": true,
        "Color": -65281
      },
      "Markers": {
        "You": {
          "Enabled": true,
          "ShowLabel": false,
          "UseIcon": true,
          "Color": -65536,
          "Size": 3
        },
        "Others": {
          "Enabled": true,
          "ShowLabel": true,
          "UseIcon": true,
          "Color": -1,
          "Size": 3
        },
        "Items": {
          "Enabled": true,
          "ShowLabel": true,
          "UseIcon": true,
          "Color": -1,
          "Size": 3
        },
        "Monsters": {
          "Enabled": true,
          "ShowLabel": false,
          "UseIcon": true,
          "Color": -23296,
          "Size": 3
        },
        "NPCs": {
          "Enabled": true,
          "ShowLabel": false,
          "UseIcon": true,
          "Color": -256,
          "Size": 3
        },
        "MyCorpse": {
          "Enabled": true,
          "ShowLabel": true,
          "UseIcon": true,
          "Color": -65536,
          "Size": 3
        },
        "OtherCorpses": {
          "Enabled": false,
          "ShowLabel": false,
          "UseIcon": true,
          "Color": -657931,
          "Size": 3
        },
        "Portals": {
          "Enabled": true,
          "ShowLabel": true,
          "UseIcon": true,
          "Color": -3841,
          "Size": 3
        },
        "Containers": {
          "Enabled": true,
          "ShowLabel": false,
          "UseIcon": true,
          "Color": -744352,
          "Size": 3
        },
        "Doors": {
          "Enabled": true,
          "ShowLabel": false,
          "UseIcon": false,
          "Color": -5952982,
          "Size": 3
        },
        "EverythingElse": {
          "Enabled": false,
          "ShowLabel": false,
          "UseIcon": true,
          "Color": -657931,
          "Size": 3
        }
      }
    }
  },
  "EquipmentManager": {
    "Think": false
  },
  "InventoryManager": {
    "AutoCram": false,
    "AutoStack": false,
    "IGThink": false,
    "IGFailure": 3,
    "IGBusyCount": 10,
    "IGRange": 15.0,
    "TreatStackAsSingleItem": true,
    "WatchLootProfile": false
  },
  "Jumper": {
    "PauseNav": true,
    "ThinkComplete": false,
    "ThinkFail": false,
    "Attempts": 3
  },
  "Nametags": {
    "Enabled": true,
    "MaxRange": 35.0,
    "Player": {
      "Enabled": true,
      "Color": -16711681
    },
    "Portal": {
      "Enabled": true,
      "Color": -16711936
    },
    "Npc": {
      "Enabled": true,
      "Color": -256
    },
    "Vendor": {
      "Enabled": true,
      "Color": -65281
    },
    "Monster": {
      "Enabled": true,
      "Color": -65536
    }
  },
  "QuestTracker": {},
  "VisualNav": {
    "Enabled": true,
    "ScaleCurrentWaypoint": true,
    "LineOffset": 0.05,
    "SaveNoneRoutes": false,
    "Display": {
      "Lines": {
        "Enabled": true,
        "Color": -65281
      },
      "ChatText": {
        "Enabled": true,
        "Color": -1
      },
      "JumpText": {
        "Enabled": true,
        "Color": -1
      },
      "JumpArrow": {
        "Enabled": true,
        "Color": -256
      },
      "OpenVendor": {
        "Enabled": true,
        "Color": -1
      },
      "Pause": {
        "Enabled": true,
        "Color": -1
      },
      "Portal": {
        "Enabled": true,
        "Color": -1
      },
      "Recall": {
        "Enabled": true,
        "Color": -1
      },
      "UseNPC": {
        "Enabled": true,
        "Color": -1
      },
      "FollowArrow": {
        "Enabled": true,
        "Color": -23296
      },
      "CurrentWaypoint": {
        "Enabled": true,
        "Color": -7722014
      }
    }
  },
  "VTank": {
    "VitalSharing": true
  },
  "VTankFellowHeals": {}
}
```