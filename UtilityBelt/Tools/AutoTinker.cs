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

namespace UtilityBelt.Tools {
    class AutoTinker : IDisposable {

        HudButton AutoTinkAddSelectedButton { get; set; }
        HudButton selectSalvage { get; set; }
        HudList AutoTinkerList { get; set; }
        HudButton ClearList { get; set; }
        HudButton PopulateList { get; set; }
        HudButton AutoTinkStartButton { get; set; }
        HudButton AutoTinkStopButton { get; set; }
        HudButton PopulateListButton { get; set; }
        HudStaticText AutoTinkItemLabel { get; set; }
        HudStaticText AutoTinkItemNameLabel { get; set; }
        HudCombo AutoTinkCombo { get; set; }
        HudTextBox AutoTinkMinPercentTextBox { get; set; }

        private FakeItem fakeItem = new FakeItem();
        public DataTable tinkerDT = new DataTable();
        private bool waitingForIds = false;
        private DateTime lastIdSpam = DateTime.MinValue;
        private DateTime startTime = DateTime.MinValue;
        private DateTime endTime = DateTime.MinValue;
        private readonly List<int> itemsToId = new List<int>();
        bool targetItemUpdated = false;
        bool readyForNextTink = false;
        private int lastIdCount;
        double currentSalvageWK;
        string currentSalvage;
        string currentItemName;
        private bool disposed;
        private bool tinking = false;
        WorldObject targetSalvage;
        WorldObject itemWO;
        TinkerCalc tinkerCalc = new TinkerCalc();
        TinkerType tinkerType = new TinkerType();

        Dictionary<string, int> SalvageList = new Dictionary<string, int>();
        Dictionary<int, double> PotentialSalvageList = new Dictionary<int, double>();
        List<string> ComboBoxList = new List<string>();

