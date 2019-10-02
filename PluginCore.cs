using System;

using Decal.Adapter;
using Decal.Adapter.Wrappers;
using MyClasses.MetaViewWrappers;
using UtilityBelt.Lib;
using UtilityBelt.Tools;
using UtilityBelt.Views;

namespace UtilityBelt
{

    //Attaches events from core
	[WireUpBaseEvents]
    
	[FriendlyName("UtilityBelt")]
	public class PluginCore : PluginBase {
        private AutoSalvage autoSalvage;
        private DungeonMaps dungeonMaps;
        private EmuConfig emuConfig;
        private QuestTracker questTracker;
        private VisualVTankRoutes visualVTankRoutes;
        private Jumper jumper;
        private Counter counter;
        private ItemGiver itemGiver;
        private VTankFellowHeals vTankFellowHeals;
        private ChatNameClickHandler chatNameClickHandler;
        private DateTime lastThought = DateTime.MinValue;

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup() {
			try {
				Globals.Init("UtilityBelt", Host, Core);
            }
			catch (Exception ex) { Logger.LogException(ex); }
		}

		/// <summary>
		/// This is called when the plugin is shut down. This happens only once.
		/// </summary>
		protected override void Shutdown() {
			try {

			}
			catch (Exception ex) { Logger.LogException(ex); }
		}

		[BaseEvent("LoginComplete", "CharacterFilter")]
		private void CharacterFilter_LoginComplete(object sender, EventArgs e)
		{
			try {
                string configFilePath = System.IO.Path.Combine(Util.GetCharacterDirectory(), "config.xml");

                Mag.Shared.Settings.SettingsFile.Init(configFilePath, Globals.PluginName);

                Util.CreateDataDirectories();
                Logger.Init();

                Globals.Config = new Config();
                Globals.MainView = new MainView();
                Globals.MapView = new MapView();
                Globals.InventoryManager = new InventoryManager();
                Globals.AutoVendor = new AutoVendor();
                Globals.Assessor = new Assessor();

                autoSalvage = new AutoSalvage();
                dungeonMaps = new DungeonMaps();
                emuConfig = new EmuConfig();
                questTracker = new QuestTracker();
                jumper = new Jumper();
                counter = new Counter();
                itemGiver = new ItemGiver();
                visualVTankRoutes = new VisualVTankRoutes();
                vTankFellowHeals = new VTankFellowHeals();
                chatNameClickHandler = new ChatNameClickHandler();

                Globals.Core.RenderFrame += Core_RenderFrame;

                UpdateChecker.CheckForUpdate();
            }
			catch (Exception ex) { Logger.LogException(ex); }
		}

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (vTankFellowHeals != null) vTankFellowHeals.Think();
                if (autoSalvage != null) autoSalvage.Think();
                if (Globals.AutoVendor != null) Globals.AutoVendor.Think();
                if (itemGiver != null) itemGiver.Think();
                if (dungeonMaps != null) dungeonMaps.Think();
                if (jumper != null) jumper.Think();
                if (counter != null) counter.Think();
                if (visualVTankRoutes != null) visualVTankRoutes.Think();

                if (Globals.InventoryManager != null) Globals.InventoryManager.Think();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        [BaseEvent("Logoff", "CharacterFilter")]
		private void CharacterFilter_Logoff(object sender, Decal.Adapter.Wrappers.LogoffEventArgs e)
		{
			try {
                Globals.Core.RenderFrame -= Core_RenderFrame;

                if (autoSalvage != null) autoSalvage.Dispose();
                if (dungeonMaps != null) dungeonMaps.Dispose();
                if (emuConfig != null) emuConfig.Dispose();
                if (questTracker != null) questTracker.Dispose();
                if (jumper != null) jumper.Dispose();
                if (counter != null) counter.Dispose();
                if (itemGiver != null) itemGiver.Dispose();
                if (visualVTankRoutes != null) visualVTankRoutes.Dispose();
                if (vTankFellowHeals != null) vTankFellowHeals.Dispose();
                if (chatNameClickHandler != null) chatNameClickHandler.Dispose();
                if (Globals.AutoVendor != null) Globals.AutoVendor.Dispose();
                if (Globals.Assessor != null) Globals.Assessor.Dispose();
                if (Globals.InventoryManager != null) Globals.InventoryManager.Dispose();
                if (Globals.MapView != null) Globals.MapView.Dispose();
                if (Globals.MainView != null) Globals.MainView.Dispose();
                if (Globals.Config != null) Globals.Config.Dispose();
            }
			catch (Exception ex) { Logger.LogException(ex); }
		}
	}
}
