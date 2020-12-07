using System;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using UtilityBelt.Lib.Salvage;
using VirindiViewService.Controls;
using System.Data;
using Decal.Filters;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Tinker;
using System.ComponentModel;
using UBHelper;

namespace UtilityBelt.Tools {
    [Name("AutoTinker")]
    [Summary("Provides a UI for automatically applying salvage to weapons/armor.")]
    [FullDescription(@"
This tool provides a UI for automatically applying tinkering salvage and imbue salvage to weapons and armor. It will choose the best (lowest workmanship) salvage and it to the target item, that still gives a percentage over [AutoTinker.MinPercent](/docs/tools/autotinker/#autotinker-minpercentage).

### Tinker Tab

This can apply specific salvage, or for melee weapons it can apply the best granite/iron option.  The math here follows endy's tinking calculator and selects highest max damage possible.

![](/screenshots/AutoTinker/autotinker.png)

1. Select an item in your inventory you want to tinker.
2. Set [AutoTinker.MinPercent](/docs/tools/autotinker/#autotinker-minpercentage) 
3. Choose a salvage type from the dropdown.
4. Click the populate button.
5. If it looks good, hit Start.


### Imbue Tab

This is capable of bulk applying imbue salvage to items.

![](/screenshots/AutoTinker/autoimbue.png)

1. Select the appropriate damage and salvage combo
2. Select refresh
3. Hit Start
OR 
1. Hit the Rend All button
2. Hit Start

The Rend All button will automatically do the following:

* Apply black opal to nether and normal wands (including no wield wands)
* Apply imperial topaz to slash weapons and combo slash/pierce weapons
* Apply black garnet to pierce weapons
* Apply white sapphire to bludge weapons
* Apply jet to electric weapons
* Apply red garnet to fire weapons
* Apply emerald to acid weapons
* Apply aquamarine to cold weapons



    ")]
    public class AutoTinker : ToolBase {

        //AutoTinker Buttons
        readonly HudButton AutoTinkAddSelectedButton;
        readonly HudList AutoTinkerList;
        readonly HudButton AutoTinkStartButton;
        readonly HudButton AutoTinkStopButton;
        readonly HudButton PopulateListButton;
        readonly HudStaticText AutoTinkItemNameLabel;
        readonly HudCombo AutoTinkCombo;
        readonly HudCombo AutoTinkMaxTinksCombo;
        readonly HudTextBox AutoTinkMinPercentTextBox;

        //AutoImbue Stuff
        readonly HudList AutoImbueList;
        readonly HudButton AutoImbueStartButton;
        readonly HudButton AutoImbueStopButton;
        readonly HudCombo AutoImbueSalvageCombo;
        readonly HudCombo AutoImbueDmgTypeCombo;
        readonly HudButton AutoImbueRefreshListButton;
        readonly HudButton AutoImbueAllButton;
        readonly Dictionary<string, int> DefaultImbueList = new Dictionary<string, int>();

        //private readonly TinkerJob tinkerJob = new TinkerJob();
        internal DataTable tinkerDT = new DataTable();
        internal DataTable TinkerList = new DataTable();

        private bool waitingForIds = false;
        private List<int> itemsToId = new List<int>();
        private readonly List<int> salvageToBeApplied = new List<int>();
        private readonly List<int> PossibleMaterialList = new List<int>();
        private readonly List<int> SalvageUsedList = new List<int>();
        private List<int> RescanList = new List<int>();
        private readonly Dictionary<int, double> FullSalvageDict = new Dictionary<int, double>();
        private readonly Dictionary<int, double> TinkerableItemDict = new Dictionary<int, double>();
        readonly List<string> dmgList = new List<string>();
        string runType;
        private bool isSelecting = false;
        private bool isRunning = false;
        private bool isPopulating = false;
        WorldObject storedTargetItem;
        readonly TinkerCalc tinkerCalc = new TinkerCalc();
        private TinkerJobManager tinkerJobManager = new TinkerJobManager();
        private  TinkerJob tinkerJob = new TinkerJob();
        readonly Dictionary<string, int> AutoTinkerSalvageList = new Dictionary<string, int>();
        readonly Dictionary<string, int> AutoImbueSalvageList = new Dictionary<string, int>();
        readonly Dictionary<int, double> PotentialSalvageList = new Dictionary<int, double>();
        readonly List<string> ComboBoxList = new List<string>();
        int weaponTinkeringSkill;
        int magicItemTinkeringSkill;
        int armorTinkeringSkill;
        int itemTinkeringSkill;

        #region Config
        [Summary("Minimum percentage required to perform tinker")]
        [DefaultValue(99.5f)]
        public float MinPercentage {
            get { return (float)GetSetting("MinPercentage"); }
            set { UpdateSetting("MinPercentage", value); }
        }

        #endregion

        //        #region /ub autotinker
        //        [Summary("starts autotinker")]
        //        [Usage("/ub autotinker")]
        //        //[Usage("/ub autotinker")]
        //        [CommandPattern("autotinker", @"^$")]
        //        public void StartAutoTink(string cmd, Match args) {
        //            try {
        //                ClearAllTinks();
        //                //ScanInventory(); 
        //                new Assessor.Job(UB.Assessor, ref itemsToId, (_) => { }, () => {
        //                    runType = "multi";
        //                    //Start(runType);
        //                });
        //            }
        //            catch (Exception ex) {
        //                Logger.WriteToChat(ex.Message);
        //            }
        //        }
        //        #endregion

        #region /ub getjob
        [Summary("run this command to see tinker jobs currently in queue")]
        [Usage("/ub getjob")]
        [CommandPattern("getjob", @"^ *(?<nothing>.*)$")]
        public void GetJob(string cmd, Match args) {
            try {
                tinkerJobManager.GetJobs();
            }
            catch (Exception ex) {
                Logger.WriteToChat(ex.Message);
            }
        }
        #endregion


        #region /ub tinkcalc
        [Summary("select an item and run this command to see best iron/granite combination")]
        [Usage("/ub tinkcalc")]
        [CommandPattern("tinkcalc", @"^ *(?<nothing>.*)$")]
        public void GetDifficulty(string cmd, Match args) {
            try {
                    List<int> scanItem = new List<int>();
                    storedTargetItem = CoreManager.Current.WorldFilter[CoreManager.Current.Actions.CurrentSelection];
                    Logger.WriteToChat(storedTargetItem.Name);
                    scanItem.Add(storedTargetItem.Id);

                    new Assessor.Job(UB.Assessor, ref scanItem, (_) => { }, () => {
                        isSelecting = false;
                        storedTargetItem = CoreManager.Current.WorldFilter[CoreManager.Current.Actions.CurrentSelection];
                        CalculateBestDamage(storedTargetItem);
                    });

                //TinkerJobManager.GetGraniteIron(double maxDamage, double variance) {


                //tinkerJobManager.ScanInventory();


                //int diff = tinkerCalc.GetDifficulty(.99, 67);
                //int count = 0;
                //
                //foreach (int m in salvageToBeApplied) {
                //    tinkCount++;
                //    //Logger.WriteToChat("tink #: " + tinkCount);
                //    tinkerCalc.GetRequiredSalvage(diff, m, storedTargetItem, tinkCount);
                //}
            }
            catch (Exception ex) {
                Logger.WriteToChat(ex.Message);
            }
        }
        #endregion

        public AutoTinker(UtilityBeltPlugin ub, string name) : base(ub, name) {
            try {
                AutoTinkerList = (HudList)UB.MainView.view["AutoTinkerList"];
                AutoTinkerList.Click += AutoTinkerList_Click;

                AutoTinkCombo = (HudCombo)UB.MainView.view["AutoTinkCombo"];

                AutoTinkMaxTinksCombo = (HudCombo)UB.MainView.view["AutoTinkMaxTinksCombo"];
                AutoTinkMaxTinksCombo.Change += AutoTinkMaxTinksCombo_Change;

                AutoTinkAddSelectedButton = (HudButton)UB.MainView.view["AutoTinkAddSelectedButton"];
                AutoTinkAddSelectedButton.Hit += AutoTinkAddSelectedButton_Hit;

                AutoTinkStartButton = (HudButton)UB.MainView.view["AutoTinkStartButton"];
                AutoTinkStartButton.Hit += AutoTinkStartButton_Hit;

                AutoTinkStopButton = (HudButton)UB.MainView.view["AutoTinkStopButton"];
                AutoTinkStopButton.Hit += AutoTinkStopButton_Hit;

                PopulateListButton = (HudButton)UB.MainView.view["PopulateListButton"];
                PopulateListButton.Hit += PopulateListButton_Hit;

                AutoTinkMinPercentTextBox = (HudTextBox)UB.MainView.view["AutoTinkMinPercentTextBox"];

                AutoTinkMinPercentTextBox.Change += AutoTinkMinPercentTextBox_Changed;
                AutoTinkMinPercentTextBox.Text = MinPercentage.ToString();

                AutoTinkItemNameLabel = (HudStaticText)UB.MainView.view["AutoTinkItemNameLabel"];


                AutoImbueList = (HudList)UB.MainView.view["AutoImbueList"];
                AutoImbueList.Click += AutoImbueList_Click;

                AutoImbueSalvageCombo = (HudCombo)UB.MainView.view["AutoImbueSalvageCombo"];
                AutoImbueSalvageCombo.Change += AutoImbueSalvageCombo_Change;

                AutoImbueDmgTypeCombo = (HudCombo)UB.MainView.view["AutoImbueDmgTypeCombo"];
                AutoImbueDmgTypeCombo.Change += AutoImbueDmgTypeCombo_Change;

                AutoImbueStartButton = (HudButton)UB.MainView.view["AutoImbueStartButton"];
                AutoImbueStartButton.Hit += AutoImbueStartButton_Hit;

                AutoImbueStopButton = (HudButton)UB.MainView.view["AutoImbueStopButton"];
                AutoImbueStopButton.Hit += AutoImbueStopButton_Hit;

                AutoImbueRefreshListButton = (HudButton)UB.MainView.view["AutoImbueRefreshListButton"];
                AutoImbueRefreshListButton.Hit += AutoImbueRefreshListButton_Hit;

                AutoImbueAllButton = (HudButton)UB.MainView.view["AutoImbueAllButton"];
                AutoImbueAllButton.Hit += AutoImbueAllButton_Hit;

                PopulateAutoTinkCombo();

                tinkerCalc.BuildDifficultyTable();

                UB.Core.RenderFrame += Core_RenderFrame;

                PopulateAutoTinkMaxTinksCombo();

                PopulateAutoImbueSalvageCombo();
                PopulateAutoImbueDmgTypeCombo();
                DefaultImbueList = tinkerJobManager.BuildDefaultImbueList();

                HudStaticText c = (HudStaticText)(AutoImbueDmgTypeCombo[AutoImbueDmgTypeCombo.Current]);
                SelectDefaultSalvage(c.Text.ToString());


            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private void SelectDefaultSalvage(string dmgType) {
            FileService service = CoreManager.Current.Filter<FileService>();

            foreach (KeyValuePair<string, int> row in DefaultImbueList) {
                if (dmgType == row.Key.ToString()) {
                    var selectSalvage = service.MaterialTable[row.Value];
                    for (int r = 0; r < AutoImbueSalvageCombo.Count; r++) {
                        HudStaticText sal = (HudStaticText)(AutoImbueSalvageCombo[r]);
                        if (sal.Text.ToString() == selectSalvage.ToString()) {
                            AutoImbueSalvageCombo.Current = r;
                        }
                    }
                }
            }
        }

        public void CalculateBestDamage(WorldObject item) {
            Logger.Debug("test");
            double fakeMaxDamage = 0;
            double fakeVariance = 0;
            double finalMaxDamage = 0;
            int granite = 0;
            int iron = 0;
            int tinkCount = item.Values(LongValueKey.NumberTimesTinkered);
            if (item.ObjectClass == ObjectClass.MeleeWeapon) {
                double maxDamage = tinkerJobManager.GetMaxDamage(item);
                double variance = item.Values(DoubleValueKey.Variance);
                for (int i = tinkCount + 1; i <= 10; i++) {
                    if (fakeMaxDamage == 0) {
                        fakeMaxDamage = maxDamage;
                    }
                    if (fakeVariance == 0) {
                        fakeVariance = variance;
                    }
                    double ironDPS = GetDPS(fakeMaxDamage + 1, fakeVariance);
                    double graniteDPS = GetDPS(fakeMaxDamage, fakeVariance * .8);
                    LogDebug("Dmg with Iron: " + ironDPS.ToString());
                    LogDebug("Dmg with Granite: " + graniteDPS.ToString());
                    if (ironDPS >= graniteDPS) {
                        finalMaxDamage = ironDPS;
                        fakeMaxDamage = fakeMaxDamage + 1;
                        iron++;
                    }
                    else if (graniteDPS > ironDPS) {
                        finalMaxDamage = graniteDPS;
                        fakeVariance = fakeVariance * .8;
                        granite++;
                    }
                }
                Logger.WriteToChat("final max damage: " + finalMaxDamage);
                Logger.WriteToChat("granite: " + granite.ToString());
                Logger.WriteToChat("iron: " + iron.ToString());
            }
        }

        public void AutoImbueSalvageCombo_Change(object sender, EventArgs e) {
            try {
                PopulateAutoImbue();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void AutoTinkMaxTinksCombo_Change(object sender, EventArgs e) {
            try {
                Stop();
                if (storedTargetItem == null) {
                    //Logger.WriteToChat("select an item first");
                    return;
                }
                runType = "single";
                SetTinkerSkills();
                if (isPopulating) {
                    Logger.WriteToChat("is populating is running");
                    return;
                }
                else {
                    tinkerJobManager.Stop();
                    isPopulating = true;
                    List<int> rescanItem = new List<int>();
                    List<int> matchingMaterial = new List<int>();
                    AutoTinkerList.ClearRows();
                    tinkerJobManager.ScanInventory();

                    string selectedItem = AutoTinkItemNameLabel.Text.ToString();


                    List<string> usableSalvageOnItem = new List<string>();
                    if (tinkerJobManager.CanBeTinkered(storedTargetItem)) {
                        UpdateNameLabel(storedTargetItem.Id);
                        usableSalvageOnItem = tinkerJobManager.GetUsableSalvage(storedTargetItem.Id);
                        FilterSalvageCombo(usableSalvageOnItem);
                        usableSalvageOnItem = usableSalvageOnItem.Distinct().ToList();

                        tinkerJobManager.CreatePossibleMaterialList(usableSalvageOnItem, false);

                        if (selectedItem == "[None]") {
                            Logger.WriteToChat("Select an item first");
                            return;
                        }
                    }


                    float.TryParse(AutoTinkMinPercentTextBox.Text, out float minPercent);
                    //Logger.WriteToChat(minPercent.ToString());

                    rescanItem.Add(storedTargetItem.Id);


                    HudStaticText c = (HudStaticText)(AutoTinkCombo[AutoTinkCombo.Current]);
                    string currentSalvageChoice = c.Text.ToString();
                    HudStaticText maxTinksString = (HudStaticText)(AutoTinkMaxTinksCombo[AutoTinkMaxTinksCombo.Current]);
                    int.TryParse(maxTinksString.Text, out int maxTinks);

                    new Assessor.Job(UB.Assessor, ref rescanItem, (_) => { }, () => {
                        tinkerJobManager.TinkerListFinished += TinkerList_Finished;
                        tinkerJobManager.TinkerListChanged += TinkerList_Changed;
                        //tinkerJobManager.PopulateTinkerList();
                        tinkerJobManager.BuildSalvageToBeApplied(storedTargetItem, currentSalvageChoice, runType, maxTinks);
                        //tinkerJobManager.WriteSalvageToBeApplied();
                        tinkerJobManager.BuildTinkerList(storedTargetItem, minPercent, "single", maxTinks);
                        rescanItem.Clear();
                    });
                    tinkerJob.minPercent = minPercent;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }


        public void AutoImbueDmgTypeCombo_Change(object sender, EventArgs e) {
            try {
                HudStaticText dmgType = (HudStaticText)(AutoImbueDmgTypeCombo[AutoImbueDmgTypeCombo.Current]);
                SelectDefaultSalvage(dmgType.Text.ToString());
                PopulateAutoImbue();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void PopulateAutoImbue() {
            if (isPopulating) return;
            isPopulating = true;
            tinkerJobManager.ClearAll();
            AutoImbueList.ClearRows();
            List<string> salList = new List<string>();
            HudStaticText dmgType = (HudStaticText)(AutoImbueDmgTypeCombo[AutoImbueDmgTypeCombo.Current]);
            HudStaticText salType = (HudStaticText)(AutoImbueSalvageCombo[AutoImbueSalvageCombo.Current]);
            salList.Add(salType.Text);

            ClearAllTinks();
            ScanInventory();
            tinkerJobManager.ScanInventory();
            new Assessor.Job(UB.Assessor, ref itemsToId, (_) => { }, () => {
                tinkerJobManager.AutoImbueListFinished += AutoImbueList_Finished;
                tinkerJobManager.AutoImbueListChanged += AutoImbueList_Changed;
                Logger.WriteToChat("done scanning");
                tinkerJobManager.CreatePossibleMaterialList(salList, false);
                tinkerJobManager.CreateTinkerableItemsList(TinkerableItemDict, dmgType.Text, salType.Text);
                itemsToId.Clear();
                isPopulating = false;
            });


        }

        private void AutoImbueRefreshListButton_Hit(object sender, EventArgs e) {
            try {
                Logger.Debug("AutoImbueRefreshListButton_Hit");
                if (!isRunning) {
                    Logger.WriteToChat("Be patient... Already made your life easy enough");
                    SetTinkerSkills();
                    PopulateAutoImbue();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void AutoImbueList_Click(object sender, int row, int col) {
            try {
                HudList.HudListRowAccessor imbueListRow = AutoImbueList[row];
                HudStaticText itemID = (HudStaticText)imbueListRow[5];
                int.TryParse(itemID.Text.ToString(), out int item);
                CoreManager.Current.Actions.SelectItem(item);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void AutoImbueStartButton_Hit(object sender, EventArgs e) {
            try {
                Logger.Debug("AutoImbueStartButton_Hit");
                if (CheckTinkerSkillChange()) {
                    Start();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void AutoImbueAllButton_Hit(object sender, EventArgs e) {
            Logger.Debug("AutoImbueAllButton_Hit");
            SetTinkerSkills();
            tinkerJobManager.ClearAll();
            AutoImbueList.ClearRows();
            ClearAllTinks();
            ScanInventory();
            tinkerJobManager.ScanInventory();
            new Assessor.Job(UB.Assessor, ref itemsToId, (_) => { }, () => {
                tinkerJobManager.AutoImbueListFinished += AutoImbueList_Finished;
                tinkerJobManager.AutoImbueListChanged += AutoImbueList_Changed;
                tinkerJobManager.CreatePossibleMaterialList(null, true);
                Logger.WriteToChat("done scanning");
                tinkerJobManager.CreateTinkerableItemsList(TinkerableItemDict, null, null, true);
                itemsToId.Clear();
            });
        }

        private void AutoImbueStopButton_Hit(object sender, EventArgs e) {
            Logger.Debug("AutoImbueStopButton_Hit");
            try {
                Stop();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void ScanInventory() {
            itemsToId.Clear();
            FullSalvageDict.Clear();
            TinkerableItemDict.Clear();
            using (var inv = CoreManager.Current.WorldFilter.GetInventory()) {

                foreach (WorldObject wo in inv) {
                    if (!FullSalvageDict.ContainsKey(wo.Id) && wo.Values(LongValueKey.UsesRemaining) == 100 &&
                        wo.ObjectClass == ObjectClass.Salvage) {
                        FullSalvageDict.Add(wo.Id, wo.Values(DoubleValueKey.SalvageWorkmanship));
                    }
                    if (CanBeTinkered(wo) && !TinkerableItemDict.ContainsKey(wo.Id)) {
                        TinkerableItemDict.Add(wo.Id, wo.Values(DoubleValueKey.SalvageWorkmanship));
                        itemsToId.Add(wo.Id);
                    }
                }
            }
        }

        private void PopulateAutoImbueDmgTypeCombo() {

            var dmgTypes = Enum.GetValues(typeof(Lib.Constants.DamageTypes));
            foreach (var dmg in dmgTypes) {
                dmgList.Add(dmg.ToString());
            }
            var sortedDmgList = from element in dmgList
                                orderby element
                                select element;
            foreach (var dmg in sortedDmgList) {
                AutoImbueDmgTypeCombo.AddItem(dmg.ToString(), null);
            }
        }


        private void PopulateAutoImbueSalvageCombo() {
            AutoImbueSalvageList.Clear();
            AutoImbueSalvageCombo.Clear();
            FileService service = CoreManager.Current.Filter<FileService>();
            for (int i = 0; i < service.MaterialTable.Length; i++) {
                var material = service.MaterialTable[i];
                if (TinkerType.SalvageType(i) == 2) {
                    //Logger.WriteToChat(material.Name.ToString());
                    if (!AutoImbueSalvageList.ContainsKey(material.Name)) {
                        AutoImbueSalvageList.Add(material.Name, i);

                    }
                }
            }
            var SortedSalvageList = AutoImbueSalvageList.Keys.ToList();
            foreach (var item in AutoImbueSalvageList.OrderBy(i => i.Key)) {
                AutoImbueSalvageCombo.AddItem(item.Key.ToString(), null);
            }
        }

        private void AutoTinkerList_Click(object sender, int row, int col) {
            try {
                HudList.HudListRowAccessor imbueListRow = AutoTinkerList[row];
                HudStaticText itemID = (HudStaticText)imbueListRow[5];
                int.TryParse(itemID.Text.ToString(), out int item);
                CoreManager.Current.Actions.SelectItem(item);

            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (!waitingForIds) {
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }


        private void AutoTinkStopButton_Hit(object sender, EventArgs e) {
            try {
                Logger.Debug("AutoTinkStopButton_Hit");
                Stop();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Stop() {
            try {
                if (!isRunning && !isPopulating) {
                    AutoTinkerList.ClearRows();
                    AutoImbueList.ClearRows();
                    storedTargetItem = null;
                }
                AutoTinkItemNameLabel.Text = "[None]";
                tinkerJobManager.TinkerListChanged -= TinkerList_Changed;
                tinkerJobManager.TinkerListFinished -= TinkerList_Finished;
                tinkerJobManager.TinkerJobChanged -= TinkerJob_Changed;
                tinkerJobManager.TinkerJobFinished -= TinkerJob_Finished;
                tinkerJobManager.ClearAll();
                tinkerJobManager.Stop();
                isRunning = false;
                isPopulating = false;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void ClearAllTinks() {
            AutoTinkerList.ClearRows();
            AutoImbueList.ClearRows();
            TinkerList.Clear();
            PotentialSalvageList.Clear();
            FullSalvageDict.Clear();
            SalvageUsedList.Clear();
            TinkerableItemDict.Clear();
            salvageToBeApplied.Clear();
        }

        private void AutoTinkMinPercentTextBox_Changed(object sender, EventArgs e) {
            try {
                float f;
                if (AutoTinkMinPercentTextBox.Text != "") {
                    f = float.Parse(AutoTinkMinPercentTextBox.Text, CultureInfo.InvariantCulture.NumberFormat);
                }
                else {
                    f = 0;
                }

                MinPercentage = f;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }


        private void TinkerList_Changed(int itemID, int tinkeredCount, int salvageID, double successChance) {
            //Logger.WriteToChat("I found the change UI event");
            UpdateTinkList(tinkeredCount + 1, CoreManager.Current.WorldFilter[itemID], CoreManager.Current.WorldFilter[salvageID], tinkerCalc.DoCalc(salvageID, CoreManager.Current.WorldFilter[itemID], tinkeredCount));
            
            //tinkerJobManager.TinkerListChanged -= TinkerList_Changed;
        }

        private void AutoImbueList_Changed(int itemID, int tinkeredCount, int salvageID, double successChance) {
            //Logger.WriteToChat("I found the change UI event");
            UpdateAutoImbueList(tinkeredCount + 1, CoreManager.Current.WorldFilter[itemID], CoreManager.Current.WorldFilter[salvageID], successChance);

            //tinkerJobManager.TinkerListChanged -= TinkerList_Changed;
        }

        private void TinkerList_Finished(object sender, EventArgs e) {
            //UpdateTinkList(tinkeredCount, CoreManager.Current.WorldFilter[itemID], CoreManager.Current.WorldFilter[salvageID], tinkerCalc.DoCalc(salvageID, CoreManager.Current.WorldFilter[itemID], tinkeredCount));
            isPopulating = false;
            tinkerJobManager.TinkerListChanged -= TinkerList_Changed;
            tinkerJobManager.TinkerListFinished -= TinkerList_Finished;
        }

        private void AutoImbueList_Finished(object sender, EventArgs e) {
            //UpdateTinkList(tinkeredCount, CoreManager.Current.WorldFilter[itemID], CoreManager.Current.WorldFilter[salvageID], tinkerCalc.DoCalc(salvageID, CoreManager.Current.WorldFilter[itemID], tinkeredCount));
            isPopulating = false;
            tinkerJobManager.AutoImbueListChanged -= AutoImbueList_Changed;
            tinkerJobManager.AutoImbueListFinished -= AutoImbueList_Finished;
        }

        private void TinkerJob_Finished(object sender, EventArgs e) {
            //ClearAllTinks();
            Stop();
            tinkerJobManager.TinkerJobChanged -= TinkerJob_Changed;
            tinkerJobManager.TinkerJobFinished -= TinkerJob_Finished;
        }

        private void TinkerJob_Changed(int salvageID, bool succeeded) {
            //Logger.WriteToChat("I found the tinker job changed event ");// + salvageID.ToString() + " with a status of " + succeeded);
            UpdateUI(salvageID, succeeded);
        }

        private void PopulateListButton_Hit(object sender, EventArgs e) {
            try {
                if (storedTargetItem == null) {
                    Logger.WriteToChat("select an item first");
                    return;
                }
                Logger.Debug("PopulateListButton_Hit");
                runType = "single";
                SetTinkerSkills();
                if (isPopulating) {
                    Logger.WriteToChat("is populating is running");
                    return;
                }
                else {
                    tinkerJobManager.Stop();
                    isPopulating = true;
                    List<int> rescanItem = new List<int>();
                    List<int> matchingMaterial = new List<int>();
                    AutoTinkerList.ClearRows();
                    tinkerJobManager.ScanInventory();

                    string selectedItem = AutoTinkItemNameLabel.Text.ToString();


                    List<string> usableSalvageOnItem = new List<string>();
                    if (tinkerJobManager.CanBeTinkered(storedTargetItem)) {
                        UpdateNameLabel(storedTargetItem.Id);
                        usableSalvageOnItem = tinkerJobManager.GetUsableSalvage(storedTargetItem.Id);
                        FilterSalvageCombo(usableSalvageOnItem);
                        usableSalvageOnItem = usableSalvageOnItem.Distinct().ToList();

                        tinkerJobManager.CreatePossibleMaterialList(usableSalvageOnItem, false);

                        if (selectedItem == "[None]") {
                            Logger.WriteToChat("Select an item first");
                            return;
                        }
                    }


                    float.TryParse(AutoTinkMinPercentTextBox.Text, out float minPercent);
                    //Logger.WriteToChat(minPercent.ToString());

                    rescanItem.Add(storedTargetItem.Id);


                    HudStaticText c = (HudStaticText)(AutoTinkCombo[AutoTinkCombo.Current]);
                    string currentSalvageChoice = c.Text.ToString();
                    HudStaticText maxTinksString = (HudStaticText)(AutoTinkMaxTinksCombo[AutoTinkMaxTinksCombo.Current]);
                    int.TryParse(maxTinksString.Text, out int maxTinks);

                    new Assessor.Job(UB.Assessor, ref rescanItem, (_) => { }, () => {
                        tinkerJobManager.TinkerListFinished += TinkerList_Finished;
                        tinkerJobManager.TinkerListChanged += TinkerList_Changed;
                        //tinkerJobManager.PopulateTinkerList();
                        tinkerJobManager.BuildSalvageToBeApplied(storedTargetItem, currentSalvageChoice, runType, maxTinks);
                        //tinkerJobManager.WriteSalvageToBeApplied();
                        tinkerJobManager.BuildTinkerList(storedTargetItem, minPercent, "single", maxTinks);
                        rescanItem.Clear();
                    });
                    tinkerJob.minPercent = minPercent;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }


        private void UpdateNameLabel(int targetItem) {
            if (targetItem != 0) {
                //Logger.WriteToChat("found the item");
                AutoTinkItemNameLabel.Text = Util.GetObjectName(targetItem);
            }
            else {
                Logger.WriteToChat(Util.GetObjectName(targetItem).ToString() + " cannot be tinkered. Please select another item.");
                AutoTinkItemNameLabel.Text = "[None]";
                storedTargetItem = null;
            }
        }

        private void AutoTinkAddSelectedButton_Hit(object sender, EventArgs e) {
            try {
                ClearAllTinks();
                Logger.Debug("AutoTinkAddSelectedButton_Hit");
                runType = "single";
                if (isSelecting) {
                    Logger.WriteToChat("still selecting");
                    return;
                }
                else {
                    isSelecting = true;

                    List<int> scanItem = new List<int>();

                    List<string> usableSalvageOnItem = new List<string>();
                    tinkerJobManager.ClearAll();
                    AutoTinkerList.ClearRows();

                    tinkerJobManager.ScanInventory();

                    storedTargetItem = CoreManager.Current.WorldFilter[CoreManager.Current.Actions.CurrentSelection];
                    scanItem.Add(storedTargetItem.Id);

                    new Assessor.Job(UB.Assessor, ref scanItem, (_) => { }, () => {
                        isSelecting = false;
                        storedTargetItem = CoreManager.Current.WorldFilter[CoreManager.Current.Actions.CurrentSelection];
                        if (tinkerJobManager.CanBeTinkered(storedTargetItem)) {
                            UpdateNameLabel(storedTargetItem.Id);
                            usableSalvageOnItem = tinkerJobManager.GetUsableSalvage(storedTargetItem.Id);
                            FilterSalvageCombo(usableSalvageOnItem);
                            usableSalvageOnItem = usableSalvageOnItem.Distinct().ToList();

                            tinkerJobManager.CreatePossibleMaterialList(usableSalvageOnItem, false);
                            //tinkerJobManager.WritePossibleMaterialList();
                            scanItem.Clear();
                        }
                    });
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

       // private void TinkerTime(WorldObject targetItem) {
       //     PossibleMaterialList.Clear();
       //     List<string> UsableSalvage = new List<string>();
       //     //Logger.WriteToChat("before max damage ");
       //     fakeItem.MaxDamage = GetMaxDamage(targetItem);
       //     //Logger.WriteToChat("after max damage ");
       //     fakeItem.variance = targetItem.Values(DoubleValueKey.Variance);
       //     //Logger.WriteToChat(targetItem.Name);
       //     //UsableSalvage = GetUsableSalvage(targetItem.Id);
       //     //foreach (var sal in UsableSalvage) {
       //     //    CreatePossibleMaterialList(sal);
       //     //}
       //     int tinksRemaining = 10 - targetItem.Values(LongValueKey.NumberTimesTinkered);
       //     int tinkCount = targetItem.Values(LongValueKey.NumberTimesTinkered);
       //     BuildSalvageToBeApplied(targetItem, tinksRemaining);
       //     BuildTinkerList(targetItem, tinksRemaining, tinkCount);
       // }

        private int GetSpellID(WorldObject targetItem, int i) {
            var spell = Util.FileService.SpellTable.GetById(targetItem.Spell(i));
            return spell.Id;
        }

        private double GetMaxDamage(WorldObject targetItem) {
            double maxDamage = targetItem.Values(LongValueKey.MaxDamage);
            Logger.WriteToChat("max damage: " + maxDamage.ToString());
            int spellCount = targetItem.SpellCount;
            for (int s = 0; s <= spellCount - 1; s++) {
                int spell = GetSpellID(targetItem, s);
                if (spell == (int)2598) {
                    Logger.WriteToChat("minor");
                    maxDamage += 2; //minor blood thirst
                }
                if (spell == (int)2586) {
                    Logger.WriteToChat("major");
                    maxDamage += 4; //major blood thirst
                }
                if (spell == (int)4661) {
                    Logger.WriteToChat("epic");
                    maxDamage += 7; //epic blood thirst
                }
                if (spell == (int)6089) {
                    Logger.WriteToChat("legendary");
                    maxDamage += 10;  //legendary blood thirst
                }
            }
            maxDamage += 24;
            Logger.WriteToChat(maxDamage.ToString());
            return maxDamage;
        }

        private bool CheckSettings() {
            if ((UB.Core.CharacterFilter.CharacterOptions & 0x80000000) == 0) {
                Logger.WriteToChat("Error: You must enable the UseCraftSuccessDialog setting!");
                return false;
            }
            return true;
        }


        private void Start() {
            try {
                if (!CheckSettings()) return;
                if (isRunning) {
                    Logger.WriteToChat("already running");
                    return;
                }
                tinkerJobManager.TinkerJobChanged += TinkerJob_Changed;
                tinkerJobManager.TinkerJobFinished += TinkerJob_Finished;
                tinkerJobManager.Start();
                isRunning = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
            //tinkerJobManager.StartTinkerJobs();

        }

        private void WriteTinkerList() {
            foreach (DataRow row in TinkerList.Rows) {
                //Logger.WriteToChat(row["targetItemID"].ToString());
                //Logger.WriteToChat(row["tinkeredCount"].ToString());
                Logger.WriteToChat(row["tinkeredCount"].ToString() + ": Applying ws " + CoreManager.Current.WorldFilter[int.Parse(row["targetSalvageID"].ToString())].Values(DoubleValueKey.SalvageWorkmanship).ToString() + " " + row["targetSalvage"].ToString() + " " + row["targetSalvageID"].ToString() + " to " + row["targetItem"] + " " + row["targetItemID"].ToString() + " with a successChance of " + row["successChance"].ToString());
               //Logger.WriteToChat(row["targetSalvageID"].ToString());
               //Logger.WriteToChat(row["targetSalvage"].ToString());
               //Logger.WriteToChat(row["successChance"].ToString());
            }
        }

        

        private int GetTargetItem() {
            foreach (var tinkerableItemID in TinkerableItemDict.OrderBy(i => i.Value)) {
                WorldObject tinkerableItem = CoreManager.Current.WorldFilter[tinkerableItemID.Key];
                if (CanBeTinkered(tinkerableItem)) {
                    Logger.WriteToChat(Util.GetObjectName(tinkerableItemID.Key).ToString());
                    return tinkerableItemID.Key;
                }
                else {
                    return 0;
                }
            }
            return 0;
        }


        private static List<ObjectClass> ValidTinkeringObjectClasses = new List<ObjectClass>() {
            ObjectClass.Armor,
            ObjectClass.Clothing,
            ObjectClass.Jewelry,
            ObjectClass.MeleeWeapon,
            ObjectClass.MissileWeapon,
            ObjectClass.WandStaffOrb
        };
        
        private bool CanBeTinkered(WorldObject wo) {
            if (!ValidTinkeringObjectClasses.Contains(wo.ObjectClass))
                return false;

            if (wo.Values(DoubleValueKey.SalvageWorkmanship, 0) <= 0)
                return false;

            if (wo.Values(LongValueKey.NumberTimesTinkered, 0) >= 10)
                return false;

            return true;
        }

        private void FilterSalvageCombo(List<string> salvageArray) {

            HudStaticText c = (HudStaticText)(AutoTinkCombo[AutoTinkCombo.Current]);
            string currentSalvageChoice = c.Text.ToString();
            AutoTinkCombo.Clear();

            foreach (string s in salvageArray) {
                AutoTinkCombo.AddItem(s, null);
            }

            for (int i = 0; i < AutoTinkCombo.Count; i++) {
                HudStaticText s = (HudStaticText)(AutoTinkCombo[i]);
                if (currentSalvageChoice == s.Text.ToString()) {
                    AutoTinkCombo.Current = i;
                }
            }
        }

        private void AutoTinkStartButton_Hit(object sender, EventArgs e) {
            Logger.Debug("AutoTinkStartButton_Hit");
            if (CheckTinkerSkillChange() && storedTargetItem != null) {
                Start();
            }
            else {
                Logger.WriteToChat("select an item first");
            }
        }
        
        private void UpdateTinkList(int tinkCount, WorldObject targetItem, WorldObject targetSalvage, double successChance) {
            HudList.HudListRowAccessor newTinkRow = AutoTinkerList.AddRow();
            ((HudStaticText)newTinkRow[0]).Text = tinkCount.ToString();
            ((HudStaticText)newTinkRow[1]).Text = Util.GetObjectName(targetItem.Id).ToString();
            ((HudStaticText)newTinkRow[2]).Text = Util.GetObjectName(targetSalvage.Id).Replace(" Salvaged", "").Replace(" Salvage", "").Replace("(100)", "") + "(" + Math.Round(targetSalvage.Values(DoubleValueKey.SalvageWorkmanship),2, MidpointRounding.AwayFromZero) + ") " ;
            ((HudStaticText)newTinkRow[3]).Text = successChance.ToString("P");
            ((HudStaticText)newTinkRow[4]).Text = targetSalvage.Id.ToString();
            ((HudStaticText)newTinkRow[5]).Text = targetItem.Id.ToString();
        }

        private void UpdateAutoImbueList(int tinkCount, WorldObject targetItem, WorldObject targetSalvage, double successChance) {
            HudList.HudListRowAccessor newTinkRow = AutoImbueList.AddRow();
            ((HudStaticText)newTinkRow[0]).Text = tinkCount.ToString();
            ((HudStaticText)newTinkRow[1]).Text = Util.GetObjectName(targetItem.Id).ToString();
            ((HudStaticText)newTinkRow[2]).Text = Util.GetObjectName(targetSalvage.Id).Replace(" Salvaged", "").Replace(" Salvage", "").Replace("(100)","") + "(" + Math.Round(targetSalvage.Values(DoubleValueKey.SalvageWorkmanship), 2, MidpointRounding.AwayFromZero) + ") ";
            ((HudStaticText)newTinkRow[3]).Text = successChance.ToString("P");
            ((HudStaticText)newTinkRow[4]).Text = targetSalvage.Id.ToString();
            ((HudStaticText)newTinkRow[5]).Text = targetItem.Id.ToString();
        }



        private void UpdateUI(int inputSalvageID, bool succeeded) {
            //Logger.WriteToChat("updating UI");
            if (AutoTinkerList.RowCount > 0) {
                for (int i = 0; i <= AutoTinkerList.RowCount; i++) {
                    HudList.HudListRowAccessor autoTinkerListRow = AutoTinkerList[i];
                    HudStaticText salvageID = (HudStaticText)autoTinkerListRow[4];
                    int.TryParse(salvageID.Text, out int salvageIdInt);
                    if (salvageIdInt == inputSalvageID) {
                        for (int c = 0; c <= AutoTinkerList.ColumnCount - 2; c++) {
                            HudStaticText col = (HudStaticText)autoTinkerListRow[c];
                            if (succeeded) {
                                col.TextColor = System.Drawing.Color.Green;
                            }
                            else {
                                col.TextColor = System.Drawing.Color.Red;
                            }
                        }
                        break;
                    }
                }
            }
            if (AutoImbueList.RowCount > 0) {
                for (int i = 0; i <= AutoImbueList.RowCount; i++) {
                    HudList.HudListRowAccessor autoImbueListRow = AutoImbueList[i];
                    HudStaticText salvageID = (HudStaticText)autoImbueListRow[4];
                    int.TryParse(salvageID.Text, out int salvageIdInt);
                    if (salvageIdInt == inputSalvageID) {
                        for (int c = 0; c <= AutoImbueList.ColumnCount - 2; c++) {
                            HudStaticText col = (HudStaticText)autoImbueListRow[c];
                            if (succeeded) {
                                col.TextColor = System.Drawing.Color.Green;
                            }
                            else {
                                col.TextColor = System.Drawing.Color.Red;
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void SetTinkerSkills() {
            int jackofalltradesbonus = 0;
            if (CoreManager.Current.CharacterFilter.GetCharProperty(326) == 1) {
                jackofalltradesbonus = 5;
            }
            weaponTinkeringSkill = CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.WeaponTinkering] + jackofalltradesbonus;
            magicItemTinkeringSkill = CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.MagicItemTinkering] + jackofalltradesbonus;
            armorTinkeringSkill = CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.ArmorTinkering] + jackofalltradesbonus;
            itemTinkeringSkill = CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.ItemTinkering] + jackofalltradesbonus;
            //Logger.WriteToChat("weaponTinkeringSkill: " + weaponTinkeringSkill.ToString());
            //Logger.WriteToChat("magicItemTinkeringSkill: " + magicItemTinkeringSkill.ToString());
            //Logger.WriteToChat("armorTinkeringSkill: " + armorTinkeringSkill.ToString());
            //Logger.WriteToChat("itemTinkeringSkill: " + itemTinkeringSkill.ToString());
        }

        private bool CheckTinkerSkillChange() {
            int jackofalltradesbonus = 0;
            if (CoreManager.Current.CharacterFilter.GetCharProperty(326) == 1) {
                jackofalltradesbonus = 5;
            }
            if ((CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.WeaponTinkering] + jackofalltradesbonus) < weaponTinkeringSkill) {
                Logger.WriteToChat("weapon tinkering dropped... stopping tinkering");
                return false;
            }
            if ((CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.MagicItemTinkering] + jackofalltradesbonus) < magicItemTinkeringSkill) {
                Logger.WriteToChat("magic item tinkering dropped... stopping tinkering");
                return false;
            }
            if ((CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.ArmorTinkering] + jackofalltradesbonus) < armorTinkeringSkill) {
                Logger.WriteToChat("armor tinkering dropped... stopping tinkering");
                return false;
            }
            if ((CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.ItemTinkering] + jackofalltradesbonus) < itemTinkeringSkill) {
                Logger.WriteToChat("item tinkering dropped... stopping tinkering");
                return false;
            }
            return true;
        }

        private void PopulateAutoTinkCombo() {
            
            AutoTinkCombo.Clear();
            
            FileService service = CoreManager.Current.Filter<FileService>();
            for (int i = 0; i < service.MaterialTable.Length; i++) {
                var material = service.MaterialTable[i];
                if (!AutoTinkerSalvageList.ContainsKey(material.Name)) {
                    AutoTinkerSalvageList.Add(material.Name, i);
                }
                
            }
            var SortedSalvageList = AutoTinkerSalvageList.Keys.ToList();
            foreach (var item in AutoTinkerSalvageList.OrderBy(i => i.Key)) {
                    AutoTinkCombo.AddItem(item.Key.ToString(), null);
            }
            AutoTinkCombo.AddItem("Granite/Iron", null);
        } 

        public void PopulateAutoTinkMaxTinksCombo() {
            AutoTinkMaxTinksCombo.Clear();

            for (int i = 1; i <= 10; i++) {
                AutoTinkMaxTinksCombo.AddItem(i.ToString(), null);
            }
            AutoTinkMaxTinksCombo.Current = AutoTinkMaxTinksCombo.Count - 1;
        }

        public double GetDPS(double maxDamage, double variance) {
            double minDmg = maxDamage * (1 - variance);
            double critDmg = maxDamage * 2;
            double avgDmg = (maxDamage + minDmg) / 2;
            double DPS = .9 * avgDmg + .1 * critDmg;
            return DPS;
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