        public AutoTinker() {
            try {
                CreateDataTable();
                Globals.Core.CommandLineText += Current_CommandLineText;
                Globals.Core.ChatBoxMessage += Current_ChatBoxMessage;

                AutoTinkerList = (HudList)Globals.MainView.view["AutoTinkerList"];
                AutoTinkerList.Click += AutoTinkerList_Click;

                AutoTinkCombo = (HudCombo)Globals.MainView.view["AutoTinkCombo"];

                AutoTinkCombo.Change += AutoTinkCombo_Change;

                AutoTinkAddSelectedButton = (HudButton)Globals.MainView.view["AutoTinkAddSelectedButton"];
                AutoTinkAddSelectedButton.Hit += AutoTinkAddSelectedButton_Hit;

                AutoTinkStartButton = (HudButton)Globals.MainView.view["AutoTinkStartButton"];
                AutoTinkStartButton.Hit += AutoTinkStartButton_Hit;

                AutoTinkStopButton = (HudButton)Globals.MainView.view["AutoTinkStopButton"];
                AutoTinkStopButton.Hit += AutoTinkStopButton_Hit;

                PopulateListButton = (HudButton)Globals.MainView.view["PopulateListButton"];
                PopulateListButton.Hit += PopulateListButton_Hit;

                AutoTinkMinPercentTextBox = (HudTextBox)Globals.MainView.view["AutoTinkMinPercentTextBox"];

                AutoTinkMinPercentTextBox.Change += AutoTinkMinPercentTextBox_Changed;
                AutoTinkMinPercentTextBox.Text = Globals.Settings.AutoTinker.MinPercentage.ToString();
                
                AutoTinkItemNameLabel = (HudStaticText)Globals.MainView.view["AutoTinkItemNameLabel"];

                PopulateAutoTinkCombo();

            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void AutoTinkerList_Click(object sender, int row, int col) {
            try {
                HudList.HudListRowAccessor imbueListRow = AutoTinkerList[row];
                HudStaticText itemID = (HudStaticText)imbueListRow[5];
                int.TryParse(itemID.Text.ToString(), out int item);
                Globals.Core.Actions.SelectItem(item);

            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Think() {
            try {
                if (waitingForIds) {
                    if (Globals.Assessor.NeedsInventoryData(itemsToId)) {
                        if (DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(15)) {
                            lastIdSpam = DateTime.UtcNow;
                            var thisIdCount = Globals.Assessor.GetNeededIdCount(itemsToId);
                            Util.WriteToChat(string.Format("AutoImbue waiting to id {0} items, this will take approximately {0} seconds.", thisIdCount));
                            if (lastIdCount != thisIdCount) {
                                lastIdCount = thisIdCount;
                            }
                        }
                        return;
                    }
                    else {
                        waitingForIds = false;
                        endTime = DateTime.UtcNow;
                        Util.WriteToChat("AutoTinker: took " + Util.GetFriendlyTimeDifference(endTime - startTime) + " to scan");
                        ClearAllTinks();
                        DoPopulateList();
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                if (tinking) {
                    string characterName = Globals.Core.CharacterFilter.Name;

                    Regex CraftSuccess = new Regex("^" + characterName + @" successfully applies the (?<salvage>[\w\s\-]+) Salvage(\s?\(100\))?\s\(workmanship (?<workmanship>\d+\.\d+)\) to the (?<item>[\w\s\-]+)\.$");
                    Regex CraftFailure = new Regex("^" + characterName + @" fails to apply the (?<salvage>[\w\s\-]+) Salvage(\s?\(100\))?\s\(workmanship (?<workmanship>\d+\.\d+)\) to the (?<item>[\w\s\-]+).\s?The target is destroyed\.$");

                    if (CraftSuccess.IsMatch(e.Text.Trim())) {
                        var match = CraftSuccess.Match(e.Text.Trim());
                        string result = "success";
                        string chatcapSalvage = match.Groups["salvage"].Value;
                        string chatcapWK = match.Groups["workmanship"].Value;
                        string chatcapItem = match.Groups["item"].Value;
                        if (currentItemName == chatcapItem && currentSalvageWK.ToString("0.00") == chatcapWK.ToString() && chatcapSalvage == currentSalvage) {
                            Logger.Debug(result + ": " + chatcapSalvage + " " + chatcapWK + " on " + chatcapItem);
                            readyForNextTink = true;
                            Globals.Core.Actions.RequestId(fakeItem.id);
                        }
                        else {
                            Logger.Debug("AutoTinker: did not match success" + "chat salvageWK: " + chatcapWK.ToString() + "real salvageWK: " + currentSalvageWK.ToString("0.00") + "chat item name: " + chatcapItem + "real item name: " + currentItemName + "chat salv name: " + chatcapSalvage + "real salv name: " + currentSalvage);
                        }
                    }


                    if (CraftFailure.IsMatch(e.Text.Trim())) {
                        var match = CraftFailure.Match(e.Text.Trim());
                        string result = "failure";
                        string chatcapSalvage = match.Groups["salvage"].Value.Replace(" Salvage", "");
                        string chatcapWK = match.Groups["workmanship"].Value;
                        string chatcapItem = match.Groups["item"].Value;
                        if (currentItemName == chatcapItem && currentSalvageWK.ToString("0.00") == chatcapWK.ToString() && chatcapSalvage == currentSalvage) {
                            Logger.Debug(result + ": " + chatcapSalvage + " " + chatcapWK + " on " + chatcapItem);
                            ClearAllTinks();
                        }
                        else {
                            Logger.Debug("AutoTinker: did not match failure" + "chat salvageWK: " + chatcapWK.ToString() + "real salvageWK: " + currentSalvageWK.ToString("0.00") + "chat item name: " + chatcapItem + "real item name: " + currentItemName + "chat salv name: " + chatcapSalvage + "real salv name: " + currentSalvage);
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void NextTink() {
            AutoTinkerList.RemoveRow(0);
            tinking = false;
            DoTinks();
        }

        public void AutoImbueSalvageCombo_Change(object sender, EventArgs e) {
            HudStaticText c = (HudStaticText)(AutoTinkCombo[AutoTinkCombo.Current]);
            ClearAllTinks();
            DoPopulateList();
        }

        public void GetPotentialSalvage() {
            try {
                HudStaticText c = (HudStaticText)(AutoTinkCombo[AutoTinkCombo.Current]);
                var materialID = SalvageList[c.Text.ToString()];

                foreach (var item in Globals.Core.WorldFilter.GetInventory()) {
                    if (!item.HasIdData) {
                        itemsToId.Add(item.Id);
                    }
                }

                if (Globals.Assessor.NeedsInventoryData(itemsToId)) {
                    Util.WriteToChat("requesting all ids");
                    Globals.Assessor.RequestAll(itemsToId);
                    waitingForIds = true;
                    lastIdSpam = DateTime.UtcNow;
                    startTime = DateTime.UtcNow;
                }

                foreach (WorldObject wo in Globals.Core.WorldFilter.GetInventory()) {
                    if (wo.Values(LongValueKey.Material) == materialID && wo.Values(LongValueKey.UsesRemaining) == 100) {
                        if (!PotentialSalvageList.ContainsKey(wo.Id)) {
                            PotentialSalvageList.Add(wo.Id,wo.Values(DoubleValueKey.SalvageWorkmanship));
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void AutoTinkStopButton_Hit(object sender, EventArgs e) {
            Globals.Core.Actions.RequestId(fakeItem.id);
            ClearAllTinks();
        }

        public void ClearAllTinks() {
            Globals.Core.WorldFilter.ChangeObject -= WaitForItemUpdate;
            UBHelper.ConfirmationRequest.ConfirmationRequestEvent -= UBHelper_ConfirmationRequest;
            readyForNextTink = false;
            tinking = false;
            fakeItem.tinkeredCount = 0;
            fakeItem.id = 0;
            fakeItem.name = "";
            fakeItem.successPercent = 0;
            fakeItem.workmanship = 0;
            AutoTinkerList.ClearRows();
            PotentialSalvageList.Clear();
        }

        private void AutoTinkMinPercentTextBox_Changed(object sender, EventArgs e) {
            float f;
            if (AutoTinkMinPercentTextBox.Text != "") {
                f = float.Parse(AutoTinkMinPercentTextBox.Text, CultureInfo.InvariantCulture.NumberFormat);
            }
            else {
                f = 0;
            }

            Globals.Settings.AutoTinker.MinPercentage = f;

        }
        public void AutoTinkCombo_Change(object sender, EventArgs e) {
            HudStaticText c = (HudStaticText)(AutoTinkCombo[AutoTinkCombo.Current]);
        }

        private void DoPopulateList() {
            tinking = false;
            if (itemWO != null) {
                itemWO = Globals.Core.WorldFilter[itemWO.Id];
            }
            ClearAllTinks();

            GetPotentialSalvage();

            foreach (var item in PotentialSalvageList.OrderBy(i => i.Value)) {
                double.TryParse(AutoTinkMinPercentTextBox.Text, out double minPercent);

                if (fakeItem.tinkeredCount < 10) {
                    DoThings(item.Key, itemWO);
                    if (minPercent / 100 > fakeItem.successPercent) {
                        continue;
                    }
                    fakeItem.tinkeredCount++;
                    UpdateTinkList();
                }
            }
            if (AutoTinkerList.RowCount <= 0) {
                Util.WriteToChat("No salvage matched the selected criteria.  Either lower the percentage or get more salvage.");
            }
        }

        private void PopulateListButton_Hit(object sender, EventArgs e) {
            ClearAllTinks();
            DoPopulateList();
        }

        private void AutoTinkAddSelectedButton_Hit(object sender, EventArgs e) {
            ClearAllTinks();
            itemWO = Globals.Core.WorldFilter[Globals.Core.Actions.CurrentSelection];
            targetItemUpdated = true;
            if (itemWO.HasIdData && itemWO.Values(DoubleValueKey.SalvageWorkmanship) > 0 && itemWO.Values(LongValueKey.Material) > 0 && itemWO.Values(LongValueKey.NumberTimesTinkered) < 10) {
                AutoTinkItemNameLabel.Text = Util.GetObjectName(itemWO.Id);
                FilterSalvage(itemWO);
            }
            else {
                Util.WriteToChat(Util.GetObjectName(itemWO.Id).ToString() + " cannot be tinkered. Please select another item.");
                AutoTinkItemNameLabel.Text = "[None]";
                itemWO = null;
            }
        }

        private void FilterSalvageCombo(string[] salvageArray) {
            for (int i = 0; i < AutoTinkCombo.Count; i++) {
                HudStaticText c = (HudStaticText)(AutoTinkCombo[i]);
                if (salvageArray.Contains(c.Text.ToString())) {
                    ComboBoxList.Add(c.Text.ToString());
                }
            }
        }

        private void FilterSalvage(WorldObject itemWO) {

            HudStaticText c = (HudStaticText)(AutoTinkCombo[AutoTinkCombo.Current]);
            string currentSalvageChoice = c.Text.ToString();

            PopulateAutoTinkCombo();
            ComboBoxList.Clear();
            var category = itemWO.ObjectClass;
            string[] filteredSalvage = new string[] { };

            switch (category) {
                case ObjectClass.MissileWeapon:
                    //Util.WriteToChat("Missile");
                    filteredSalvage = new string[] { "Mahogany", "Brass" };
                    break;
                case ObjectClass.MeleeWeapon:
                    //Util.WriteToChat("Melee");
                    filteredSalvage = new string[] { "Brass", "Granite", "Iron" };
                    break;
                case ObjectClass.WandStaffOrb:
                    //Util.WriteToChat("Wand");
                    filteredSalvage = new string[] { "Brass", "Green Garnet" };
                    break;
                case ObjectClass.Armor:
                    //Util.WriteToChat("Armor");
                    filteredSalvage = new string[] { "Steel" };
                    break;
                case ObjectClass.Jewelry:
                    //Util.WriteToChat("Jewelry");
                    filteredSalvage = new string[] { "Gold" };
                    break;
                default:
                    Console.WriteLine("Select an item");
                    break;
            }

            FilterSalvageCombo(filteredSalvage);

            AutoTinkCombo.Clear();
            foreach (string item in ComboBoxList) {
                AutoTinkCombo.AddItem(item, null);
            }
            for (int i = 0; i < AutoTinkCombo.Count; i++) {
                HudStaticText s = (HudStaticText)(AutoTinkCombo[i]);
                if (currentSalvageChoice == s.Text.ToString()) {
                    AutoTinkCombo.Current = i;
                }
            }


            if (category == ObjectClass.MissileWeapon) {
                //Util.WriteToChat("setting default to mahog");
                AutoTinkCombo.Current = 1;
            }

        }

        private void AutoTinkStartButton_Hit(object sender, EventArgs e) {
                DoTinks();
        }
        
        private void UpdateTinkList() {
            HudList.HudListRowAccessor newTinkRow = AutoTinkerList.AddRow();
            ((HudStaticText)newTinkRow[0]).Text = fakeItem.tinkeredCount.ToString();
            ((HudStaticText)newTinkRow[1]).Text = fakeItem.name.ToString();
            ((HudStaticText)newTinkRow[2]).Text = Util.GetObjectName(targetSalvage.Id) + " w" + Math.Round(targetSalvage.Values(DoubleValueKey.SalvageWorkmanship),2, MidpointRounding.AwayFromZero);
            ((HudStaticText)newTinkRow[3]).Text = fakeItem.successPercent.ToString("P");
            ((HudStaticText)newTinkRow[4]).Text = targetSalvage.Id.ToString();
            ((HudStaticText)newTinkRow[5]).Text = fakeItem.id.ToString();
        }

        public decimal TruncateDecimal(decimal value, int precision) {
            decimal step = (decimal)Math.Pow(10, precision);
            decimal tmp = Math.Truncate(step * value);
            return tmp / step;
        }

        private void DoTinks() {
            if (AutoTinkerList.RowCount <= 0) {
                ClearAllTinks();
            }

            HudList.HudListRowAccessor tinkerListRow = AutoTinkerList[0];
            HudStaticText item = (HudStaticText)tinkerListRow[1];
            HudStaticText sal = (HudStaticText)tinkerListRow[2];
            HudStaticText successP = (HudStaticText)tinkerListRow[3];
            string successPstr = successP.Text.ToString();
            HudStaticText salID = (HudStaticText)tinkerListRow[4];
            HudStaticText itemID = (HudStaticText)tinkerListRow[5];
            
            if (!int.TryParse(itemID.Text.ToString(), out int intItemId)) {
                Util.WriteToChat("AutoTinker: Something went wrong, unable to parse item to work with: " + itemID.Text);
            }
            
            if (!int.TryParse(salID.Text.ToString(), out int intSalvId)) {
                Util.WriteToChat("AutoTinker: Something went wrong, unable to parse salvage to work with: " + salID.Text);
            }
            
            
            if (intItemId != 0) {
                currentItemName = Util.GetObjectName(intItemId);
            }
            
            if (intSalvId != 0) {
                currentSalvageWK = Math.Round(Globals.Core.WorldFilter[intSalvId].Values(DoubleValueKey.SalvageWorkmanship),2, MidpointRounding.AwayFromZero);
                currentSalvage = Util.GetObjectName(intSalvId).Replace(" Salvage", "").Replace(" (100)", "");
            }
            
            Globals.Core.Actions.SelectItem(intSalvId);


            if (!tinking) {
                Globals.Core.Actions.SelectItem(intSalvId);
                if (Globals.Core.Actions.CurrentSelection == intSalvId) {
                    UBHelper.ConfirmationRequest.ConfirmationRequestEvent += UBHelper_ConfirmationRequest;
                    string verifyPercent = (tinkerCalc.DoCalc(intSalvId, Globals.Core.WorldFilter[intItemId], Globals.Core.WorldFilter[intItemId].Values(LongValueKey.NumberTimesTinkered)).ToString("P"));
                    if (targetItemUpdated && verifyPercent == successPstr ) {
                        //Util.WriteToChat("matched success %... applying");
                        Globals.Core.WorldFilter.ChangeObject += WaitForItemUpdate;
                        readyForNextTink = false;
                        Globals.Core.Actions.ApplyItem(intSalvId, intItemId);
                        targetItemUpdated = false;
                        tinking = true;
                    }
                    else if (targetItemUpdated && verifyPercent != successPstr) {
                        Util.WriteToChat("Tinker % changed.  Please check your buffs/brill and refresh the list to continue." + " ------ planned: " + successPstr + " ----- actual: " + verifyPercent);
                        ClearAllTinks();
                    }
                }
            }
        }

        private void WaitForItemUpdate(object sender, ChangeObjectEventArgs e) {
            if (e.Changed.Id == fakeItem.id && readyForNextTink) {
                //Util.WriteToChat("changed: " + e.Changed.Name);
                targetItemUpdated = true;
                Globals.Core.WorldFilter.ChangeObject -= WaitForItemUpdate;
                NextTink();
            }
        }

        private void UBHelper_ConfirmationRequest(object sender, UBHelper.ConfirmationRequest.ConfirmationRequestEventArgs e) {
            if (e.Confirm == 5) {
                Util.WriteToChat($"AutoTinker: Clicking Yes on {e.Text}");
                e.ClickYes = true;
                UBHelper.ConfirmationRequest.ConfirmationRequestEvent -= UBHelper_ConfirmationRequest;
            }
        }

        private void PopulateAutoTinkCombo() {
            AutoTinkCombo.Clear();
            
            FileService service = Globals.Core.Filter<FileService>();
            for (int i = 0; i < service.MaterialTable.Length; i++) {
                var material = service.MaterialTable[i];
                if (!SalvageList.ContainsKey(material.Name)) {
                    SalvageList.Add(material.Name, i);
                }
                
            }
            var SortedSalvageList = SalvageList.Keys.ToList();
            foreach (var item in SalvageList.OrderBy(i => i.Key)) {
                    AutoTinkCombo.AddItem(item.Key.ToString(), null);
            }
            AutoTinkCombo.AddItem("Granite/Iron", null);
        }

        public void GetMaterialList() {
            FileService service = Globals.Core.Filter<FileService>();
            for (var i = 0; i < service.MaterialTable.Length; i++) {
                var material = service.MaterialTable[i];
            }

        }

        public void CreateDataTable() {
            try {
                tinkerDT.Columns.Add("tinkeredCount", typeof(int));
                tinkerDT.Columns.Add("targetItem", typeof(string));
                tinkerDT.Columns.Add("targetSalvage", typeof(string));
                tinkerDT.Columns.Add("successChance", typeof(double));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) { }
        //    try {
        //        if (e.Text.StartsWith("/ub autotinker target salvage")) {
        //            string path = e.Text.Replace("/ub autotinker target salvage ", "").Trim();
        //            e.Eat = true;
        //
        //            salvageWO = Globals.Core.WorldFilter[Globals.Core.Actions.CurrentSelection];
        //            Util.WriteToChat("name: " + Util.GetObjectName(salvageWO.Id) + "---- Salvage Material: " + salvageWO.Values(LongValueKey.Material));
        //            var salvageMod = TinkerType.GetMaterialMod(salvageWO.Values(LongValueKey.Material));
        //
        //            return;
        //        }
        //        if (e.Text.StartsWith("/ub autotinker target item")) {
        //            string path = e.Text.Replace("/ub autotinker target salvage ", "").Trim();
        //            e.Eat = true;
        //
        //            itemWO =  Globals.Core.WorldFilter[Globals.Core.Actions.CurrentSelection];
        //            return;
        //        }
        //        if (e.Text.StartsWith("/ub autotinker dotinker")) {
        //            string path = e.Text.Replace("/ub autotinker target salvage ", "").Trim();
        //            e.Eat = true;
        //            Util.WriteToChat("starting autotinker");
        //            Util.WriteToChat("Applying " + Util.GetObjectName(salvageWO.Id) + " to " + itemWO.Name);
        //            DoThings(salvageWO.Id,itemWO);
        //            UpdateTinkList();
        //
        //            return;
        //        }
        //    }
        //    catch (Exception ex) { Logger.LogException(ex); }
        //}

        public void DoThings(int targetSalvageID, WorldObject targetWO) {
            if (targetSalvageID == 0) {
                Util.WriteToChat("targetSalvageID is null");
            }
            if (targetWO.Id == 0) {
                Util.WriteToChat("targetSalvageID is null");
            }

            targetSalvage = Globals.Core.WorldFilter[targetSalvageID];
            fakeItem.name = itemWO.Name;
            fakeItem.id = itemWO.Id;
            if (fakeItem.tinkeredCount == 0) {
                fakeItem.tinkeredCount = itemWO.Values(LongValueKey.NumberTimesTinkered);
            }
            fakeItem.successPercent = tinkerCalc.DoCalc(targetSalvage.Id, targetWO, fakeItem.tinkeredCount);
            var enchantments = Globals.Core.CharacterFilter.Enchantments;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.CommandLineText -= Current_CommandLineText;
                    Globals.Core.ChatBoxMessage -= Current_ChatBoxMessage;
                }
                disposed = true;
            }
        }
    }
}
