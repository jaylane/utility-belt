using System;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using UtilityBelt.Lib.Salvage;
using System.Collections.Generic;
using System.Linq;
using UtilityBelt.Lib.Constants;

namespace UtilityBelt.Lib.Tinker {
    class TinkerJobManager {
        public List<TinkerJob> jobs = new List<TinkerJob>();
        private TinkerJob tinkerJob = new TinkerJob();


        private List<int> itemsToId = new List<int>();
        private readonly List<int> salvageToBeApplied = new List<int>();
        private readonly List<int> PossibleMaterialList = new List<int>();
        private readonly List<int> SalvageUsedList = new List<int>();
        private  Dictionary<int, double> FullSalvageDict = new Dictionary<int, double>();
        private  Dictionary<int, double> TinkerableItemDict = new Dictionary<int, double>();
        readonly Dictionary<string, int> DefaultImbueList = new Dictionary<string, int>();
        private double fakeMaxDamage;
        private double fakeVariance;
        private int imbueCount;

        public bool CanBeTinkered(WorldObject wo) {
            if (!ValidTinkeringObjectClasses.Contains(wo.ObjectClass))
                return false;

            if (wo.Values(DoubleValueKey.SalvageWorkmanship, 0) <= 0)
                return false;

            if (wo.Values(LongValueKey.NumberTimesTinkered, 0) >= 10)
                return false;

            return true;
        }

        private static List<ObjectClass> ValidTinkeringObjectClasses = new List<ObjectClass>() {
            ObjectClass.Armor,
            ObjectClass.Clothing,
            ObjectClass.Jewelry,
            ObjectClass.MeleeWeapon,
            ObjectClass.MissileWeapon,
            ObjectClass.WandStaffOrb
        };

        public Dictionary<string,int> BuildDefaultImbueList() {
            DefaultImbueList.Add("Acid", 21);
            DefaultImbueList.Add("Bludgeoning", 47);
            DefaultImbueList.Add("Cold", 13);
            DefaultImbueList.Add("Electric", 27);
            DefaultImbueList.Add("Fire", 35);
            DefaultImbueList.Add("Nether", 16);
            DefaultImbueList.Add("Piercing", 15);
            DefaultImbueList.Add("Slashing", 26);
            DefaultImbueList.Add("SlashPierce", 26);
            DefaultImbueList.Add("Normal", 16);
            return DefaultImbueList;
        }

