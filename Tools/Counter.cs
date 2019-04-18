using Decal.Adapter.Wrappers;
using Decal.Adapter;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Linq;
using System.Net;

namespace UtilityBelt.Tools {
    class Counter : IDisposable {


        private bool disposed = false;

        public Counter() {
            CoreManager.Current.CommandLineText += new EventHandler<ChatParserInterceptEventArgs>(Current_CommandLineText);
        }


        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/ub count")) {
                    string item = e.Text.Replace("/ub count ", "").Trim();
                    int stackCount = 0;
                    int totalStackCount = 0;
                    foreach (WorldObject wo in CoreManager.Current.WorldFilter.GetInventory()) {
                        if (String.Compare(wo.Name, item, StringComparison.OrdinalIgnoreCase) == 0) {
                            stackCount = wo.Values(LongValueKey.StackCount, 1);
                            totalStackCount += stackCount;
                        }
                        else {
                            //Util.WriteToChat("-1");
                        }
                    }
                    Util.Think("Counter: " + item + " - " + totalStackCount.ToString());
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }


        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);

        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    CoreManager.Current.CommandLineText -= new EventHandler<ChatParserInterceptEventArgs>(Current_CommandLineText);
                }
                disposed = true;
            }
        }
    }
}
