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
        #region Config
        [Summary("VitalSharing")]
        [DefaultValue(true)]
        public bool VitalSharing {
            get { return (bool)GetSetting("VitalSharing"); }
            set { UpdateSetting("VitalSharing", value); }
        }
        #endregion

        public VTankControl(UtilityBeltPlugin ub, string name) : base(ub, name) {
            if (UB.Core.CharacterFilter.LoginStatus != 0) Enable();
            else UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
        }
        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
            Enable();
        }

        public void Enable() {
            UBHelper.vTank.Enable();
            UB.Core.CharacterFilter.Logoff += CharacterFilter_Logoff;
        }

        private void CharacterFilter_Logoff(object sender, Decal.Adapter.Wrappers.LogoffEventArgs e) {
            if (e.Type == Decal.Adapter.Wrappers.LogoffEventType.Authorized)
                UBHelper.vTank.Disable();
        }
        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                    UB.Core.CharacterFilter.Logoff -= CharacterFilter_Logoff;
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
