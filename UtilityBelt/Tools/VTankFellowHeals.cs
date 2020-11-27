using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using uTank2;
using static uTank2.PluginCore;
using Decal.Adapter.Wrappers;
using System.Runtime.InteropServices;
using SharedMemory;
using UtilityBelt.Lib;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;

namespace UtilityBelt.Tools {
    [Name("VTankFellowHeals")]
    [Summary("Automatically forwards vital information to VTank.")]
    [FullDescription(@"
If enabled, this will automatically share vital information for all clients on the same PC with VTank.

This allows VTank to heal/restam/remana characters on your same pc, even when they do not share in ingame fellowship.
    ")]
    public class VTankFellowHeals : ToolBase {
        private BufferReadWrite sharedBuffer;
        DateTime lastThought = DateTime.UtcNow;
        DateTime lastUpdate = DateTime.MinValue;

        private const string BUFFER_NAME = "UtilityBeltyVTankFellowHealsBuffer";
        private const int UPDATE_TIMEOUT = 6000; // ms
        private const int UPDATE_INTERVAL = 500; // ms
        private int BUFFER_SIZE = 1024 * 1024;

        public VTankFellowHeals(UtilityBeltPlugin ub, string name) : base(ub, name) {
            try {
                sharedBuffer = new SharedMemory.BufferReadWrite(BUFFER_NAME, BUFFER_SIZE);

                // write a blank record count if we are the first ones here
                var d = 0;
                sharedBuffer.Write<int>(ref d);
            }
            catch {
                sharedBuffer = new SharedMemory.BufferReadWrite(BUFFER_NAME);
            }

            UB.Core.CharacterFilter.ChangeVital += CharacterFilter_ChangeVital;
            UB.Core.RenderFrame += Core_RenderFrame;
        }

        private void CharacterFilter_ChangeVital(object sender, ChangeVitalEventArgs e) {
            try {
                UpdateMySharedVitals();
            }
            catch (Exception ex) { Logger.LogException(ex);  }
        }

        public void UpdateMySharedVitals() {
            if (UBHelper.vTank.Instance == null || !UB.VTank.VitalSharing) return;
            UBPlayerUpdate playerUpdate;
            try {
                playerUpdate = GetMyPlayerUpdate();
            }
            catch {
                return;
            }

            try {
                int recordCount = 0;
                var updates = new List<UBPlayerUpdate>();
                var i = 0;
                int offset = sizeof(int);

                using (var mutex = new Mutex(false, "UtilityBelt.VTankFellowHeals.SharedMemory")) {
                    try {
                        if (!mutex.WaitOne(TimeSpan.FromMilliseconds(20), true)) {
                            return;
                        }
                    }
                    catch (AbandonedMutexException) {
                        return;
                    }

                    try {
                        sharedBuffer.Read<int>(out recordCount, 0);
                        while (i < recordCount && offset <= sharedBuffer.BufferSize) {
                            UBPlayerUpdate update = new UBPlayerUpdate();
                            offset = update.Deserialize(sharedBuffer, offset);

                            if (update.PlayerID != UB.Core.CharacterFilter.Id && DateTime.UtcNow - update.lastUpdate <= TimeSpan.FromMilliseconds(UPDATE_TIMEOUT)) {
                                updates.Add(update);
                                UpdateVTankVitalInfo(update);
                            }
                            else if (update.PlayerID != UB.Core.CharacterFilter.Id) {
                                //Util.WriteToChat("Marking player as invalid: " + update.PlayerID.ToString() + " on server " + update.Server);
                                UBHelper.vTank.Instance?.HelperPlayerSetInvalid(update.PlayerID);
                            }

                            i++;
                        }
                    }
                    catch {
                        try {
                            sharedBuffer.Write<int>(ref recordCount, 0);
                        }
                        catch { }
                        return;
                    }

                    var newRecordCount = updates.Count + 1;

                    //Util.WriteToChat($"Wrote newRecordCount:{newRecordCount} w/ id:{playerUpdate.PlayerID} stam:{playerUpdate.curStam}/{playerUpdate.maxStam}");

                    try {
                        sharedBuffer.Write(ref newRecordCount, 0);
                        offset = playerUpdate.Serialize(sharedBuffer, sizeof(int));
                        for (var x = 0; x < updates.Count; x++) {
                            offset = updates[x].Serialize(sharedBuffer, offset);
                        }
                    }
                    catch {
                        newRecordCount = 0;
                        try {
                            sharedBuffer.Write<int>(ref recordCount, 0);
                        }
                        catch { }
                    }

                    lastUpdate = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateVTankVitalInfo(UBPlayerUpdate update) {
            try {
                if (UBHelper.vTank.Instance == null || update == null) return;
                if (update.Server != UB.Core.CharacterFilter.Server) return;

                //Util.WriteToChat($"Updating vital info for {update.PlayerID} stam:{update.curStam}/{update.maxStam}");

                var helperUpdate = new sPlayerInfoUpdate() {
                    PlayerID = update.PlayerID,
                    HasHealthInfo = update.HasHealthInfo,
                    HasManaInfo = update.HasManaInfo,
                    HasStamInfo = update.HasStamInfo,
                    curHealth = update.curHealth,
                    curMana = update.curMana,
                    curStam = update.curStam,
                    maxHealth = update.maxHealth,
                    maxMana = update.maxMana,
                    maxStam = update.maxStam
                };

                if (helperUpdate != null) {
                    UBHelper.vTank.Instance.HelperPlayerUpdate(helperUpdate);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (DateTime.UtcNow - lastUpdate > TimeSpan.FromMilliseconds(UPDATE_INTERVAL)) {
                    if (UBHelper.vTank.Instance == null || !UB.VTank.VitalSharing) return;

                    UpdateMySharedVitals();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private UBPlayerUpdate GetMyPlayerUpdate() {
            return new UBPlayerUpdate {
                PlayerID = UB.Core.CharacterFilter.Id,

                HasHealthInfo = true,
                HasManaInfo = true,
                HasStamInfo = true,

                curHealth = UB.Core.CharacterFilter.Vitals[CharFilterVitalType.Health].Current,
                curMana = UB.Core.CharacterFilter.Vitals[CharFilterVitalType.Mana].Current,
                curStam = UB.Core.CharacterFilter.Vitals[CharFilterVitalType.Stamina].Current,

                maxHealth = UB.Core.CharacterFilter.EffectiveVital[CharFilterVitalType.Health],
                maxMana = UB.Core.CharacterFilter.EffectiveVital[CharFilterVitalType.Mana],
                maxStam = UB.Core.CharacterFilter.EffectiveVital[CharFilterVitalType.Stamina],

                lastUpdate = DateTime.UtcNow,

                Server = UB.Core.CharacterFilter.Server
            };
        }

        #region IDisposable Support
        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.CharacterFilter.ChangeVital -= CharacterFilter_ChangeVital;
                    UB.Core.RenderFrame -= Core_RenderFrame;

                    sharedBuffer.Dispose();

                    base.Dispose(disposing);
                }
                
                disposedValue = true;
            }
        }
        #endregion
    }
}
