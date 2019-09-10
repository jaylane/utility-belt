using Decal.Adapter.Wrappers;
using Decal.Adapter;
using System;

namespace UtilityBelt.Tools {
    class Counter : IDisposable {


        private bool disposed = false;

        public Counter() {
            CoreManager.Current.CommandLineText += new EventHandler<ChatParserInterceptEventArgs>(Current_CommandLineText);
        }


        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/ub count ")) {
                    e.Eat = true;
                    if (e.Text.Contains("item ")) {
                        string item = e.Text.Replace("/ub count item", "").Trim();
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
                        Util.Think("Item Count: " + item + " - " + totalStackCount.ToString());
                    }
                    else if (e.Text.Contains("player ")) {
                        int playerCount = 0;
                        string rangeString = e.Text.Replace("/ub count player ", "").Trim();
                        int rangeInt = 0;
                        if (Int32.TryParse(rangeString, out rangeInt)) {
                            // success parse
                        }
                        else {
                            Util.WriteToChat("bad player count range: " + rangeString);
                        }

                        foreach (WorldObject wo in CoreManager.Current.WorldFilter.GetLandscape()) {
                            if (wo.Type == 1 && (CoreManager.Current.WorldFilter.Distance(CoreManager.Current.CharacterFilter.Id, wo.Id) * 240) < rangeInt) {
                                Util.WriteToChat("object : " + wo.Name + " id: " + wo.Id + " type: " + wo.Type);
                                playerCount++;
                            }
                        }
                        Util.Think("Player Count: " + " " + playerCount.ToString());
                    }
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