        public void CreateTinkerableItemsList(Dictionary<int, double> dict, string dmgType = "", string salType = "", bool multi = false) {
            jobs.Clear();
            imbueCount = 0;
            TinkerableItemDict = dict;
            int salID;
            int dmgtTypeInt;
            int tinkCount = 0;
            if (multi) {
                foreach (var item in TinkerableItemDict) {
                    salID = 0;
                    dmgtTypeInt = -1;
                    if (!CanBeImbued(item.Key)) {
                        continue;
                    }
                    if (CoreManager.Current.WorldFilter[item.Key].ObjectClass == ObjectClass.WandStaffOrb) {
                        dmgtTypeInt = CoreManager.Current.WorldFilter[item.Key].Values(LongValueKey.WandElemDmgType);
                    }
                    if (CoreManager.Current.WorldFilter[item.Key].ObjectClass == ObjectClass.MeleeWeapon ||
                        CoreManager.Current.WorldFilter[item.Key].ObjectClass == ObjectClass.MissileWeapon) {
                        dmgtTypeInt = CoreManager.Current.WorldFilter[item.Key].Values(LongValueKey.DamageType);
                    }
                    if (dmgtTypeInt == -1) {
                        continue;
                    }
                    //Logger.WriteToChat(dmgtTypeInt.ToString());
                    salID = GetDefaultSalvage(dmgtTypeInt);
                    //Logger.WriteToChat(salID.ToString());

                    //Logger.WriteToChat("I want to apply " + Util.FileService.MaterialTable.GetById(salID).Name + " to " + Util.GetObjectName(item.Key));

                    List<int> salBagToUse = new List<int>();
                    salBagToUse.Clear();
                    int salBag = GetNextSalvageBag(salID);
                    if (salBag == 0) {
                        continue;
                    }
                    SalvageUsedList.Add(salBag);
                    salBagToUse.Add(salBag);
                    //Logger.WriteToChat(salBag.ToString());

                    tinkCount++;
                    TinkerCalc tinkerCalc = new TinkerCalc();
                    double successChance = tinkerCalc.DoCalc(salBag, CoreManager.Current.WorldFilter[item.Key], CoreManager.Current.WorldFilter[item.Key].Values(LongValueKey.NumberTimesTinkered));
                    //Util.WriteToChat(successChance.ToString());
                    
                    if (successChance < .30) {
                        continue;
                    }

                    tinkCount++;
                    if (salBag != 0) {
                        jobs.Add(new TinkerJob() {
                            itemID = item.Key,
                            salvageToBeApplied = salBagToUse
                        });
                        AddAutoImbueList(item.Key, tinkCount, salBag, tinkerCalc.DoCalc(salBag, CoreManager.Current.WorldFilter[item.Key], 1));
                    }
                }
                AutoImbueListFinished?.Invoke(this, EventArgs.Empty);
            }
            else {

                List<ObjectClass> AllowedObjectClasses = new List<ObjectClass>();
                uint dmgTypeID = (uint)Enum.Parse(typeof(DamageTypes), dmgType.Trim());
                AllowedObjectClasses = TinkerType.IsImbuePossible(Util.FileService.MaterialTable.GetByName(salType).Id);
                foreach (var item in TinkerableItemDict) {
                    if (!CanBeImbued(item.Key)) {
                        continue;
                    }
                    if ((CoreManager.Current.WorldFilter[item.Key].Values(LongValueKey.DamageType) == dmgTypeID ||
                        CoreManager.Current.WorldFilter[item.Key].Values(LongValueKey.WandElemDmgType) == dmgTypeID) &&
                        AllowedObjectClasses.Contains(CoreManager.Current.WorldFilter[item.Key].ObjectClass)) {
                        BuildSalvageToBeApplied(CoreManager.Current.WorldFilter[item.Key], salType, "imbue", 1);
                        BuildTinkerList(CoreManager.Current.WorldFilter[item.Key], 0, "imbue", 1);
                    }
                }

                AutoImbueListFinished?.Invoke(this, EventArgs.Empty);
            }
        }

        private int GetDefaultSalvage(int damageType) {
            switch (damageType) {
                case 0:
                    return 16;
                case 1:
                    return 26;
                case 2:
                    return 15;
                case 3:
                    return 26;
                case 4:
                    return 47;
                case 8:
                    return 13;
                case 16:
                    return 35;
                case 32:
                    return 21;
                case 64:
                    return 27;
                case 1024:
                    return 16;
                default:
                    return -1;
                    //DefaultImbueList.Add("Acid", 21);
                    //DefaultImbueList.Add("Bludgeoning", 47);
                    //DefaultImbueList.Add("Cold", 13);
                    //DefaultImbueList.Add("Electric", 27);
                    //DefaultImbueList.Add("Fire", 35);
                    //DefaultImbueList.Add("Nether", 16);
                    //DefaultImbueList.Add("Piercing", 15);
                    //DefaultImbueList.Add("Slashing", 26);
                    //DefaultImbueList.Add("SlashPierce", 26);
                    //DefaultImbueList.Add("Normal", 16);
            }
        }

        public void CreatePossibleMaterialList(List<string> salvageList = null, bool multi = false) {

            PossibleMaterialList.Clear();
            List<string> salvages = new List<string>();
            string[] multipleSalvages;
            if (multi == true) {
                //Logger.WriteToChat("i'm in multi");
                for (int i = 1; i < Util.FileService.MaterialTable.Length; i++) {
                    PossibleMaterialList.Add(Util.FileService.MaterialTable.GetById(i).Id);
                    //Logger.WriteToChat(Util.FileService.MaterialTable.GetById(i).Name);
                }
            }
            else {
                //Logger.WriteToChat("i'm not in multi");
                //split items from the combo box if they contain a / and create a new list
                foreach (string salvage in salvageList) {
                    //Logger.WriteToChat(salvage.ToString());
                    if (salvage.Contains("/")) {
                        multipleSalvages = salvage.Split('/');
                        foreach (string s in multipleSalvages) {
                            salvages.Add(s);
                        }
                    }
                    else {
                        if (!salvages.Contains(salvage)) {
                            salvages.Add(salvage);
                        }
                    }
                }
                //actually add salvage material to PossibleMaterialList
                foreach (string salvage in salvages) {
                    if (!string.IsNullOrEmpty((Util.FileService.MaterialTable.GetByName(salvage).Name))) {
                       // Logger.WriteToChat("found salvage " + Util.FileService.MaterialTable.GetByName(salvage).Name);
                        PossibleMaterialList.Add(Util.FileService.MaterialTable.GetByName(salvage).Id);
                    }
                }
            }
        }

