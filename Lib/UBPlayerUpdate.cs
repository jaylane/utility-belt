using SharedMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {

    public class UBPlayerUpdate {
        public int PlayerID = 0;
        public bool HasHealthInfo = false;
        public bool HasManaInfo = false;
        public bool HasStamInfo = false;
        public int curHealth = 0;
        public int curStam = 0;
        public int curMana = 0;
        public int maxHealth = 0;
        public int maxStam = 0;
        public int maxMana = 0;
        public DateTime lastUpdate = DateTime.UtcNow;

        public string Server = "Unknown";

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
            
            if (serverNameLength > 0) {
                var serverNameBuffer = new char[serverNameLength];
                for (var i = 0; i < serverNameLength; i++) {
                    sharedBuffer.Read(out char c, offset); offset += sizeof(char);
                    serverNameBuffer[i] = c;
                }

                Server = new string(serverNameBuffer);
            }

            return offset;
        }
    }
}
