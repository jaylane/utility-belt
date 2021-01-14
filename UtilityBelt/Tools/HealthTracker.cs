using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using static UtilityBelt.Tools.VTankControl;
using UBLoader.Lib.Settings;
using UtilityBelt.Lib.Expressions;

namespace UtilityBelt.Tools {
    [Name("HealthTracker")]
    public class HealthTracker : ToolBase {
        Dictionary<int, double> TrackedObjects = new Dictionary<int, double>();

        #region Config
        [Summary("Enable health tracking for players/mobs")]
        public readonly Setting<bool> Enabled = new Setting<bool>(true);
        #endregion //Config

        #region Expressions
        #region wobjectgethealth[wobject obj]
        [ExpressionMethod("wobjectgethealth")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get health of")]
        [ExpressionReturn(typeof(double), "Returns a value from 0-1 representing the objects current health percentage.  If the objects health is not currently being tracked this will return -1")]
        [Summary("Gets the specified mob/player wobject current health percentage.  Note: You must have the wobject selected in order to receive health updates")]
        [Example("wobjectgethealth[wobjectgetselection[]]", "Returns the health of the currently selected mob/player")]
        public object wobjectgethealth(ExpressionWorldObject wobject) {
            if (TrackedObjects.TryGetValue(wobject.Wo.Id, out double health))
                return health;

            return -1;
        }
        #endregion //wobjectgethealth[wobject obj]
        #endregion //Expressions

        public HealthTracker(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
            UB.Core.WorldFilter.ReleaseObject += WorldFilter_ReleaseObject;
        }

        private void WorldFilter_ReleaseObject(object sender, Decal.Adapter.Wrappers.ReleaseObjectEventArgs e) {
            try {
                if (TrackedObjects.ContainsKey(e.Released.Id))
                    TrackedObjects.Remove(e.Released.Id);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void UpdateObjectHealth(int id, double healthPercentage) {
            if (TrackedObjects.ContainsKey(id))
                TrackedObjects[id] = healthPercentage;
            else
                TrackedObjects.Add(id, healthPercentage);
        }

        private void EchoFilter_ServerDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                if (e.Message.Type == 0xF7B0) {
                    switch (e.Message.Value<int>("event")) {
                        case 0x01C0: // Combat_QueryHealthResponse
                            var combatQueryId = e.Message.Value<int>("object");
                            var combatQueryHP = e.Message.Value<double>("health");
                            UpdateObjectHealth(combatQueryId, combatQueryHP);
                            break;

                        case 0x00C9: // Item_SetAppraiseInfo
                            var flags = e.Message.Value<int>("flags");
                            if ((flags & 0x00000100) != 0) {
                                var appraiseId = e.Message.Value<int>("object");
                                var health = e.Message.Value<int>("health");
                                var healthMax = e.Message.Value<int>("healthMax");
                                var healthPercentage = (double)health / (healthMax > 0 ? healthMax : 1);
                                UpdateObjectHealth(appraiseId, healthPercentage);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                    UB.Core.WorldFilter.ReleaseObject -= WorldFilter_ReleaseObject;
                }
                disposedValue = true;
            }
        }
    }
}
