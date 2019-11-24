using System;

using Decal.Adapter;
using Decal.Adapter.Wrappers;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Tools;
using UtilityBelt.Views;

namespace UtilityBelt
{
	public static class Globals {
        public static string PluginName { get; private set; }
        public static NetServiceHost Host { get; private set; }
        public static CoreManager Core { get; private set; }

        public static Settings Settings { get; internal set; }

        public static MainView MainView { get; internal set; }
        public static InventoryManager InventoryManager { get; internal set; }
        public static MapView MapView { get; internal set; }
        public static AutoVendor AutoVendor { get; set; }
        public static Misc Misc { get; set; }
        public static Assessor Assessor { get; internal set; }
        public static DungeonMaps DungeonMaps { get; internal set; }
        public static VisualVTankRoutes VisualVTankRoutes { get; internal set; }
        public static DoorWatcher DoorWatcher { get; internal set; }
        public static string AccountName { get; internal set; }
        public static string CharacterName { get; internal set; }
        public static string ServerName { get; internal set; }

        public static void Init(string pluginName, NetServiceHost host, CoreManager core, string account, string character, string server) {
            PluginName = pluginName;
            AccountName = account;
            CharacterName = character;
            ServerName = server;
            Host = host;
            Core = core;

            DoorWatcher = new DoorWatcher();
        }
    }
}
