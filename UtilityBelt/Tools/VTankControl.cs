using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using uTank2;
using UtilityBelt.Lib;
using static uTank2.PluginCore;

namespace UtilityBelt.Tools {
    [Name("VTank")]
    public class VTankControl : ToolBase {
        // TODO: remove these and switch to Plugin.Debug, or Logger.Debug?
        private static bool navBlockDebug = false;
        private static bool itemBlockDebug = false;
        public static DateTime navBlockedUntil = DateTime.MinValue;
        public static DateTime itemBlockedUntil = DateTime.MinValue;
        private static cExternalInterfaceTrustedRelay _vTankInstance;

        #region Config
        [Summary("VitalSharing")]
        [DefaultValue(true)]
        public bool VitalSharing {
            get { return (bool)GetSetting("VitalSharing"); }
            set { UpdateSetting("VitalSharing", value); }
        }
        #endregion

        public static cExternalInterfaceTrustedRelay vTankInstance {
            get {
                if (_vTankInstance != null) return _vTankInstance;

                ConstructorInfo ctor = typeof(cExternalInterfaceTrustedRelay).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
                _vTankInstance = (cExternalInterfaceTrustedRelay)ctor.Invoke(new object[] { eExternalsPermissionLevel.None });

                FieldInfo fieldInfo = vTankInstance.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInfo.SetValue(vTankInstance, 15);

                return _vTankInstance;
            }
        }
        private static Dictionary<string, List<object>> settingsStack = new Dictionary<string, List<object>>();

        public VTankControl(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public static void Decision_Lock(uTank2.ActionLockType lType, TimeSpan tSpan) {
            try {
                vTankInstance?.Decision_Lock(lType, tSpan);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        public static void Decision_UnLock(uTank2.ActionLockType lType) {
            try {
                vTankInstance?.Decision_UnLock(lType);
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

        internal static object GetVTankSetting(string setting) {
            try {
                return vTankInstance?.GetSetting(setting);
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }
    }
}
