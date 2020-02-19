using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Lib;

namespace UtilityBelt.Tools {
    [Name("Fellowships")]
    public class FellowshipManager : ToolBase {

        #region Expressions
        #region getfellowshipstatus[]
        [ExpressionMethod("getfellowshipstatus")]
        [ExpressionReturn(typeof(string), "Returns 1 if you are in a fellowship, 0 otherwise")]
        [Summary("Checks if you are currently in a fellowship")]
        [Example("getfellowshipstatus[]", "returns 1 if you are in a fellowship, 0 otherwise")]
        public object Getfellowshipstatus() {
            return InFellowship;
        }
        #endregion //getfellowshipstatus[]
        #region getfellowshipname[]
        [ExpressionMethod("getfellowshipname")]
        [ExpressionReturn(typeof(string), "Returns the name of a fellowship, or an empty string if none")]
        [Summary("Gets the name of your current fellowship")]
        [Example("getfellowshipname[]", "returns the name of your current fellowship")]
        public object Getfellowshipname() {
            return FellowshipName;
        }
        #endregion //getfellowshipname[]
        #endregion //Expressions

        public static bool InFellowship = false;
        public static string FellowshipName = "";

        public FellowshipManager(UtilityBeltPlugin ub, string name) : base(ub, name) {
            UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
        }

        private void EchoFilter_ServerDispatch(object sender, Decal.Adapter.NetworkMessageEventArgs e) {
            try {
                switch (e.Message.Type) {
                    case 0xF7B0:
                        switch (e.Message.Value<int>("event")) {
                            case 0x02BE: // Fellowship_FullUpdate
                                var name = e.Message.Value<string>("name");
                                FellowshipName = name;
                                InFellowship = true;
                                break;
                            case 0x02BF: // Fellowship_Disband
                                InFellowship = false;
                                FellowshipName = "";
                                break;
                            case 0x00A3: // Fellowship_Quit
                                var id = e.Message.Value<int>("fellow");
                                if (id == UB.Core.CharacterFilter.Id) {
                                    InFellowship = false;
                                    FellowshipName = "";
                                }
                                break;
                        }
                        break;
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
