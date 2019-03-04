using System;

using Decal.Adapter;
using Decal.Adapter.Wrappers;
using MyClasses.MetaViewWrappers;
using UtilityBelt.Tools;
using UtilityBelt.Views;

namespace UtilityBelt
{

    //Attaches events from core
	[WireUpBaseEvents]
    
	[FriendlyName("UtilityBelt")]
	public class PluginCore : PluginBase {
        private AutoVendor autoVendor;
        private AutoSalvage autoSalvage;
        private DungeonMaps dungeonMaps;
        private EmuConfig emuConfig;
        private QuestTracker questTracker;
        private DateTime lastThought = DateTime.MinValue;

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup() {
			try {
				Globals.Init("UtilityBelt", Host, Core);
            }
			catch (Exception ex) { Util.LogException(ex); }
		}

		/// <summary>
		/// This is called when the plugin is shut down. This happens only once.
		/// </summary>
		protected override void Shutdown() {
			try {

			}
			catch (Exception ex) { Util.LogException(ex); }
		}

		[BaseEvent("LoginComplete", "CharacterFilter")]
		private void CharacterFilter_LoginComplete(object sender, EventArgs e)
		{
			try {
                string configFilePath = System.IO.Path.Combine(Util.GetCharacterDirectory(), "config.xml");

                Mag.Shared.Settings.SettingsFile.Init(configFilePath, Globals.PluginName);

                Util.CreateDataDirectories();

                Globals.Config = new Config();
                Globals.MainView = new MainView();
                Globals.MapView = new MapView();
                Globals.InventoryManager = new InventoryManager();

                autoVendor = new AutoVendor();
                autoSalvage = new AutoSalvage();
                dungeonMaps = new DungeonMaps();
                emuConfig = new EmuConfig();
                questTracker = new QuestTracker();

                Globals.Core.RenderFrame += Core_RenderFrame;
            }
			catch (Exception ex) { Util.LogException(ex); }
		}

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (autoSalvage != null) autoSalvage.Think();
                if (autoVendor != null) autoVendor.Think();
                if (dungeonMaps != null) dungeonMaps.Think();
                if (Globals.InventoryManager != null) Globals.InventoryManager.Think();
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        [BaseEvent("Logoff", "CharacterFilter")]
		private void CharacterFilter_Logoff(object sender, Decal.Adapter.Wrappers.LogoffEventArgs e)
		{
			try {
                Globals.Core.RenderFrame -= Core_RenderFrame;

                if (autoVendor != null) autoVendor.Dispose();
                if (autoSalvage != null) autoSalvage.Dispose();
                if (dungeonMaps != null) dungeonMaps.Dispose();
                if (emuConfig != null) emuConfig.Dispose();
                if (questTracker != null) questTracker.Dispose();
                if (Globals.InventoryManager != null) Globals.InventoryManager.Dispose();
                if (Globals.MapView != null) Globals.MapView.Dispose();
                if (Globals.MainView != null) Globals.MainView.Dispose();
                if (Globals.Config != null) Globals.Config.Dispose();
            }
			catch (Exception ex) { Util.LogException(ex); }
		}
	}
}
