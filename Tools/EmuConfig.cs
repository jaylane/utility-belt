using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Timers;
using System.Linq;
using System.Xml;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using MyClasses.MetaViewWrappers;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using VirindiViewService.Controls;
using System.Data;
using System.Data.Linq;
using UtilityBelt.Constants;

namespace UtilityBelt.Tools {
    class EmuConfig : IDisposable {

        HudList UIEmuConfigList { get; set; }

        DataTable emuConfigDataTable = new DataTable();
        DateTime lastRequestedConfigList;
        


        private bool disposed = false;
        private static readonly Regex EmuConfigRegex = new Regex(@"(?<ConfigHeader>.*) settings:\n(?<ConfigValues>.*)");
        private static readonly Regex EmuConfigChangedRegex = new Regex(@"Character option (?<changedConfig>.*) is now (?<changedStatus>.*).");
        

        public EmuConfig() {
            UIEmuConfigList = (HudList)Globals.MainView.view["EmuConfigList"];
            UIEmuConfigList.Click += new HudList.delClickedControl(EmuConfigList_Click);
            //UIConfigCheckBox = Globals.View.view != null ? (HudCheckBox)Globals.View.view["ConfigCheckBox"] : new HudCheckBox();
            Globals.Core.ChatBoxMessage += new EventHandler<ChatTextInterceptEventArgs>(Current_ChatBoxMessage);
            lastRequestedConfigList = DateTime.UtcNow;
            Globals.Core.Actions.InvokeChatParser("/config list");

        }

        
        private void EmuConfigList_Click(object sender, int row, int col) {
            try {
                //Util.WriteToChat("row: " + row + " column:" + col);
                HudList.HudListRowAccessor myRow = UIEmuConfigList[row];
                string configName = ((HudStaticText)myRow[0]).Text.Replace(" ", "");
                //Util.WriteToChat("Toggling " + configName);
                ToggleConfig(configName);
            } catch (Exception ex) { Logger.LogException(ex); }
        } 



        public void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                Match match = EmuConfigRegex.Match(e.Text);
                Match configChange = EmuConfigChangedRegex.Match(e.Text);
            //ChatHandler:
                if (DateTime.UtcNow - lastRequestedConfigList < TimeSpan.FromSeconds(2)) {
                    if (match.Success) {
                        e.Eat = true;


                        string configHeader = match.Groups["ConfigHeader"].Value;
                        string configValues = match.Groups["ConfigValues"].Value;

                        string[] configValuesArray = (configValues.Split(','));

                        HudList.HudListRowAccessor newHeaderRow = UIEmuConfigList.AddRow();
                        ((HudStaticText)newHeaderRow[0]).Text = configHeader.Trim() + " settings";
                        ((HudStaticText)newHeaderRow[0]).TextColor = System.Drawing.Color.Goldenrod;
                        ((HudCheckBox)newHeaderRow[1]).Visible = false;


                        //Util.WriteToChat(configHeader.Trim());
                        foreach (string configValue in configValuesArray) {
                            //Util.WriteToChat(configValue.Trim());


                            if (Enum.IsDefined(typeof(CharOptions), configValue.Trim())) {
                                uint charOptionsValue = (uint)Enum.Parse(typeof(CharOptions), configValue.Trim());
                                //if ((Globals.Core.CharacterFilter.CharacterOptions & 0x00000002) != 0) { "this setting is on" };
                                if ((uint)(Globals.Core.CharacterFilter.CharacterOptions & charOptionsValue) != 0) {
                                    HudList.HudListRowAccessor newValueRow = UIEmuConfigList.AddRow();
                                    ((HudStaticText)newValueRow[0]).Text = "        " + Regex.Replace(configValue, "([A-Z]+)", " $1").Trim();
                                    ((HudCheckBox)newValueRow[1]).Checked = true;
                                    //Util.WriteToChat("       " + configValue.Trim());
                                    //Util.WriteToChat(configValue.Trim() + " status: On");
                                } else {
                                    HudList.HudListRowAccessor newValueRow = UIEmuConfigList.AddRow();
                                    ((HudStaticText)newValueRow[0]).Text = "        " + Regex.Replace(configValue, "([A-Z]+)", " $1").Trim();
                                    ((HudCheckBox)newValueRow[1]).Checked = false;
                                    //Util.WriteToChat(configValue.Trim() + " status: Off");
                                }
                            }
                            if (Enum.IsDefined(typeof(CharOptions2), configValue.Trim())) {
                                uint charOptions2Value = (uint)Enum.Parse(typeof(CharOptions2), configValue.Trim());
                                if ((Globals.Core.CharacterFilter.CharacterOptionFlags & charOptions2Value) != 0) {
                                    HudList.HudListRowAccessor newValueRow = UIEmuConfigList.AddRow();
                                    ((HudStaticText)newValueRow[0]).Text = "        " + Regex.Replace(configValue, "([A-Z]+)", " $1").Trim();
                                    ((HudCheckBox)newValueRow[1]).Checked = true;
                                    //Util.WriteToChat("       " + configValue.Trim());
                                    //Util.WriteToChat(configValue.Trim() + " status: On");
                                } else {
                                    HudList.HudListRowAccessor newValueRow = UIEmuConfigList.AddRow();
                                    ((HudStaticText)newValueRow[0]).Text = "        " + Regex.Replace(configValue, "([A-Z]+)", " $1").Trim();
                                    ((HudCheckBox)newValueRow[1]).Checked = false;
                                    //Util.WriteToChat(configValue.Trim() + " status: Off");
                                }
                            }
                        }
                    } else {
                        //Util.WriteToChat("did not find" + e.Text);
                    }
                }
                
            if (configChange.Success) {
                string changedConfig = configChange.Groups["changedConfig"].Value;
                string changedStatus = configChange.Groups["changedStatus"].Value;
                bool changedStatusBool;
                    //Util.WriteToChat("changedConfig: " + changedConfig + " changedStatus: " + changedStatus);
                    if (changedStatus == "on") {
                        changedStatusBool = true;
                    } else {
                        changedStatusBool = false;
                    }

                    for (int i = 0; i < UIEmuConfigList.RowCount; i++) {
                        HudList.HudListRowAccessor row = UIEmuConfigList[i];
                        string configName2 = ((HudStaticText)row[0]).Text.Replace(" ", "");
                        //Util.WriteToChat("configName : " + configName2);
                        if (configName2 == changedConfig) {
                        //    Util.WriteToChat(changedConfig);
                            ((HudCheckBox)row[1]).Checked = changedStatusBool;
                        }
                    }
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        //HudList.HudListRowAccessor myRow = UIMyKillTaskList[row];
        //string configName = ToCamelCase(((HudStaticText)myRow[0]).Text)

        private void ConfigToggled(string setting) {
            
        }

        // sends a /config command to toggle a setting
        private void ToggleConfig(string setting) {
            try {
                Util.DispatchChatToBoxWithPluginIntercept(string.Format("/config {0}", setting));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
            
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.ChatBoxMessage -= new EventHandler<ChatTextInterceptEventArgs>(Current_ChatBoxMessage);
                    UIEmuConfigList.Click -= new HudList.delClickedControl(EmuConfigList_Click);
                }
                disposed = true;
            }
        }
    }
}
