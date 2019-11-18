using System;

using Decal.Adapter;
using Decal.Adapter.Wrappers;
using MyClasses.MetaViewWrappers;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Tools;
using UtilityBelt.Views;

namespace UtilityBelt
{

    //Attaches events from core
	[WireUpBaseEvents]
    
	[FriendlyName("UtilityBelt")]
	public class PluginCore : PluginBase {
        private AutoSalvage autoSalvage;
        private AutoTrade autoTrade;

        //private EmuConfig emuConfig;
        private QuestTracker questTracker;
        private Jumper jumper;
        private Counter counter;
        private VTankFellowHeals vTankFellowHeals;
        private ChatNameClickHandler chatNameClickHandler;
        private ChatLogger chatLogger;

        public PluginCore() : base() {
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());

            // if you add a new embedded assembly, you should load it here.
            System.Reflection.Assembly.Load((byte[])rm.GetObject("UBHelper"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("Newtonsoft_Json"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("SharedMemory"));
        }

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup() {
			try {
                Globals.Init("UtilityBelt", Host, Core);
                Util.Init(); //static classes can not have constructors, but still need to init variables.

                UBHelper.Core.Startup();
            }
			catch (Exception ex) { Logger.LogException(ex); }
        }

        /// <summary>
        /// This is called when the plugin is shut down. This happens only once.
        /// </summary>
        protected override void Shutdown() {
			try {
                UBHelper.Core.Shutdown();
            }
			catch (Exception ex) { Logger.LogException(ex); }
        }

        [BaseEvent("LoginComplete", "CharacterFilter")]
		private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
			try {
                Util.CreateDataDirectories();
                Logger.Init();
                Globals.Settings = new Settings();

                Logger.Debug($"UB Initialized {DateTime.UtcNow} v{Util.GetVersion(true)}");

                VTankControl.initializeVTankInterface();
                
                Globals.MainView = new MainView();
                Globals.MapView = new MapView();
                Globals.InventoryManager = new InventoryManager();
                Globals.AutoVendor = new AutoVendor();
                Globals.Assessor = new Assessor();
                Globals.DungeonMaps = new DungeonMaps();
                Globals.VisualVTankRoutes = new VisualVTankRoutes();

                autoSalvage = new AutoSalvage();
                autoTrade = new AutoTrade();
                //emuConfig = new EmuConfig();
                questTracker = new QuestTracker();
                Globals.Misc = new Misc();
                jumper = new Jumper();
                counter = new Counter();
                vTankFellowHeals = new VTankFellowHeals();
                chatNameClickHandler = new ChatNameClickHandler();
                chatLogger = new ChatLogger();

                Globals.Core.RenderFrame += Core_RenderFrame;


                if (Globals.Settings.Plugin.CheckForUpdates) {
                    UpdateChecker.CheckForUpdate();
                }
            }
			catch (Exception ex) { Logger.LogException(ex); }
		}

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (vTankFellowHeals != null) vTankFellowHeals.Think();
                if (autoSalvage != null) autoSalvage.Think();
                if (Globals.AutoVendor != null) Globals.AutoVendor.Think();
                if (autoTrade != null) autoTrade.Think();
                if (Globals.Misc != null) Globals.Misc.Think();
                if (Globals.DungeonMaps != null) Globals.DungeonMaps.Think();
                if (jumper != null) jumper.Think();
                if (counter != null) counter.Think();
                if (Globals.VisualVTankRoutes != null) Globals.VisualVTankRoutes.Think();
                if (Globals.Assessor != null) Globals.Assessor.Think();
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
                if (autoTrade != null) autoTrade.Dispose();
                if (Globals.DungeonMaps != null) Globals.DungeonMaps.Dispose();
                //if (emuConfig != null) emuConfig.Dispose();
                if (questTracker != null) questTracker.Dispose();
                if (Globals.Misc != null) Globals.Misc.Dispose();
                if (jumper != null) jumper.Dispose();
                if (counter != null) counter.Dispose();
                if (Globals.VisualVTankRoutes != null) Globals.VisualVTankRoutes.Dispose();
                if (vTankFellowHeals != null) vTankFellowHeals.Dispose();
                if (chatNameClickHandler != null) chatNameClickHandler.Dispose();
                if (Globals.AutoVendor != null) Globals.AutoVendor.Dispose();
                if (Globals.Assessor != null) Globals.Assessor.Dispose();
                if (Globals.InventoryManager != null) Globals.InventoryManager.Dispose();
                if (Globals.MapView != null) Globals.MapView.Dispose();
                if (Globals.MainView != null) Globals.MainView.Dispose();
                if (chatLogger != null) chatLogger.Dispose();
            }
			catch (Exception ex) { Logger.LogException(ex); }
		}
	}
}
