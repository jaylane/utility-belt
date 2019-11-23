using System;

using Decal.Adapter;
using Decal.Adapter.Wrappers;
using MyClasses.MetaViewWrappers;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Tools;
using UtilityBelt.Views;

namespace UtilityBelt {
	public class UtilityBeltPlugin {
        private AutoSalvage autoSalvage;
        private AutoTrade autoTrade;

        //private EmuConfig emuConfig;
        private QuestTracker questTracker;
        private Jumper jumper;
        private Counter counter;
        private VTankFellowHeals vTankFellowHeals;
        private ChatNameClickHandler chatNameClickHandler;
        private AutoTinker autotinker;
        private AutoImbue autoimbue;
        private ChatLogger chatLogger;
        private EquipmentManager equipmentManager;

        public UtilityBeltPlugin() {
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());

            // if you add a new embedded assembly, you should load it here.
            System.Reflection.Assembly.Load((byte[])rm.GetObject("UBHelper"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("Newtonsoft_Json"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("SharedMemory"));
        }

        public void Startup(string assemblyLocation, NetServiceHost Host, CoreManager Core) {
			try {
                Util.AssemblyLocation = assemblyLocation;
                Globals.Init("UtilityBelt", Host, Core);
                Util.Init(); //static classes can not have constructors, but still need to init variables.

                UBHelper.Core.Startup();

                Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
            }
			catch (Exception ex) { Logger.LogException(ex); }
        }

		private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
			try {
                Init();
            }
			catch (Exception ex) { Logger.LogException(ex); }
		}

        public void Init() {
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
            equipmentManager = new EquipmentManager();
            autotinker = new AutoTinker();
            autoimbue = new AutoImbue();

            Nametags.Init(); // static class

            Globals.Core.RenderFrame += Core_RenderFrame;

            if (Globals.Settings.Plugin.CheckForUpdates) {
                Util.WriteToChat("Init calling check for update");
                UpdateChecker.CheckForUpdate();
            }
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
                if (autotinker != null) autotinker.Think();
                if (autoimbue != null) autoimbue.Think();
                if (Globals.VisualVTankRoutes != null) Globals.VisualVTankRoutes.Think();
                if (Globals.Assessor != null) Globals.Assessor.Think();
                if (Globals.InventoryManager != null) Globals.InventoryManager.Think();
                if (Globals.InventoryManager != null) Globals.InventoryManager.Think();
                if (equipmentManager != null) equipmentManager.Think();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Shutdown() {
            try {
                Globals.Core.RenderFrame -= Core_RenderFrame;
                Globals.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;

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
                if (equipmentManager != null) equipmentManager.Dispose();
                if (autotinker != null) autotinker.Dispose();
                if (autoimbue != null) autoimbue.Dispose();
                Nametags.Dispose();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
    }
}
