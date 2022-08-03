using System;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using UtilityBelt.Lib.Salvage;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Decal.Filters;

namespace UtilityBelt.Lib.Tinker {
    public class MyEventArgs : EventArgs {
        public int SalvageID { get; set; }
        public bool Success { get; set; }
        public MyEventArgs(int salvageID, bool success) {
            SalvageID = salvageID;
            Success = success;
        }
    }

    class TinkerJob {
        public int itemID { get; set; }
        public List<int> salvageToBeApplied = new List<int>();
        public List<int> salvageApplied = new List<int>();
        public float minPercent;
        private TinkerCalc tinkerCalc = new TinkerCalc();
        bool isRunning = false;
        bool tinking = false;
        WorldObject tinkeringItem;
        private string tinkeringItemName;
        WorldObject tinkeringSalvage;
        string tinkeringSalvageName;
        double tinkeringSalvageWorkmanship;
        private int tinkerCount = -1;
        TinkerJob activeJob;
        int lastSkill = 0;
        int lastTinkType = 0;

        public void StartTinkering(TinkerJob job) {
            isRunning = true;
            activeJob = job;
            Logger.Debug("Autotinker: " + Util.GetObjectName(activeJob.itemID));
            int i = 0;
            Logger.Debug("Autotinker: " + activeJob.salvageToBeApplied.Count.ToString());
            //salvagesto.First();
            //foreach (int s in activeJob.salvageToBeApplied) {
            //    i++;
            //    Logger.WriteToChat("tink #: " + i.ToString());
            //    Logger.WriteToChat(s.ToString());
            //}
            DoNextTink();
        }


        public void DoNextTink() {
            try {
                //Logger.WriteToChat("DoNextTink");

                //Logger.WriteToChat("TINKING STATUS: " + tinking.ToString());
                tinking = false;
                //Logger.WriteToChat(activeJob.salvageToBeApplied.Count.ToString());
                if (activeJob.salvageToBeApplied.Count == 0 && !tinking) {
                    Logger.Debug("done tinkering item");
                    Stop();
                    TinkerJobFinished?.Invoke(this, EventArgs.Empty);
                    return;
                }

                //Logger.WriteToChat("doing next tink");
                //Logger.WriteToChat("salvageToBeApplied: " + activeJob.salvageToBeApplied.Count());
                tinkeringItem = CoreManager.Current.WorldFilter[activeJob.itemID];
                if (tinkerCount == -1) {
                    tinkerCount = tinkeringItem.Values(LongValueKey.NumberTimesTinkered);
                }
                else {
                    tinkerCount++;
                }
                //foreach (int s in activeJob.salvageToBeApplied) {
                //    Logger.WriteToChat(s.ToString());
                //}


                //Logger.WriteToChat(tinkeringItem.Name);
                int nextSalvage = activeJob.salvageToBeApplied.First();
                //Logger.WriteToChat(nextSalvage.ToString());

                TinkerType tinkerType = new TinkerType();
                int currentTinkType = TinkerType.GetTinkerType(CoreManager.Current.WorldFilter[nextSalvage].Values(LongValueKey.Material));
                int currentSkill = tinkerType.GetRequiredTinkSkill(currentTinkType);
                if (currentSkill < lastSkill && currentTinkType == lastTinkType) {
                    Logger.WriteToChat("tinkering skill decreased... stopping tinkering.");
                    Stop();
                    TinkerJobFinished?.Invoke(this, EventArgs.Empty);
                    return;
                }
                lastTinkType = currentTinkType;
                lastSkill = currentSkill;

                FileService service = CoreManager.Current.Filter<FileService>();
                var material = service.MaterialTable.GetById(tinkeringItem.Values(LongValueKey.Material, 0));
                if (tinkeringItem.Name.Contains(material.Name)) {
                    tinkeringItemName = tinkeringItem.Name.Replace(material.Name, "");
                }
                else {
                    tinkeringItemName = tinkeringItem.Name;
                }
                tinkeringItemName = string.Format("{0} {1}", material.Name.Trim(), tinkeringItemName);
                tinkeringSalvage = CoreManager.Current.WorldFilter[nextSalvage];
                tinkeringSalvageName = Util.GetObjectName(tinkeringSalvage.Id).Replace("Salvaged ", "").Replace("Salvage ", "").Replace("Salvage", "").Replace("(100)", "").Trim();
                tinkeringSalvageWorkmanship = tinkeringSalvage.Values(DoubleValueKey.SalvageWorkmanship);
                CoreManager.Current.Actions.SelectItem(nextSalvage);

                //Logger.WriteToChat("isRunning: " + isRunning);
                if (CoreManager.Current.Actions.CurrentSelection == nextSalvage && isRunning) {

                    //Logger.WriteToChat("successChance: " + tinkerCalc.DoCalc(nextSalvage, tinkeringItem, tinkerCount).ToString());
                    //Logger.WriteToChat("Do tinks: Applying " + nextSalvage.ToString() + " to " + activeJob.itemID.ToString());

                    //Logger.WriteToChat(CoreManager.Current.WorldFilter[nextSalvage].Values(DoubleValueKey.SalvageWorkmanship).ToString("0.00"));
                    UBHelper.ConfirmationRequest.ConfirmationRequestEvent += UBHelper_ConfirmationRequest;
                    CoreManager.Current.ChatBoxMessage += Current_ChatBoxMessage;
                    activeJob.salvageToBeApplied.RemoveAt(0);
                    activeJob.salvageApplied.Add(nextSalvage);
                    tinking = true;
                    //Logger.WriteToChat("tinking status right before tink: " + tinking.ToString());
                    //Logger.WriteToChat("subscribed to chat");
                    CoreManager.Current.Actions.ApplyItem(nextSalvage, activeJob.itemID);
                }
                else { tinking = false; Logger.WriteToChat("not sure how I got here"); }
            }
            catch (Exception ex) { Logger.LogException(ex); }

        }

