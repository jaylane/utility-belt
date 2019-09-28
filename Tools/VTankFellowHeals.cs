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

namespace UtilityBelt.Tools {

    public class VTankFellowHeals : IDisposable {
        cExternalInterfaceTrustedRelay vTank;
        private BufferReadWrite sharedBuffer;
        DateTime lastThought = DateTime.UtcNow;
        DateTime lastUpdate = DateTime.MinValue;

        public const string BUFFER_NAME = "UtilityBeltyVTankFellowHealsBuffer";
        public const int UPDATE_TIMEOUT = 5; // seconds
        public const int UPDATE_INTERVAL = 2; // seconds
        public int BUFFER_SIZE = 1024 * 1024;

        public VTankFellowHeals() {
            vTank = VTankControl.GetVTankInterface(eExternalsPermissionLevel.FullUnderlying);

            try {
                sharedBuffer = new SharedMemory.BufferReadWrite(BUFFER_NAME, BUFFER_SIZE);

                // write a blank record count if we are the first ones here
                var d = 0;
                sharedBuffer.Write<int>(ref d);
            }
            catch (Exception ex) {
                sharedBuffer = new SharedMemory.BufferReadWrite(BUFFER_NAME);
            }

            Globals.Core.CharacterFilter.ChangeVital += CharacterFilter_ChangeVital;
        }

        private void CharacterFilter_ChangeVital(object sender, ChangeVitalEventArgs e) {
            try {
                UpdateMySharedVitals();
            }
            catch (Exception ex) { Logger.LogException(ex);  }
        }

        public bool HasVTank() {
            return vTank != null;
        }

        public void UpdateMySharedVitals() {
            if (!HasVTank()) return;

            UBPlayerUpdate playerUpdate = GetMyPlayerUpdate();

            try {
                int recordCount = 0;
                var updates = new List<UBPlayerUpdate>();
                var i = 0;

                using (var mutex = new Mutex(false, "UtilityBelt.VTankFellowHeals.SharedMemory")) {
                    if (!mutex.WaitOne(TimeSpan.FromMilliseconds(50), false)) {
                        return;
                    }

                    sharedBuffer.Read<int>(out recordCount);

                    int offset = sizeof(int);
                    while (i < recordCount && offset <= sharedBuffer.BufferSize) {
                        UBPlayerUpdate update = new UBPlayerUpdate();
                        offset = update.Deserialize(sharedBuffer, offset);

                        if (update.PlayerID != Globals.Core.CharacterFilter.Id && DateTime.UtcNow - update.lastUpdate <= TimeSpan.FromSeconds(UPDATE_TIMEOUT)) {
                            updates.Add(update);
                            UpdateVtankVitalInfo(update);
                        }
                        else if (update.PlayerID != Globals.Core.CharacterFilter.Id) {
                            if (HasVTank()) {
                                Util.WriteToChat("Marking player as invalid: " + update.PlayerID.ToString() + " on server " + update.Server);
                                vTank.HelperPlayerSetInvalid(update.PlayerID);
                            }
                        }

                        i++;
                    }

                    var newRecordCount = updates.Count + 1;

                    sharedBuffer.Write(ref newRecordCount, 0);
                    offset = playerUpdate.Serialize(sharedBuffer, sizeof(int));

                    //Util.WriteToChat($"Wrote newRecordCount:{newRecordCount} w/ id:{playerUpdate.PlayerID} stam:{playerUpdate.curStam}/{playerUpdate.maxStam}");

                    for (var x = 0; x < updates.Count; x++) {
                        offset = updates[x].Serialize(sharedBuffer, offset);
                    }

                    lastUpdate = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateVtankVitalInfo(UBPlayerUpdate update) {
            try {
                if (!HasVTank()) return;
                if (update.Server != Globals.Core.CharacterFilter.Server) return;

                //Util.WriteToChat($"Updating vital info for {update.PlayerID} stam:{update.curStam}/{update.maxStam}");
                
                vTank.HelperPlayerUpdate(new sPlayerInfoUpdate() {
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
                });
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Think() {
            if (DateTime.UtcNow - lastUpdate > TimeSpan.FromSeconds(UPDATE_INTERVAL)) {
                UpdateMySharedVitals();
            }
        }

        private UBPlayerUpdate GetMyPlayerUpdate() {
            return new UBPlayerUpdate {
                PlayerID = Globals.Core.CharacterFilter.Id,

                HasHealthInfo = true,
                HasManaInfo = true,
                HasStamInfo = true,

                curHealth = Globals.Core.CharacterFilter.Vitals[CharFilterVitalType.Health].Current,
                curMana = Globals.Core.CharacterFilter.Vitals[CharFilterVitalType.Mana].Current,
                curStam = Globals.Core.CharacterFilter.Vitals[CharFilterVitalType.Stamina].Current,

                maxHealth = Globals.Core.CharacterFilter.EffectiveVital[CharFilterVitalType.Health],
                maxMana = Globals.Core.CharacterFilter.EffectiveVital[CharFilterVitalType.Mana],
                maxStam = Globals.Core.CharacterFilter.EffectiveVital[CharFilterVitalType.Stamina],

                lastUpdate = DateTime.UtcNow,

                Server = Globals.Core.CharacterFilter.Server
            };
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    sharedBuffer.Dispose();

                    Globals.Core.CharacterFilter.ChangeVital -= CharacterFilter_ChangeVital;
                }
                
                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
        }
        #endregion
    }
}
