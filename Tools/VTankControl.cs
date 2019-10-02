using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using uTank2;
using static uTank2.PluginCore;

namespace UtilityBelt.Tools {
    class VTankControl {
        private static Dictionary<eExternalsPermissionLevel, cExternalInterfaceTrustedRelay> vTankInstances = new Dictionary<eExternalsPermissionLevel, cExternalInterfaceTrustedRelay>();
        private static Dictionary<string, List<bool>> settingsStack = new Dictionary<string, List<bool>>();

        public VTankControl() {
        }

        public static cExternalInterfaceTrustedRelay GetVTankInterface(eExternalsPermissionLevel permissionLevel) {
            if (vTankInstances.ContainsKey(permissionLevel)) {
                return vTankInstances[permissionLevel];
            }

            cExternalInterfaceTrustedRelay vTank;
            ConstructorInfo ctor = typeof(cExternalInterfaceTrustedRelay).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
            vTank = (cExternalInterfaceTrustedRelay)ctor.Invoke(new object[] { eExternalsPermissionLevel.None });

            FieldInfo fieldInfo = vTank.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(vTank, permissionLevel);

            vTankInstances.Add(permissionLevel, vTank);

            return vTank;
        }

        public static bool PushSetting(string setting, bool value) {
            try {
                if (!settingsStack.ContainsKey(setting)) {
                    settingsStack.Add(setting, new List<bool>());
                }

                var vTankSettingsReader = GetVTankInterface(eExternalsPermissionLevel.ReadSettings);
                var vTankSettingsWriter = GetVTankInterface(eExternalsPermissionLevel.WriteSettings);

                settingsStack[setting].Add((bool)vTankSettingsReader.GetSetting(setting));

                vTankSettingsWriter.SetSetting(setting, value);

                return true;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        public static bool PopSetting(string setting) {
            try {
                if (!settingsStack.ContainsKey(setting) || settingsStack[setting].Count == 0) {
                    return false;
                }

                var vTankSettingsWriter = GetVTankInterface(eExternalsPermissionLevel.WriteSettings);
                var value = settingsStack[setting].Last();
                settingsStack[setting].RemoveAt(settingsStack[setting].Count - 1);

                vTankSettingsWriter.SetSetting(setting, value);

                return true;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }
    }
}
