using ACE.DatLoader.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UBCommon.Enums;

namespace UtilityBelt.Lib.ScriptInterface {
    public class SpellData {
        public uint DisplayOrder { get; }
        public float RecoveryAmount { get; }
        public double RecoveryInterval { get; }
        public uint FizzleEffect { get; }
        public uint TargetEffect { get; }
        public uint CasterEffect { get; }
        public List<uint> Formula { get; }
        public double PortalLifetime { get; }
        public float DegradeLimit { get; }
        public float DegradeModifier { get; }
        public double Duration { get; }
        public uint MetaSpellId { get; }
        public SpellType MetaSpellType { get; }
        public float ComponentLoss { get; }
        public uint FormulaVersion { get; }
        public float SpellEconomyMod { get; }
        public uint Power { get; }
        public float BaseRangeMod { get; }
        public float BaseRangeConstant { get; }
        public uint BaseMana { get; }
        public uint Bitfield { get; }
        public SpellCategory Category { get; }
        public uint Icon { get; }
        public MagicSchool School { get; }
        public string Desc { get; }
        public string Name { get; }
        public uint NonComponentTargetType { get; }
        public uint ManaMod { get; }
    }
}
