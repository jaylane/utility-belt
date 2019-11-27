using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Decal.Adapter;
using Decal.Adapter.Wrappers;

namespace UtilityBelt.Lib.Salvage {
    class TinkerType {

        int weaponTinkeringSkill;
        int magicItemTinkeringSkill;
        int armorTinkeringSkill;
        int itemTinkeringSkill;

        public static int GetTinkerType(int material) {
            switch (material) {
                case (int)Material.AGATE:
                case (int)Material.AZURITE:
                case (int)Material.BLACK_OPAL:
                case (int)Material.BLOODSTONE:
                case (int)Material.CARNELIAN:
                case (int)Material.CITRINE:
                case (int)Material.FIRE_OPAL:
                case (int)Material.GREEN_GARNET:
                case (int)Material.HEMATITE:
                case (int)Material.LAPIS_LAZULI:
                case (int)Material.LAVENDER_JADE:
                case (int)Material.MALACHITE:
                case (int)Material.OPAL:
                case (int)Material.RED_JADE:
                case (int)Material.ROSE_QUARTZ:
                case (int)Material.SMOKEY_QUARTZ:
                case (int)Material.SUNSTONE:
                    return 30;

                case (int)Material.ALABASTER:
                case (int)Material.ARMOREDILLO_HIDE:
                case (int)Material.BRONZE:
                case (int)Material.CERAMIC:
                case (int)Material.MARBLE:
                case (int)Material.PERIDOT:
                case (int)Material.REEDSHARK_HIDE:
                case (int)Material.STEEL:
                case (int)Material.WOOL:
                case (int)Material.YELLOW_TOPAZ:
                case (int)Material.ZIRCON:
                    return 29;

                case (int)Material.AQUAMARINE:
                case (int)Material.BLACK_GARNET:
                case (int)Material.BRASS:
                case (int)Material.EMERALD:
                case (int)Material.GRANITE:
                case (int)Material.IMPERIAL_TOPAZ:
                case (int)Material.IRON:
                case (int)Material.JET:
                case (int)Material.MAHOGANY:
                case (int)Material.OAK:
                case (int)Material.RED_GARNET:
                case (int)Material.VELVET:
                case (int)Material.WHITE_SAPPHIRE:
                    return 28;

                case (int)Material.AMBER:
                case (int)Material.COPPER:
                case (int)Material.DIAMOND:
                case (int)Material.EBONY:
                case (int)Material.GOLD:
                case (int)Material.GROMNIE_HIDE:
                case (int)Material.LINEN:
                case (int)Material.MOONSTONE:
                case (int)Material.PINE:
                case (int)Material.PORCELAIN:
                case (int)Material.PYREAL:
                case (int)Material.RUBY:
                case (int)Material.SAPPHIRE:
                case (int)Material.SATIN:
                case (int)Material.SILVER:
                case (int)Material.TEAK:
                    return 18;

                default:
                    return 0;
            }
        }

        public void GetTinkSkills() {
            int jackofalltradesbonus = 0;
            //if (CoreManager.Current.CharacterFilter.GetCharProperty(236) == 1) {
            //    jackofalltradesbonus = 5;
            //}
            weaponTinkeringSkill = CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.WeaponTinkering] + jackofalltradesbonus;
            magicItemTinkeringSkill = CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.MagicItemTinkering] + jackofalltradesbonus;
            armorTinkeringSkill = CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.ArmorTinkering] + jackofalltradesbonus;
            itemTinkeringSkill = CoreManager.Current.CharacterFilter.EffectiveSkill[CharFilterSkillType.ItemTinkering] + jackofalltradesbonus;
        }

        public static float GetMaterialMod(int material) {
            switch (material) {
                case (int)Material.GOLD:
                case (int)Material.OAK:
                    return 10.0f;

                case (int)Material.ALABASTER:
                case (int)Material.ARMOREDILLO_HIDE:
                case (int)Material.BRASS:
                case (int)Material.BRONZE:
                case (int)Material.CERAMIC:
                case (int)Material.GRANITE:
                case (int)Material.LINEN:
                case (int)Material.MARBLE:
                case (int)Material.MOONSTONE:
                case (int)Material.OPAL:
                case (int)Material.PINE:
                case (int)Material.REEDSHARK_HIDE:
                case (int)Material.VELVET:
                case (int)Material.WOOL:
                    return 11.0f;

                case (int)Material.EBONY:
                case (int)Material.GREEN_GARNET:
                case (int)Material.IRON:
                case (int)Material.MAHOGANY:
                case (int)Material.PORCELAIN:
                case (int)Material.SATIN:
                case (int)Material.STEEL:
                case (int)Material.TEAK:
                    return 12.0f;

                case (int)Material.BLOODSTONE:
                case (int)Material.CARNELIAN:
                case (int)Material.CITRINE:
                case (int)Material.HEMATITE:
                case (int)Material.LAVENDER_JADE:
                case (int)Material.MALACHITE:
                case (int)Material.RED_JADE:
                case (int)Material.ROSE_QUARTZ:
                    return 25.0f;

                default:
                    return 20.0f;
            }
        }

        public static int SalvageType(int material) {
            switch (material) {
                case (int)Material.BLACK_OPAL:
                case (int)Material.FIRE_OPAL:
                case (int)Material.SUNSTONE:
                case (int)Material.AQUAMARINE:
                case (int)Material.BLACK_GARNET:
                case (int)Material.EMERALD:
                case (int)Material.IMPERIAL_TOPAZ:
                case (int)Material.JET:
                case (int)Material.RED_GARNET:
                case (int)Material.WHITE_SAPPHIRE:
                    return 2;

                default:
                    return 1;
            }
        }

            public int GetRequiredTinkSkill(int tinkerType) {
            GetTinkSkills();
            switch (tinkerType) {
                case 30:
                    //Util.WriteToChat("magic item");
                    return magicItemTinkeringSkill;
                case 29:
                    //Util.WriteToChat("armor");
                    return armorTinkeringSkill;
                case 28:
                    //Util.WriteToChat("weapon");
                    return weaponTinkeringSkill;
                case 18:
                    //Util.WriteToChat("item");
                    return itemTinkeringSkill;
                default:
                    //Util.WriteToChat("invalid salvage");
                    return 0;
            }
        }
    }
}
