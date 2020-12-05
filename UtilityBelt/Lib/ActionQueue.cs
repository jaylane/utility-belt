using System;
using System.Collections.Generic;

namespace UtilityBelt.Lib {
    /// <summary>
    /// A queue for managing inventory actions
    /// </summary>
    public static class ActionQueue {
        private static bool isrunning = false;
        private static readonly List<Item> ItemsRunning = new List<Item>();
        internal static void Init() {
            if (!isrunning) {
                isrunning = true;
                Decal.Adapter.CoreManager.Current.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
                Decal.Adapter.CoreManager.Current.RenderFrame += Core_RenderFrame;
            }
        }
        internal static void Dispose() {
            if (isrunning) {
                Decal.Adapter.CoreManager.Current.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                Decal.Adapter.CoreManager.Current.RenderFrame += Core_RenderFrame;
                isrunning = false;
            }
        }
        private static void Add(Item caller) {
            ItemsRunning.Add(caller);
            if (!isrunning) {
                Init();
            }
        }
        private static void Remove(Item caller) {
            ItemsRunning.Remove(caller);
            if (ItemsRunning.Count == 0)
                Dispose();
        }
        private static void Core_RenderFrame(object sender, EventArgs e) {
            foreach (Item i in ItemsRunning) {
                //if (i.ids != null && i.ids.Count == 0) {
                //Core.W("ActionQueue.Core_RenderFrame - id list found empty?");
                //i.Dispose();
                //continue;
                //}
                if (i.heartBeat < UBHelper.Core.curTime) {
                    Logger.WriteToChat($"ActionQueue.Core_RenderFrame Timeout! i.ids.Count:{i.ids.Count}");
                    i.Dispose();
                }
            }
        }

        //TODO: Replace with Event hooks.
        private static void EchoFilter_ServerDispatch(object sender, Decal.Adapter.NetworkMessageEventArgs e) {
            if (e.Message.Type == 0xF7B0) {
                if ((int)e.Message["event"] == 0x0022) {  // Event: Move Item
                    TryChange((int)e.Message["item"]);
                }
                else if ((int)e.Message["event"] == 0x00A0) { //INVENTORY_SERVER_SAYS_FAILED
                    TryChange((int)e.Message["item"]);
                }
            }
            else if (e.Message.Type == 0x0024) { // Remove Item
                TryChange((int)e.Message["object"]);
            }
            else if (e.Message.Type == 0x02DA && (int)e.Message["key"] == 2) { // Add Item
                TryChange((int)e.Message["object"]);
            }
            else if (e.Message.Type == 0xF745) { // CreateObject
                TryChange((int)e.Message["object"]);
            }
            else if (e.Message.Type == 0x0197) { // Adjust Stack Size
                TryChange((int)e.Message["item"]);
            }
        }
        private static void TryChange(int object_id) {
            foreach (Item i in ItemsRunning) if (i.ids.Contains(object_id)) i.ChangeId(object_id);
        }

        public delegate void JobCallback(bool DidSomething);
        public class Item {
            public List<int> ids;
            public double heartBeat;
            private JobCallback jobCallback;
            private bool didSomething = false;

            public Item(JobCallback jobCallback) {
                this.jobCallback = jobCallback;
                ids = new List<int>();
                heartBeat = UBHelper.Core.curTime + 30d;
                ActionQueue.Add(this);
            }
            public void Queue(int object_id) {
                if (object_id != 0) { didSomething = true; ids.Add(object_id); }
            }
            public void Dispose() {
                if (ids.Count != 0) {
                    Logger.WriteToChat($"Action Queue disposed with {ids.Count} items remaining!");
                    foreach (int i in ids) Logger.WriteToChat($"   0x{i:X8} {(new UBHelper.Weenie(i).Name)}");
                }
                jobCallback?.Invoke(didSomething);
                ActionQueue.Remove(this);
                jobCallback = null;
                ids = null;
            }
            internal void ChangeId(int object_id) {
                heartBeat = UBHelper.Core.curTime + 30d;
                ids.Remove(object_id);
                if (ids.Count == 0) Dispose();
            }
        }
    }
}
