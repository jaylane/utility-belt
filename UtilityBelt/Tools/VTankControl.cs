using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using uTank2;
using static uTank2.PluginCore;

namespace UtilityBelt.Tools {
    class VTankControl {
        private static bool navBlockDebug = false;
        private static bool itemBlockDebug = false;
        // private static bool settingDebug = true;
        public static DateTime navBlockedUntil = DateTime.MinValue;
        public static DateTime itemBlockedUntil = DateTime.MinValue;
        public static cExternalInterfaceTrustedRelay vTankInstance;
        private static Dictionary<string, List<object>> settingsStack = new Dictionary<string, List<object>>();

        public VTankControl() {
        }

        
        public static void initializeVTankInterface() {
            ConstructorInfo ctor = typeof(cExternalInterfaceTrustedRelay).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
            vTankInstance = (cExternalInterfaceTrustedRelay) ctor.Invoke(new object[] { eExternalsPermissionLevel.None });

            FieldInfo fieldInfo = vTankInstance.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(vTankInstance, 15);
        }

        public static void Decision_Lock(uTank2.ActionLockType lType, TimeSpan tSpan) {
            try {
                vTankInstance.Decision_Lock(lType, tSpan);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        public static void Decision_UnLock(uTank2.ActionLockType lType) {
            try {
                vTankInstance.Decision_UnLock(lType);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        //todo: need a global debug checkbox (with verbosity levels?)
        public static void Nav_Block(int msMax, bool debug) {
            navBlockDebug = debug;
            if (msMax > 0) {
                if (navBlockDebug) {
                    Util.WriteToChat("[Nav Blocked] for " + msMax + "ms");
                }
                TimeSpan blockDuration = TimeSpan.FromMilliseconds(msMax);
                navBlockedUntil = DateTime.UtcNow + blockDuration;
                Decision_Lock(uTank2.ActionLockType.Navigation, blockDuration);
            }
        }
        public static void Nav_UnBlock() {
            if (navBlockDebug) {
                Util.WriteToChat("[Nav Block] removed");
            }
            Decision_UnLock(uTank2.ActionLockType.Navigation);
        }

        public static void Item_Block(int msMax, bool debug) {
            itemBlockDebug = debug;
            if (msMax > 0) {
                if (itemBlockDebug) {
                    Util.WriteToChat("[Item Blocked] for " + msMax + "ms");
                }
                TimeSpan blockDuration = TimeSpan.FromMilliseconds(msMax);
                itemBlockedUntil = DateTime.UtcNow + blockDuration;
                Decision_Lock(uTank2.ActionLockType.ItemUse, blockDuration);
            }
        }
        public static void Item_UnBlock() {
            if (itemBlockDebug) {
                Util.WriteToChat("[Item Block] removed");
            }
            Decision_UnLock(uTank2.ActionLockType.ItemUse);
        }
        /*
         * Commented dead code
        public static bool PushSetting(string setting, object value) {
            try {
                if (!settingsStack.ContainsKey(setting)) {
                    if (settingDebug)
                        Util.WriteToChat("PushSetting: Add New settingStack List:" + setting);
                    settingsStack.Add(setting, new List<object>());
                }

                settingsStack[setting].Add(GetSetting(setting));
                
                SetSetting(setting, value);

                if (settingDebug)
                    Util.WriteToChat("PushSetting: settingStack[" + setting + "]=" + value + " (" + settingsStack[setting].Count + " entries)");
                return true;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        public static object PopSetting(string setting) {
            try {
                if (!settingsStack.ContainsKey(setting) || settingsStack[setting].Count == 0) {
                    if (settingDebug)
                        Util.WriteToChat("PopSetting: settingStack[" + setting + "] not found");
                    return false;
                }
                var value = settingsStack[setting].Last();
                settingsStack[setting].RemoveAt(settingsStack[setting].Count - 1);

                SetSetting(setting, value);

                if (settingDebug)
                    Util.WriteToChat("PopSetting: settingStack[" + setting + "] restored to "+ value);
                return true;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        private static bool SetSetting(string setting, object value) {
            try {
                Type type = vTankInstance.GetSettingType(setting);

                if (type == typeof(bool)) {
                    vTankInstance.SetSetting(setting, (bool)value);
                }
                else if (type == typeof(string)) {
                    vTankInstance.SetSetting(setting, (string)value);
                }
                else if (type == typeof(double)) {
                    vTankInstance.SetSetting(setting, (double)value);
                }
                else if (type == typeof(float)) {
                    vTankInstance.SetSetting(setting, (float)value);
                }
                else if (type == typeof(int)) {
                    vTankInstance.SetSetting(setting, (int)value);
                }
                else if (type == typeof(object)) {
                    vTankInstance.SetSetting(setting, (object)value);
                }
                else {
                    Util.WriteToChat("bad vtank setting type... " + type.ToString());
                    return false;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return true;
        }
        */
        internal static object GetSetting(string setting) {
            try {
                return vTankInstance.GetSetting(setting);
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }
    }
}
