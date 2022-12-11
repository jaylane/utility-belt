using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using static UtilityBelt.Tools.VTankControl;
using UBService.Lib.Settings;
using UtilityBelt.Lib.Expressions;
using UtilityBelt.Lib.Dungeon;

namespace UtilityBelt.Tools {
    [Name("HealthTracker")]
    public class HealthTracker : ToolBase {

        public class TrackedVitalObject 
        {
            public double HealthPct;
            public int? Health, Stamina, Mana;
        }

        Dictionary<int, TrackedVitalObject> TrackedObjects = new Dictionary<int, TrackedVitalObject>();

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
            if (TrackedObjects.TryGetValue(wobject.Wo.Id, out var obj))
                return obj.HealthPct;

            return -1;
        }


        [ExpressionMethod("wobjectgethealthvalue")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get health of")]
        [ExpressionReturn(typeof(double), "Returns a value representing the objects current health value. If the object has not been identified this will return -1")]
        [Summary("Gets the specified mob/player wobject current health value.  Note: You must have the wobject selected in order to receive health updates")]
        [Example("wobjectgethealthvalue[wobjectgetselection[]]", "Returns the health of the currently selected mob/player")]
        public object wobjectgethealthvalue(ExpressionWorldObject wobject) {
            if (TrackedObjects.TryGetValue(wobject.Wo.Id, out var obj)) {
                if (!obj.Health.HasValue)
                    return -1;
                return obj.Health.Value;
            }
            return -1;
        }

        #endregion //wobjectgethealth[wobject obj]

        [ExpressionMethod("wobjectgetstaminavalue")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get stamina of")]
        [ExpressionReturn(typeof(double), "Returns a value representing the objects current stamina value. If the object has not been identified this will return -1")]
        [Summary("Gets the specified mob/player wobject current stamina value.  Note: You must have the wobject selected in order to receive stamina updates")]
        [Example("wobjectgetstaminavalue[wobjectgetselection[]]", "Returns the stamina of the currently selected mob/player")]
        public object wobjectgetstaminavalue(ExpressionWorldObject wobject) {
            if (TrackedObjects.TryGetValue(wobject.Wo.Id, out var obj)) {
                if (!obj.Stamina.HasValue)
                    return -1;
                return obj.Stamina.Value;
            }
            return -1;
        }


        [ExpressionMethod("wobjectgetmanavalue")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get mana of")]
        [ExpressionReturn(typeof(double), "Returns a value representing the objects current mana value. If the object has not been identified this will return -1")]
        [Summary("Gets the specified mob/player wobject current mana value.  Note: You must have the wobject selected in order to receive mana updates")]
        [Example("wobjectgetmanavalue[wobjectgetselection[]]", "Returns the mana of the currently selected mob/player")]
        public object wobjectgetmanavalue(ExpressionWorldObject wobject) {
            if (TrackedObjects.TryGetValue(wobject.Wo.Id, out var obj)) {
                if (!obj.Mana.HasValue)
                    return -1;
                return obj.Mana.Value;
            }
            return -1;
        }

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
            try {
                if (TrackedObjects.ContainsKey(id))
                    TrackedObjects[id].HealthPct = healthPercentage;
                else
                    TrackedObjects.Add(id, new TrackedVitalObject() { HealthPct = healthPercentage });
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        public void UpdateObjectVitals(int id, int health, int stamina, int mana, double healthPercentage) {
            try {
                if (TrackedObjects.ContainsKey(id)) {
                    TrackedObjects[id].HealthPct = healthPercentage;
                    TrackedObjects[id].Health = health;
                    TrackedObjects[id].Stamina = stamina;
                    TrackedObjects[id].Mana = mana;
                }
                else
                    TrackedObjects.Add(id, new TrackedVitalObject() { HealthPct = healthPercentage, Health = health, Stamina = stamina, Mana = mana });
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
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
                                var stamina = e.Message.Value<int>("stamina");
                                var mana = e.Message.Value<int>("mana");
                                var healthMax = e.Message.Value<int>("healthMax");
                                var healthPercentage = (double)health / (healthMax > 0 ? healthMax : 1);
                                UpdateObjectVitals(appraiseId, health, stamina, mana, healthPercentage);
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