        public delegate void MyCustomEvent(int itemID, int tinkeredCount, int salvageID, double successChance);
        public event MyCustomEvent TinkerListChanged;
        public event EventHandler TinkerListFinished;
        public event MyCustomEvent AutoImbueListChanged;
        public event EventHandler AutoImbueListFinished;

        public delegate void TinkerJobChangedEvent(int salvageID, bool succeeded);
        public event TinkerJobChangedEvent TinkerJobChanged;

        public void BuildTinkerList(WorldObject targetItem, double minPercent, string runType = "single", int maxTinks = 1) {
            if (runType == "single") {
                SalvageUsedList.Clear();
                int tinkCount = targetItem.Values(LongValueKey.NumberTimesTinkered);
                int tinksRemaining = maxTinks - targetItem.Values(LongValueKey.NumberTimesTinkered);
                for (int i = 1; i <= tinksRemaining; i++) {
                    int salvBagID = GetSalvageToBeApplied(targetItem, tinkCount, minPercent);
                    if (salvBagID == 0) {
                        continue;
                    }
                    WorldObject salvBag = CoreManager.Current.WorldFilter[salvBagID];
                    //Logger.WriteToChat("tink count: " + tinkCount);
                    SalvageUsedList.Add(CoreManager.Current.WorldFilter[salvBagID].Id);
                    tinkCount++;
                }
                jobs.Add(new TinkerJob() {
                    itemID = targetItem.Id,
                    salvageToBeApplied = SalvageUsedList
                });
            }
            else if (runType == "bulkImbue") {
                List<int> ImbueSalvage = new List<int>();
                int salvBagID = GetSalvageToBeApplied(targetItem, 0);
                if (salvBagID == 0) {
                    return;
                }
                SalvageUsedList.Add(CoreManager.Current.WorldFilter[salvBagID].Id);
                ImbueSalvage.Add(CoreManager.Current.WorldFilter[salvBagID].Id);
                //Logger.WriteToChat(salvBagID.ToString());

                jobs.Add(new TinkerJob() {
                    itemID = targetItem.Id,
                    salvageToBeApplied = ImbueSalvage
                });

            }
            else if (runType == "imbue") {
                List<int> ImbueSalvage = new List<int>();
                int salvBagID = GetSalvageToBeApplied(targetItem, 0);
                if (salvBagID == 0) {
                    return;
                }
                SalvageUsedList.Add(CoreManager.Current.WorldFilter[salvBagID].Id);
                ImbueSalvage.Add(CoreManager.Current.WorldFilter[salvBagID].Id);
                ///Logger.WriteToChat(salvBagID.ToString());

                jobs.Add(new TinkerJob() {
                    itemID = targetItem.Id,
                    salvageToBeApplied = ImbueSalvage
                });
            }
            
            TinkerListFinished?.Invoke(this, EventArgs.Empty);
        }

        
        private void TinkerJob_Changed(object sender, MyEventArgs e) {
            //e.SalvageID
            //Logger.WriteToChat("I found the tinker job changed event event " + e.SalvageID.ToString() + " ---- " + e.Success.ToString() );
            TinkerJobChanged?.Invoke(e.SalvageID, e.Success);
            //UpdateUI(salvageID);
        }


        public event EventHandler TinkerJobFinished;

        public void GetTinkerableItems(string runType) {
            if (runType == "imbue") {

            }
            else if (runType == "bulktink") {

            }
        }

