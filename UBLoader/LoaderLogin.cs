using System;
using System.Net.Sockets;

namespace UBLoader {
    public class LoaderLogin {
        static uint id;

        public static void SetNextLogin(uint nextIndex) => id = nextIndex;
        public static void ClearNextLogin() => id = 0;

        public unsafe static void Login() {
            if (id == 0)
                return;
            if (!UBHelper.Core.CharacterSet.ContainsKey((int)id))
                FilterCore.LogError($"Error automatically logging in with: {id}");
            AcClient.CPlayerSystem.GetPlayerSystem()->LogOnCharacter(id);
        }
    }
}


