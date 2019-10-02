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
        private static Dictionary<string, List<object>> settingsStack = new Dictionary<string, List<object>>();

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

        public static bool PushSetting(string setting, object value) {
            try {
                if (!settingsStack.ContainsKey(setting)) {
                    settingsStack.Add(setting, new List<object>());
                }

                var vTankSettingsReader = GetVTankInterface(eExternalsPermissionLevel.ReadSettings);
                var vTankSettingsWriter = GetVTankInterface(eExternalsPermissionLevel.WriteSettings);
                settingsStack[setting].Add(GetSetting(setting));
                
                SetSetting(setting, value);

                return true;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        public static object PopSetting(string setting) {
            try {
                if (!settingsStack.ContainsKey(setting) || settingsStack[setting].Count == 0) {
                    return false;
                }

                var vTankSettingsReader = GetVTankInterface(eExternalsPermissionLevel.ReadSettings);
                var vTankSettingsWriter = GetVTankInterface(eExternalsPermissionLevel.WriteSettings);
                var value = settingsStack[setting].Last();
                settingsStack[setting].RemoveAt(settingsStack[setting].Count - 1);

                SetSetting(setting, value);

                return true;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        private static bool SetSetting(string setting, object value) {
            try {
                var vTankSettingsReader = GetVTankInterface(eExternalsPermissionLevel.ReadSettings);
                var vTankSettingsWriter = GetVTankInterface(eExternalsPermissionLevel.WriteSettings);
                Type type = vTankSettingsReader.GetSettingType(setting);

                if (type == typeof(bool)) {
                    vTankSettingsWriter.SetSetting(setting, (bool)value);
                }
                else if (type == typeof(string)) {
                    vTankSettingsWriter.SetSetting(setting, (string)value);
                }
                else if (type == typeof(double)) {
                    vTankSettingsWriter.SetSetting(setting, (double)value);
                }
                else if (type == typeof(float)) {
                    vTankSettingsWriter.SetSetting(setting, (float)value);
                }
                else if (type == typeof(int)) {
                    vTankSettingsWriter.SetSetting(setting, (int)value);
                }
                else if (type == typeof(object)) {
                    vTankSettingsWriter.SetSetting(setting, (object)value);
                }
                else {
                    Util.WriteToChat("bad vtank setting type... " + type.ToString());
                    return false;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return true;
        }

        internal static object GetSetting(string setting) {
            try {
                var vTankSettingsReader = GetVTankInterface(eExternalsPermissionLevel.ReadSettings);
                return vTankSettingsReader.GetSetting(setting);
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }
    }
}