        private void TinkerJob_Finished(object sender, EventArgs e) {
            //Logger.WriteToChat("tinker job finished");
            jobs.Remove(jobs.First());

            if (jobs.Count > 0) {
                //Logger.WriteToChat("more jobs left");
                StartNextJob();
            }
            else {
                Logger.WriteToChat("Done tinkering");
                //ClearAllTinks();
                Stop();
                FullSalvageDict.Clear();
                TinkerableItemDict.Clear();
                tinkerJob.TinkerJobChanged -= TinkerJob_Changed;
                tinkerJob.TinkerJobFinished -= TinkerJob_Finished;
                TinkerJobFinished.Invoke(this, null);
            }
        }

        private bool CanBeImbued(int itemID) {
            WorldObject wo = CoreManager.Current.WorldFilter[itemID];
            if (wo.Values(LongValueKey.Workmanship) <= 0) {
                //Logger.WriteToChat(Util.GetObjectName(wo.Id).ToString() + " ---- item doesn't have a workmanship");
                return false;
            }

            if (wo.Values(LongValueKey.Imbued) >= 1) {
                //Logger.WriteToChat(Util.GetObjectName(wo.Id).ToString() + " ---- item is imbued");
                return false;
            }

            if (wo.Values(LongValueKey.NumberTimesTinkered) >= 10) {
                //Logger.WriteToChat(Util.GetObjectName(wo.Id).ToString() + " ---- item is already tinkered 10 times");
                return false;
            }
            return true;
        }

        public void Start() {
            tinkerJob.TinkerJobChanged += TinkerJob_Changed;
            tinkerJob.TinkerJobFinished += TinkerJob_Finished;
            StartNextJob();
        }

        private void StartNextJob() {

            if (jobs.Count > 0) {
                tinkerJob.StartTinkering(jobs.First());
            }
            else { Logger.WriteToChat("i'm out of jobs"); }
        }

        public void Stop() {
            jobs.Clear();
            ClearAll();
            tinkerJob.Stop();
            tinkerJob.TinkerJobChanged -= TinkerJob_Changed;
            tinkerJob.TinkerJobFinished -= TinkerJob_Finished;
        }

        private int GetSalvageToBeApplied(WorldObject targetItem, int tinkCount, double minPercent = 0) {
            TinkerCalc tinkerCalc = new TinkerCalc();
            foreach (var item in FullSalvageDict.OrderBy(i => i.Value)) {
                WorldObject salvWO = CoreManager.Current.WorldFilter[item.Key];
                if (salvageToBeApplied.Contains(salvWO.Values(LongValueKey.Material)) && !SalvageUsedList.Contains(salvWO.Id)) {
                    double successChance = tinkerCalc.DoCalc(salvWO.Id, targetItem, tinkCount);
                    if (successChance <= minPercent / 100) {
                        continue;
                    }
                    salvageToBeApplied.Remove(salvWO.Values(LongValueKey.Material));
                    AddNewJob(targetItem.Id, tinkCount, item.Key, successChance);
                    AddAutoImbueList(targetItem.Id, imbueCount, item.Key, successChance);
                    return item.Key;
                }
            }
            return 0;
        }

        public void ClearAll() {
            imbueCount = 0;
            salvageToBeApplied.Clear();
            PossibleMaterialList.Clear();
            SalvageUsedList.Clear();
    }

