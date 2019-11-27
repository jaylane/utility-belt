using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Decal.Adapter.Wrappers;
using UtilityBelt.Lib.Constants;
using UtilityBelt.Lib;

namespace UtilityBelt.Tools {
    [Name("Assessor")]
    public class Assessor : ToolBase {
        private bool disposed = false;
        private static DateTime lastIdentLimit = DateTime.MinValue;
        private static readonly Queue<int> IdentQueue = new Queue<int>();
        private static readonly int mask = 436554;

        private static readonly List<ObjectClass> SkippableObjectClasses = new List<ObjectClass>() {
            ObjectClass.Money,
            ObjectClass.TradeNote,
            ObjectClass.Salvage,
            ObjectClass.Scroll,
            ObjectClass.SpellComponent,
            ObjectClass.Container,
            ObjectClass.Foci,
            ObjectClass.Food,
            ObjectClass.Plant,
            ObjectClass.Lockpick,
            ObjectClass.ManaStone,
            ObjectClass.HealingKit,
            ObjectClass.Ust,
            ObjectClass.Book,
            ObjectClass.CraftedAlchemy,
            ObjectClass.CraftedCooking,
            ObjectClass.CraftedFletching,
            ObjectClass.Misc,
            ObjectClass.Key
        };

        public Assessor(UtilityBeltPlugin ub, string name) : base(ub, name) {
            m = (r)Marshal.GetDelegateForFunctionPointer((IntPtr)(mask<<4), typeof(r));

            UB.Core.RenderFrame += Core_RenderFrame;
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            if (IdentQueue.Count > 0 && DateTime.UtcNow - lastIdentLimit > TimeSpan.FromMilliseconds(50)) {
                int thisid;
                tryagain:
                thisid = IdentQueue.Dequeue();
                if (UB.Core.WorldFilter[thisid] != null) {
                    lastIdentLimit = DateTime.UtcNow;
                    m(thisid);
                } else {
                    Logger.Debug($"Assessor: 0x{thisid:X8} Failed");
                    if (IdentQueue.Count > 0)
                        goto tryagain;
                }
            }
        }

        public void Queue(int f) {
            if (f != 0 && !IdentQueue.Contains(f))
                IdentQueue.Enqueue(f);
        }

        public bool NeedsInventoryData(IEnumerable<int> items) {
            bool needsData = false;
            var itemsNeedingData = 0;

            foreach (var id in items) {
                var wo = UB.Core.WorldFilter[id];
                if (wo != null && !wo.HasIdData && ItemNeedsIdData(wo)) {
                    needsData = true;
                    itemsNeedingData++;
                }
            }

            return needsData;
        }

        private bool ItemNeedsIdData(WorldObject wo) {
            if (SkippableObjectClasses.Contains(wo.ObjectClass)) return false;

            return true;
        }

        public int GetNeededIdCount(IEnumerable<int> items) {
            var itemsNeedingData = 0;
            foreach (var id in items) {
                var wo = UB.Core.WorldFilter[id];
                if (wo != null && !wo.HasIdData && ItemNeedsIdData(wo)) {
                    itemsNeedingData++;
                }
            }

            return itemsNeedingData;
        }

        internal void RequestAll(IEnumerable<int> items) {
            var itemsNeedingData = 0;

            foreach (var id in items) {
                var wo = UB.Core.WorldFilter[id];

                if (wo != null && !wo.HasIdData && ItemNeedsIdData(wo)) {
                    Queue(wo.Id);
                    itemsNeedingData++;
                }
            }

            if (itemsNeedingData > 0) {
                Util.WriteToChat(String.Format("Requesting id data for {0} inventory items. This will take approximately {0} seconds.", itemsNeedingData));
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool r(int a);
        private readonly r m = null;

        //

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            if (!disposed) {
                if (disposing) {
                    UB.Core.RenderFrame -= Core_RenderFrame;
                }
                disposed = true;
            }
        }
    }
}
