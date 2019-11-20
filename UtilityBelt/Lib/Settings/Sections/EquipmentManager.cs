using System.ComponentModel;

namespace UtilityBelt.Lib.Settings.Sections
{
    public class EquipmentManager : SectionBase
    {
        [Summary("Think to yourself when done equipping items")]
        [DefaultValue(false)]
        public bool Think
        {
            get => (bool)GetSetting("Think");
            set => UpdateSetting("Think", value);
        }

        public EquipmentManager(SectionBase parent) : base(parent)
        {
            Name = "EquipmentManager";
        }
    }
}