        public void BuildSalvageToBeApplied(WorldObject targetItem, string currentSalvageChoice, string runType, int maxTinks = 1) {
            int nextSal;
            if (runType == "single") {
                salvageToBeApplied.Clear();
                fakeMaxDamage = GetMaxDamage(targetItem);
                fakeVariance = targetItem.Values(DoubleValueKey.Variance);
                int tinksRemaining = maxTinks - targetItem.Values(LongValueKey.NumberTimesTinkered);
                //Logger.WriteToChat("runType: " + runType.ToString());
                for (int i = 1; i <= tinksRemaining; i++) {
                    if (currentSalvageChoice == "Granite/Iron") {
                        nextSal = GetGraniteIron(fakeMaxDamage, fakeVariance);
                    }
                    else {
                        nextSal = Util.FileService.MaterialTable.GetByName(currentSalvageChoice).Id;
                    }
                    salvageToBeApplied.Add(nextSal);
                }
            }
            else if (runType == "imbue") {
                nextSal = GetNextSalvageBag();
                if (nextSal == 0) {
                    Logger.WriteToChat("no salvage matches...  quitting");
                    return;
                }
                salvageToBeApplied.Add(CoreManager.Current.WorldFilter[nextSal].Values(LongValueKey.Material));
            }
            else if (runType == "bulkImbue") {
                nextSal = GetNextSalvageBag();
                if (nextSal == 0) {
                    Logger.WriteToChat("no salvage matches...  quitting");
                    return;
                }
                salvageToBeApplied.Add(CoreManager.Current.WorldFilter[nextSal].Values(LongValueKey.Material));
            }
            else {
                fakeMaxDamage = GetMaxDamage(targetItem);
                fakeVariance = targetItem.Values(DoubleValueKey.Variance);
                int tinksRemaining = maxTinks - targetItem.Values(LongValueKey.NumberTimesTinkered);
                for (int i = 1; i <= tinksRemaining; i++) {
                    if (targetItem.ObjectClass == ObjectClass.MeleeWeapon) {
                        nextSal = GetGraniteIron(fakeMaxDamage, fakeVariance);
                        salvageToBeApplied.Add(nextSal);
                    }
                    else {
                        //Logger.WriteToChat("made it to the right spot");
                        nextSal = GetNextSalvageBag();
                        if (nextSal == 0) {
                            Logger.WriteToChat("no salvage matches...  quitting");
                            break;
                        }
                        salvageToBeApplied.Add(CoreManager.Current.WorldFilter[nextSal].Values(LongValueKey.Material));
                    }
                }
            }
        }

        public void WriteSalvageToBeApplied() {
            foreach (var sal in salvageToBeApplied) {
                Logger.WriteToChat(sal.ToString());
            }
        }

        private int GetNextSalvageBag(int salType = 0) {
            foreach (var item in FullSalvageDict.OrderBy(i => i.Value)) {
                //Logger.WriteToChat(item.Key.ToString());
                WorldObject salvWO = CoreManager.Current.WorldFilter[item.Key];
                if (PossibleMaterialList.Contains(salvWO.Values(LongValueKey.Material)) && !SalvageUsedList.Contains(salvWO.Id)) {
                    if (salType == 0) {
                        return item.Key;
                    }
                    else {
                        if (salvWO.Values(LongValueKey.Material) == salType) {
                            return item.Key;
                        }
                    }
                }
                else {
                    continue;
                }
            }
            return 0;
        }
        public double GetDPS(double maxDamage, double variance) {
            double minDmg = maxDamage * (1 - variance);
            double critDmg = maxDamage * 2;
            double avgDmg = (maxDamage + minDmg) / 2;
            double DPS = .9 * avgDmg + .1 * critDmg;
            //Logger.WriteToChat("DPS: " + DPS.ToString());
            return DPS;
        }

        public int GetGraniteIron(double maxDamage, double variance) {
            //Logger.WriteToChat("Max Damage: " + maxDamage.ToString());
            //Logger.WriteToChat("Variance: " + variance.ToString());
            double ironDPS = GetDPS(maxDamage + 1, variance);
            double graniteDPS = GetDPS(maxDamage, variance * .8);
            if (ironDPS >= graniteDPS) {
                fakeMaxDamage = maxDamage + 1;
                return 61;
            }
            else if (graniteDPS > ironDPS) {
                fakeVariance = variance * .8;
                return 67;
            }
            else {
                return -1;
            }
        }