        public event EventHandler<MyEventArgs> TinkerJobChanged;
        public event EventHandler TinkerJobFinished;

        public void Stop() {
            tinkeringItem = null;
            tinkeringSalvage = null;
            tinkeringSalvageName = "";
            tinkeringSalvageWorkmanship = 0;
            salvageToBeApplied.Clear();
            salvageApplied.Clear();
            itemID = 0;
            isRunning = false;
            activeJob = null;
            lastSkill = 0;
            lastTinkType = 0;
            tinking = false;
            CoreManager.Current.ChatBoxMessage -= Current_ChatBoxMessage;
            UBHelper.ConfirmationRequest.ConfirmationRequestEvent -= UBHelper_ConfirmationRequest;
        }

        public void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                if (tinking && isRunning) {

                    string characterName = CoreManager.Current.CharacterFilter.Name;

                    Regex CraftSuccess = new Regex("^" + characterName + @" successfully applies the (?<salvage>[\w\s\-]+?)(\sSalvage.*)(\s?\(100\))?\s\(workmanship (?<workmanship>\d+\.\d+)\) to the (?<item>[\w\s\'\-]+)\.$");
                    Regex CraftFailure = new Regex("^" + characterName + @" fails to apply the (?<salvage>[\w\s\-]+?)(\sSalvage.*)(\s?\(100\))?\s\(workmanship (?<workmanship>\d+\.\d+)\) to the (?<item>[\w\s\'\-]+)\..*$");
                    Regex foolproofMessage = new Regex(@"^You apply the (?<salvage>.*)\.$");

                    if (CraftSuccess.IsMatch(e.Text.Trim())) {
                        CoreManager.Current.ChatBoxMessage -= Current_ChatBoxMessage;
                        var match = CraftSuccess.Match(e.Text.Trim());
                        string result = "success";
                        string chatcapSalvage = match.Groups["salvage"].Value;
                        string chatcapWK = match.Groups["workmanship"].Value;
                        string chatcapItem = match.Groups["item"].Value;
                        if (tinkeringItemName == chatcapItem && tinkeringSalvageWorkmanship.ToString("0.00") == chatcapWK.ToString() && chatcapSalvage.Trim() == tinkeringSalvageName.Trim()) {
                            Logger.Debug(result + ": " + chatcapSalvage + " " + chatcapWK + " on " + chatcapItem);
                            Logger.Debug("sending tinkerjob changed event " + tinkeringSalvage.Id);
                            SendTinkerJobEvent(tinkeringSalvage.Id, true);
                            tinking = false;
                            if (activeJob.salvageToBeApplied.Count > 0) {
                                DoNextTink();
                            }
                            else {
                                TinkerJobFinished?.Invoke(this, EventArgs.Empty);
                            }
                        }
                        else {
                            Logger.Debug("AutoTinker: did not match success" + "chat salvageWK: " + chatcapWK.ToString() + "real salvageWK: " + tinkeringSalvageWorkmanship.ToString("0.00") + "chat item name: " + chatcapItem + "real item name: " + tinkeringItemName + "chat salv name: " + chatcapSalvage + "real salv name: " + tinkeringSalvageName.Trim());
                        }
                    }
                    else if (foolproofMessage.IsMatch(e.Text.Trim())) {
                        var match = foolproofMessage.Match(e.Text.Trim());
                        string chatcapSalvage = match.Groups["salvage"].Value;
                        string result = "foolproof success";
                        if (chatcapSalvage.ToLower().Replace("foolproof", "").Trim() == tinkeringSalvageName.ToLower().Replace("foolproof", "").Trim()) {
                            Logger.Debug(result + ": " + chatcapSalvage + " on " + tinkeringItemName);
                            Logger.Debug("sending tinkerjob changed event " + tinkeringSalvage.Id);
                            SendTinkerJobEvent(tinkeringSalvage.Id, true);
                            tinking = false;
                            if (activeJob.salvageToBeApplied.Count > 0) {
                                DoNextTink();
                            }
                            else {
                                TinkerJobFinished?.Invoke(this, EventArgs.Empty);
                            }
                        }
                        else {
                            Logger.Debug("AutoTinker: did not match success... " + "real item name: " + tinkeringItemName.ToLower() + " --- chat salv name: " + chatcapSalvage.ToLower().Replace("foolproof", "").Trim() + " == real salv name: " + tinkeringSalvageName.ToLower().Replace("foolproof", "").Trim());
                        }
                    }


                    if (CraftFailure.IsMatch(e.Text.Trim())) {
                        CoreManager.Current.ChatBoxMessage -= Current_ChatBoxMessage;
                        var match = CraftFailure.Match(e.Text.Trim());
                        string result = "failure";
                        string chatcapSalvage = match.Groups["salvage"].Value.Replace(" Salvage", "");
                        string chatcapWK = match.Groups["workmanship"].Value;
                        string chatcapItem = match.Groups["item"].Value;
                        if (tinkeringItemName == chatcapItem && tinkeringSalvageWorkmanship.ToString("0.00") == chatcapWK.ToString() && chatcapSalvage == tinkeringSalvageName.Trim()) {
                            Logger.Debug(result + ": " + chatcapSalvage + " " + chatcapWK + " on " + chatcapItem);
                            SendTinkerJobEvent(tinkeringSalvage.Id, false);
                            //Logger.WriteToChat("failed job and have " + activeJob.salvageToBeApplied.Count() + " remaining");
                            Logger.Debug("done tinkering item");
                            TinkerJobFinished?.Invoke(this, EventArgs.Empty);
                            //Stop();
                        }
                        else {
                            //Logger.WriteToChat(chatcapSalvage.ToString());
                            //Logger.WriteToChat(chatcapWK.ToString());
                            //Logger.WriteToChat(chatcapItem.ToString());
                            Logger.Debug("AutoTinker: did not match failure " + "chat salvageWK:  " + chatcapWK.ToString() + " real salvageWK: " + tinkeringSalvageWorkmanship.ToString("0.00") + " chat item name: " + chatcapItem + " real item name: " + tinkeringItemName + "chat salv name: " + chatcapSalvage + "real salv name: " + tinkeringSalvageName.Trim());
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void SendTinkerJobEvent(int salvageID, bool succeeded) {
            TinkerJobChanged?.Invoke(this, new MyEventArgs(salvageID, succeeded));
        }

        private void UBHelper_ConfirmationRequest(object sender, UBHelper.ConfirmationRequest.ConfirmationRequestEventArgs e) {
            try {
                if (e.Confirm == UBHelper.ConfirmationType.CRAFT_INTERACTION) {
                    Logger.WriteToChat($"AutoTinker: Clicking Yes on {e.Text}");
                    e.ClickYes = true;
                    UBHelper.ConfirmationRequest.ConfirmationRequestEvent -= UBHelper_ConfirmationRequest;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
    }
}
