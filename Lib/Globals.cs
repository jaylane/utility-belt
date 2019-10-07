using System;

using Decal.Adapter;
using Decal.Adapter.Wrappers;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Tools;
using UtilityBelt.Views;

namespace UtilityBelt
{
	public static class Globals {
        public static string PluginName { get; private set; }
        public static PluginHost Host { get; private set; }
        public static CoreManager Core { get; private set; }

        public static Settings Settings { get; internal set; }

        public static MainView MainView { get; internal set; }
        public static InventoryManager InventoryManager { get; internal set; }
        public static MapView MapView { get; internal set; }
        public static AutoVendor AutoVendor { get; set; }
        public static Assessor Assessor { get; internal set; }
        public static DungeonMaps DungeonMaps { get; internal set; }
        public static VisualVTankRoutes VisualVTankRoutes { get; internal set; }

        public static void Init(string pluginName, PluginHost host, CoreManager core) {
			PluginName = pluginName;
			Host = host;
			Core = core;
        }
    }
}