        public double GetMaxDamage(WorldObject targetItem) {
            double maxDamage = targetItem.Values(LongValueKey.MaxDamage);
            //Logger.WriteToChat("max damage: " + maxDamage.ToString());
            int spellCount = targetItem.SpellCount;
            for (int s = 0; s <= spellCount - 1; s++) {
                int spell = Util.FileService.SpellTable.GetById(targetItem.Spell(s)).Id;
                //int spell = GetSpellID(targetItem, s);
                if (spell == (int)2598) {
                    //Logger.WriteToChat("minor");
                    maxDamage += 2; //minor blood thirst
                }
                if (spell == (int)2586) {
                    //Logger.WriteToChat("major");
                    maxDamage += 4; //major blood thirst
                }
                if (spell == (int)4661) {
                    //Logger.WriteToChat("epic");
                    maxDamage += 7; //epic blood thirst
                }
                if (spell == (int)6089) {
                    //Logger.WriteToChat("legendary");
                    maxDamage += 10;  //legendary blood thirst
                }
            }
            maxDamage += 24;
            //Logger.WriteToChat(maxDamage.ToString());
            return maxDamage;
        }

        public void WritePossibleMaterialList() {
            foreach (var m in PossibleMaterialList) {
                //Logger.WriteToChat(m.ToString());
            }
            //Logger.WriteToChat("updated");
        }


        public List<string> GetUsableSalvage(int targetItem) {
            WorldObject targetItemWO = CoreManager.Current.WorldFilter[targetItem];

            List<string> filteredSalvageList = new List<string>();
            filteredSalvageList.Clear();

            switch (targetItemWO.ObjectClass) {
                case ObjectClass.MissileWeapon:
                    //Logger.WriteToChat("Missile");
                    filteredSalvageList.Add("Mahogany");
                    filteredSalvageList.Add("Brass");
                    filteredSalvageList.Add("Gold");
                    filteredSalvageList.Add("Moonstone");
                    filteredSalvageList.Add("Linen");
                    filteredSalvageList.Add("Pine");
                    break;
                case ObjectClass.MeleeWeapon:
                    filteredSalvageList.Add("Granite/Iron");
                    filteredSalvageList.Add("Brass");
                    filteredSalvageList.Add("Granite");
                    filteredSalvageList.Add("Iron");
                    filteredSalvageList.Add("Gold");
                    filteredSalvageList.Add("Moonstone");
                    filteredSalvageList.Add("Linen");
                    filteredSalvageList.Add("Pine");
                    //Logger.WriteToChat("Melee");
                    break;
                case ObjectClass.WandStaffOrb:
                    filteredSalvageList.Add("Brass");
                    filteredSalvageList.Add("Green Garnet");
                    filteredSalvageList.Add("Gold");
                    filteredSalvageList.Add("Moonstone");
                    filteredSalvageList.Add("Linen");
                    filteredSalvageList.Add("Pine");
                    filteredSalvageList.Add("Opal");
                    //Logger.WriteToChat("Wand");
                    break;
                case ObjectClass.Armor:
                    filteredSalvageList.Add("Steel");
                    filteredSalvageList.Add("Moonstone");
                    filteredSalvageList.Add("Linen");
                    filteredSalvageList.Add("Pine");
                    //Logger.WriteToChat("Armor");
                    break;
                case ObjectClass.Jewelry:
                    filteredSalvageList.Add("Gold");
                    filteredSalvageList.Add("Moonstone");
                    filteredSalvageList.Add("Linen");
                    filteredSalvageList.Add("Pine");
                    //Logger.WriteToChat("Jewelry");
                    break;
                default:
                    Console.WriteLine("Select an item");
                    break;
            }

            filteredSalvageList.Sort();
            return filteredSalvageList;
        }

        public void ScanInventory() {
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

        public void AddNewJob(int inputItemID, int tinkCount, int inputSalvageID, double inputSuccessChance) {
            TinkerListChanged?.Invoke(inputItemID, tinkCount, inputSalvageID, inputSuccessChance);
        }
        public void AddAutoImbueList(int inputItemID, int tinkCount, int inputSalvageID, double inputSuccessChance) {
            AutoImbueListChanged?.Invoke(inputItemID, imbueCount, inputSalvageID, inputSuccessChance);
            imbueCount++;
        }

        public int GetJobs() {
            int jobId = 0;
            foreach (TinkerJob job in jobs) {
                jobId = job.itemID;
                Logger.WriteToChat("Target item: " + Util.GetObjectName(job.itemID));
                foreach (int s in job.salvageToBeApplied) {
                    Logger.WriteToChat("salvage: " + Util.GetObjectName(s) + " " + s.ToString());
                }
            }
            return jobId;
        }
    }
}
