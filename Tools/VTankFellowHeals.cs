using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using uTank2;
using static uTank2.PluginCore;
using Decal.Adapter.Wrappers;
using System.Runtime.InteropServices;
using SharedMemory;

namespace UtilityBelt.Tools {
    public struct UBPlayerUpdate {
        public int PlayerID;
        public bool HasHealthInfo;
        public bool HasManaInfo;
        public bool HasStamInfo;
        public int curHealth;
        public int curStam;
        public int curMana;
        public int maxHealth;
        public int maxStam;
        public int maxMana;
        public DateTime lastUpdate;

        public string Server;

        public int Serialize(BufferReadWrite sharedBuffer, int offset) {
            sharedBuffer.Write(ref PlayerID, offset); offset += sizeof(int);
            sharedBuffer.Write(ref HasHealthInfo, offset); offset += sizeof(bool);
            sharedBuffer.Write(ref HasManaInfo, offset); offset += sizeof(bool);
            sharedBuffer.Write(ref HasStamInfo, offset); offset += sizeof(bool);
            sharedBuffer.Write(ref curHealth, offset); offset += sizeof(int);
            sharedBuffer.Write(ref curStam, offset); offset += sizeof(int);
            sharedBuffer.Write(ref curMana, offset); offset += sizeof(int);
            sharedBuffer.Write(ref maxHealth, offset); offset += sizeof(int);
            sharedBuffer.Write(ref maxStam, offset); offset += sizeof(int);
            sharedBuffer.Write(ref maxMana, offset); offset += sizeof(int);

            double lastUpdateUnixTime = (Int32)(lastUpdate.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            sharedBuffer.Write(ref lastUpdateUnixTime, offset); offset += sizeof(double);

            var serverName = Server.ToCharArray();
            int serverNameLength = serverName.Length;
            sharedBuffer.Write(ref serverNameLength, offset); offset += sizeof(int);

            for (var i = 0; i < serverNameLength; i++) {
                char c = serverName[i];
                sharedBuffer.Write(ref c, offset); offset += sizeof(char);
            }

            return offset;
        }

        public int Deserialize(BufferReadWrite sharedBuffer, int offset) {
            sharedBuffer.Read(out PlayerID, offset); offset += sizeof(int);
            sharedBuffer.Read(out HasHealthInfo, offset); offset += sizeof(bool);
            sharedBuffer.Read(out HasManaInfo, offset); offset += sizeof(bool);
            sharedBuffer.Read(out HasStamInfo, offset); offset += sizeof(bool);
            sharedBuffer.Read(out curHealth, offset); offset += sizeof(int);
            sharedBuffer.Read(out curStam, offset); offset += sizeof(int);
            sharedBuffer.Read(out curMana, offset); offset += sizeof(int);
            sharedBuffer.Read(out maxHealth, offset); offset += sizeof(int);
            sharedBuffer.Read(out maxStam, offset); offset += sizeof(int);
            sharedBuffer.Read(out maxMana, offset); offset += sizeof(int);
            sharedBuffer.Read(out double lastUpdateUnixTime, offset); offset += sizeof(double);

            lastUpdate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            lastUpdate = lastUpdate.AddSeconds(lastUpdateUnixTime);

            sharedBuffer.Read(out int serverNameLength, offset); offset += sizeof(int);

            var serverNameBuffer = new char[serverNameLength];
            for (var i = 0; i < serverNameLength; i++) {
                sharedBuffer.Read(out char c, offset); offset += sizeof(char);
                serverNameBuffer[i] = c;
            }

            Server = new string(serverNameBuffer);

            return offset;
        }
    }

    public class VTankFellowHeals : IDisposable {
        cExternalInterfaceTrustedRelay vTank;
        private BufferReadWrite sharedBuffer;
        DateTime lastThought = DateTime.UtcNow;
        DateTime lastUpdate = DateTime.MinValue;

        public const string BUFFER_NAME = "UtilityBeltyVTankFellowHealsBuffer";
        public const int UPDATE_TIMEOUT = 10; // seconds
        public const int UPDATE_INTERVAL = 1; // seconds
        public int UPDATE_SIZE = Marshal.SizeOf(typeof(UBPlayerUpdate));
        public int BUFFER_SIZE = Marshal.SizeOf(typeof(UBPlayerUpdate)) * 200;

        public VTankFellowHeals() {
            vTank = VTankControl.GetVTankInterface();

            try {
                sharedBuffer = new SharedMemory.BufferReadWrite(BUFFER_NAME, BUFFER_SIZE);
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

                sharedBuffer.Read<int>(out recordCount);

                //Util.WriteToChat($"UpdateMySharedVitals: recordCount:{recordCount}");

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
                            //Util.WriteToChat("Marking player as invalid: " + update.PlayerID.ToString() + " on server " + update.Server);
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
