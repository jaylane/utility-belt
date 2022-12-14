using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.ScriptInterface {
    public class ScriptInterface {
        public static SpellTable SpellTable = null;

        public void Init() {
        }

        public SpellBase GetSpellData(int id) {
            if (SpellTable == null) {
                SpellTable = UtilityBeltPlugin.Instance.PortalDat.ReadFromDat<SpellTable>(0x0E00000E);
            }
            try {
                if (SpellTable.Spells.TryGetValue((uint)id, out SpellBase spell)) {
                    return spell;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            return null;
        }
    }
}
