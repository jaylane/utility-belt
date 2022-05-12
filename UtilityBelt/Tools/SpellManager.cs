using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Interop.Input;
using Microsoft.DirectX;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Dungeon;
using UtilityBelt.Lib.Maps.Markers;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using static UtilityBelt.Tools.VTankControl;
using UBLoader.Lib.Settings;
using UtilityBelt.Lib.Expressions;

namespace UtilityBelt.Tools {
    [Name("Spells")]
    [Summary("Spell Stuff")]
    public class SpellManager : ToolBase {
        #region Expressions
        #region spellnamefromid[number spellid]
        [ExpressionMethod("spellname")]
        [ExpressionParameter(0, typeof(double), "spellid", "Spell ID")]
        [ExpressionReturn(typeof(string), "Returns a string name of the passed spell id.")]
        [Summary("Gets the name of a spell by id")]
        [Example("spellname[1]", "Returns `Strength Other I` for spell id 1")]
        public object spellname(double id) {
            return Lib.Spells.GetName((int)id);
        }
        #endregion //spellnamefromid[number spellid]
        #region componentname[number componentid]
        [ExpressionMethod("componentname")]
        [ExpressionParameter(0, typeof(double), "componentid", "Spell ID")]
        [ExpressionReturn(typeof(string), "Returns a string name of the passed spell id.")]
        [Summary("Gets the name of a spell component by id")]
        [Example("componentname[1]", "Returns total count of prismatic tapers in your inventory")]
        public object componentnamefromid(double id) {
            return Spells.GetComponentName((int)id);
        }
        #endregion //componentnamefromid[number componentid]
        #region componentdata[number componentid]
        [ExpressionMethod("componentdata")]
        [ExpressionParameter(0, typeof(double), "componentid", "component ID")]
        [ExpressionReturn(typeof(ExpressionDictionary), "Returns a dictionary containing component data")]
        [Summary("Returns a dictionary containing component data for the passed component id")]
        [Example("componentdata[1]", "returns a dictionary of information about component id 1")]
        public object componentdata(double id) {
            var component = Spells.GetComponent((int)id);
            var componentData = new ExpressionDictionary();
            componentData.Items.Add("BurnRate", (double)component.BurnRate);
            componentData.Items.Add("GestureId", (double)component.GestureId);
            componentData.Items.Add("GestureSpeed", (double)component.GestureSpeed);
            componentData.Items.Add("IconId", (double)component.IconId);
            componentData.Items.Add("Id", (double)component.Id);
            componentData.Items.Add("Name", component.Name);
            componentData.Items.Add("BurnRate", (double)component.SortKey);
            componentData.Items.Add("Type", component.Type.Name);
            componentData.Items.Add("Word", component.Word);

            return componentData;
        }
        #endregion //componentdata[number componentid]
        #region getknownspells[]
        [ExpressionMethod("getknownspells")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of spell ids known by this character")]
        [Summary("Returns a list of spell ids known by this character")]
        [Example("getknownspells[]", "Returns a list of spell ids known by this character")]
        public object getknownspells() {
            var spells = new ExpressionList();
            foreach (var x in UtilityBeltPlugin.Instance.Core.CharacterFilter.SpellBook) {
                spells.Items.Add(x);
            }

            return spells;
        }
        #endregion //getknownspells[]
        #region spelldata[number spellid]
        [ExpressionMethod("spelldata")]
        [ExpressionParameter(0, typeof(double), "spellid", "Spell ID")]
        [ExpressionReturn(typeof(ExpressionDictionary), "Returns a dictionary of spell data")]
        [Summary("Gets a dictionary of information about the passed spell id")]
        [Example("spelldata[1]", "returns spell data for spellid 1")]
        public object spelldata(double id) {
            var spell = Lib.Spells.GetSpell((int)id);
            var spellData = new ExpressionDictionary();
            if (spell == null)
                return spellData;

            var componentIds = new ExpressionList();
            for (var i=0; i < spell.ComponentIDs.Length; i++) {
                componentIds.Items.Add((double)spell.ComponentIDs[i]);
            }
            spellData.Items.Add("CasterEffect", (double)spell.CasterEffect);
            spellData.Items.Add("ComponentIds", componentIds);
            spellData.Items.Add("Description", spell.Description);
            spellData.Items.Add("Difficulty", (double)spell.Difficulty);
            spellData.Items.Add("Duration", (double)spell.Duration);
            spellData.Items.Add("Family", (double)spell.Family);
            spellData.Items.Add("Flags", (double)spell.Flags);
            spellData.Items.Add("Generation", (double)spell.Generation);
            spellData.Items.Add("IconId", (double)spell.IconId);
            spellData.Items.Add("Id", (double)spell.Id);
            spellData.Items.Add("IsDebuff", (double)(spell.IsDebuff ? 1 : 0));
            spellData.Items.Add("IsFastWindup", (double)(spell.IsFastWindup ? 1 : 0));
            spellData.Items.Add("IsFellowship", (double)(spell.IsFellowship ? 1 : 0));
            spellData.Items.Add("IsIrresistible", (double)(spell.IsIrresistible ? 1 : 0));
            spellData.Items.Add("IsOffensive", (double)(spell.IsOffensive ? 1 : 0));
            spellData.Items.Add("IsUntargetted", (double)(spell.IsUntargetted ? 1 : 0));
            spellData.Items.Add("Mana", (double)spell.Mana);
            spellData.Items.Add("Name", spell.Name);
            spellData.Items.Add("School", spell.School.Name);
            spellData.Items.Add("SortKey", (double)spell.SortKey);
            spellData.Items.Add("Speed", (double)spell.Speed);
            spellData.Items.Add("TargetEffect", (double)spell.TargetEffect);
            spellData.Items.Add("TargetMask", (double)spell.TargetMask);
            spellData.Items.Add("Type", (double)spell.Type);

            return spellData;
        }
        #endregion //spelldata[number spellid]
        #endregion Expressions

        public SpellManager(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
        }
    }
}
