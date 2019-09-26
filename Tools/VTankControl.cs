using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using uTank2;
using static uTank2.PluginCore;

namespace UtilityBelt.Tools {
    class VTankControl {
        private static cExternalInterfaceTrustedRelay vTank;

        public VTankControl() {
        }

        public static cExternalInterfaceTrustedRelay GetVTankInterface() {
            if (vTank != null && vTank.PermissionLevel == eExternalsPermissionLevel.FullUnderlying) return vTank;

            ConstructorInfo ctor = typeof(cExternalInterfaceTrustedRelay).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
            vTank = (cExternalInterfaceTrustedRelay)ctor.Invoke(new object[] { eExternalsPermissionLevel.None });

            FieldInfo fieldInfo = vTank.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(vTank, eExternalsPermissionLevel.FullUnderlying);

            return vTank;
        }
    }
}
