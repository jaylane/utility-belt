using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.MagTools.Shared;

namespace UtilityBelt.Tools {
    class Jumper : IDisposable {
        private bool disposed = false;

        public Jumper() {
        Globals.Core.CommandLineText += Current_CommandLineText;
            }

                void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/mt face ")) {
                    if (e.Text.Length > 9) {
                        int heading;
                        if (!int.TryParse(e.Text.Substring(9, e.Text.Length - 9), out heading))
                            return;
                        
                        CoreManager.Current.Actions.FaceHeading(heading, true);
                    }

                    return;
                }

                if (e.Text.StartsWith("/ub jump") || e.Text.StartsWith("/ub sjump")) {
                    int msToHoldDown = 0;
                    bool addShift = e.Text.Contains("sjump");
                    bool addW = e.Text.Contains("jumpw");
                    bool addZ = e.Text.Contains("jumpz");
                    bool addX = e.Text.Contains("jumpx");
                    bool addC = e.Text.Contains("jumpc");
                    e.Eat = true;

                    string[] split = e.Text.Split(' ');
                    if (split.Length == 3)
                        int.TryParse(split[2], out msToHoldDown);

                    PostMessageTools.SendSpace(msToHoldDown, addShift, addW, addZ, addX, addC);

                    return;
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.CommandLineText -= Current_CommandLineText;
                }
                disposed = true;
            }
        }
    }
}
