using System;
using System.ComponentModel;
using System.Linq;

namespace UtilityBelt.Lib.ChatLog
{
    public enum ChatMessageType : uint
    {
        /// <summary>
        /// allegiance MOTD
        /// 
        /// Failed to leave chat room.
        /// 
        /// You give Mr Muscles 10 Gems of Knowledge.
        /// 
        /// You roast the cacao beans.
        /// 
        /// Your chat privileges have been restored.
        /// 
        /// Buff Dude is online
        /// 
        /// The Mana Stone drains 3,502 points of mana from the Staff.
        /// The Staff is destroyed.
        /// 
        /// The Mana Stone gives 26,693 points of mana to the following items: Sparring Pants, Chainmail Tassets, Sollerets, Platemail Gauntlets, Sparring Shirt, Celdon Breastplate, Bracelet, Chainmail Bracers, Veil of Darkness
        /// Your items are fully charged.
        /// 
        /// The Mana Stone gives 3,123 points of mana to the following items: Frigid Bracelet, Silifi of Crimson Night, Tunic, Sleeves of Inexhaustibility, Breeches
        /// You need 3,640 more mana to fully charge your items.
        /// 
        /// LogTextTypeEnumMapper: Default
        /// </summary>
        [ACIcon(0x6006119)]
        [ChatColor(0x7FFF7E)]
        Broadcast = 0x00,

        /// <summary>
        /// LogTextTypeEnumMapper: Speech
        /// </summary>
        [ACIcon(0x6001028)]
        [ChatColor(0xFFFFFF)]
        Speech = 0x02,

        /// <summary>
        /// 
        /// Via F7E0:
        /// Buff Dude has added you to their home's guest list.  You now have access to their home.,
        /// Buff Dude has granted you access to their home's storage.,
        /// Buff DudeRipley has removed all house guests, including yourself.,
        /// 
        /// LogTextTypeEnumMapper: Tell
        /// </summary>
        [ACIcon(0x6001036)]
        [ChatColor(0xFFFF3E)]
        Tell = 0x03,

        /// <summary>
        /// You tell ...
        /// 
        /// LogTextTypeEnumMapper: Speech_Direct_Send
        /// </summary>
        [Description("Tell"), Parent(Tell), ShowInSettings(false)]
        [ChatColor(0xCCCC60)]
        OutgoingTell = 0x04,

        /// <summary>
        /// Warning!  You have not paid your maintenance costs for the last 30 day maintenance period.  Please pay these costs by this deadline or you will lose your house, and all your items within it.
        /// Some Guy has discovered the Wayfarer's Pearl!
        /// 
        /// LogTextTypeEnumMapper: System
        /// </summary>
        [ACIcon(0x6002D13)]
        [ChatColor(0xFF7EFF)]
        System = 0x05,

        /// <summary>
        /// You receive 18 points of periodic nether damage.
        /// You suffer 4 points of minor impact damage.
        /// Dirty Fighting! Big Guy delivers a Unbalancing Blow to Armored Tusker!,
        /// 
        /// LogTextTypeEnumMapper: Combat
        /// </summary>
        [ACIcon(0x60010BC)]
        [ChatColor(0xFF3E3E)]
        Combat = 0x06,

        /// <summary>
        /// Prismatic Amuli Leggings cast Epic Quickness on you
        /// The spell Brogard's Defiance on Baggy Pants has expired.
        /// You resist the spell cast by Someone
        /// Some Guy tried and failed to cast a spell at you!
        /// You cast Incantation of Revitalize Self and restore 172 points of your stamina.
        /// 
        /// LogTextTypeEnumMapper: Magic
        /// </summary>
        [ACIcon(0x60032CD)]
        [ChatColor(0x3EBEFF)]
        Magic = 0x07,

        /// <summary>
        /// Light Pink Text - Both of the following two were associated with the following channels: admin, audit, av1, av2, av3, sentinel
        /// output: You say on the [channel name] channel, "message here"
        /// 
        /// LogTextTypeEnumMapper: Channel
        /// </summary>
        [ACIcon(0x6001FA3)]
        Channel = 0x08,

        /// <summary>
        /// LogTextTypeEnumMapper: Channel_Send
        /// </summary>
        [Description("Channel"), Parent(Channel), ShowInSettings(false)]
        ChannelSend = 0x09,

        /// <summary>
        /// Bright Yellow Text
        /// LogTextTypeEnumMapper: Social
        /// </summary>
        [ACIcon(0x6006D9F)]
        [ChatColor(0xFFFF3E)]
        Social = 0x0A,

        /// <summary>
        /// Light Yellow Text
        /// LogTextTypeEnumMapper: Social_Send
        /// </summary>
        [Description("Social"), Parent(Social), ShowInSettings(false)]
        [ChatColor(0xCCCC60)]
        SocialSend = 0x0B,

        /// <summary>
        /// Via 0x02BB:
        /// All's quiet on this front!Enum_000C = 0x000C 
        /// Lemme alone.  And keep my door closed!
        /// Haha!
        /// Au virinaa! Au... baeaa?
        /// Suvani hasode, Metresa, tamaa Dar Hallae.
        /// Arena One is now available for new warriors!
        /// 
        /// Via 0x02BC:
        /// Did you know there are other people who collect other items, like gromnie teeth?  You might find them in some towns.  I'll take them, but I won't pay you for them!
        /// I collect only phyntos wasp wings.  I'll reward you well for any you happen to have.
        /// Avoid the great crater, I say; the caves there are none too pleasant, even for me!  Oh, the fumes!
        /// If we cannot think of anything quieter and tidier than that... We are not any better than these humans...
        /// There are other beings that will exchange items and currency with you for the remains of creatures such as husks and skeletal pieces.
        /// Very rarely, you can get a perfect red phyntos wasp wing.  If you can give one to me, I'll pay you for it.  I will also pay you for the tails of rats.
        /// I can take the hides of certain creatures and turn them into items of value.
        /// Some say that two brews based on two yeasts of varying qualities are indistinguishable from one another. To those people I say, "You have the taste of a Drudge and the brains of a Banderling!" Heathens, I say. Heathens!
        /// Phyntos wasps are not my favorite creature, but I do admire the wings.
        /// Have you a drudge charm, swamp stone, rat tail, or such? I'll pay you good money or items if you give them to me. They're hard to come by.
        /// Have you the skins of armoredillos, gromnies, or reedsharks?  I can use them in my craft.
        /// Damnable Mukkir...  They get everywhere...
        /// 
        /// LogTextTypeEnumMapper: Emote
        /// </summary>
        [ACIcon(0x6001035)]
        [ChatColor(0xD2D2C7)]
        Emote = 0x0C,

        /// <summary>
        /// You are now level 5!
        /// Your base Heavy Weapons skill is now 70!
        /// 
        /// You are now level 68!
        /// You have 100,647,873 experience points and 3 skill credits available to raise skills and attributes.
        /// You will earn another skill credit at level 70.,
        /// 
        /// LogTextTypeEnumMapper: Advancement
        /// </summary>
        [ACIcon(0x60073EE)]
        [ChatColor(0x3EDCDC)]
        Advancement = 0x0D,

        /// <summary>
        /// Light Cyan (skyblue?) Text - Would seem to be associated with the following channel: Abuse
        /// output: You say on the Abuse channel, "message here"
        /// 
        /// LogTextTypeEnumMapper: Abuse
        /// </summary>
        [ACIcon(0x6001372)]
        Abuse = 0x0E,

        /// <summary>
        /// Red Text - Possibly OutgoingHelpSay, not even sure if that showed up on the client when you sent out an urgent help command
        /// 
        /// LogTextTypeEnumMapper: Help
        /// </summary>
        [ACIcon(0x60018D5)]
        Help = 0x0F,

        /// <summary>
        /// Mr Sneaky tried and failed to assess you!
        /// 
        /// LogTextTypeEnumMapper: Appraisal
        /// </summary>
        [ACIcon(0x6001388)]
        [ChatColor(0x7FFF7E)]
        Appraisal = 0x10,

        /// <summary>
        /// Via 02BB:
        /// Malar Quaril
        /// Puish Zharil
        /// 
        /// Via F7E0:
        /// Aetheria surges on Pyreal Target Drudge with the power of Surge of Affliction!
        /// The cloak of Some Guy weaves the magic of Cloaked in Skill!
        /// 
        /// LogTextTypeEnumMapper: Spellcasting
        /// </summary>
        [ACIcon(0x6001374)]
        [ChatColor(0x3EBEFF)]
        Spellcasting = 0x11,

        /// <summary>
        /// Fellow warriors, aid me!
        /// 
        /// LogTextTypeEnumMapper: Allegiance
        /// </summary>
        [ACIcon(0x600218B)]
        [ChatColor(0xED921E)]
        Allegiance = 0x12,

        /// <summary>
        /// Bright Yellow Text
        /// 
        /// LogTextTypeEnumMapper: Fellowship
        /// </summary>
        [ACIcon(0x6001436)]
        [ChatColor(0xFFFF3E)]
        Fellowship = 0x13,

        /// <summary>
        /// Green Text
        /// 
        /// LogTextTypeEnumMapper: World_Broadcast
        /// </summary>
        [ACIcon(0x6001F88), Description("World Broadcast")]
        [ChatColor(0x7FFF7E)]
        WorldBroadcast = 0x14,

        /// <summary>
        /// Red Text
        /// 
        /// LogTextTypeEnumMapper: Combat_Enemy
        /// </summary>
        [Description("Combat"), Parent(Combat), ShowInSettings(false)]
        [ChatColor(0xFF3E3E)]
        CombatEnemy = 0x15,

        /// <summary>
        /// Pink Text
        /// 
        /// LogTextTypeEnumMapper: Combat_Self
        /// </summary>
        [Description("Combat"), Parent(Combat), ShowInSettings(false)]
        [ChatColor(0xF47571)]
        CombatSelf = 0x16,

        /// <summary>
        /// Player is recalling home.
        /// 
        /// LogTextTypeEnumMapper: Recall
        /// </summary>
        [ACIcon(0x6001382)]
        [ChatColor(0x7FFF7E)]
        Recall = 0x17,

        /// <summary>
        /// Super Tink fails to apply the Fire Opal Salvage (workmanship 10.00) to the White Sapphire Fire Baton. The target is destroyed.
        /// Super Tink successfully applies the Steel Salvage (workmanship 10.00) to the Silver Signet Crown.,
        /// 
        /// LogTextTypeEnumMapper: Craft
        /// </summary>
        [ACIcon(0x6001C72)]
        [ChatColor(0x7FFF7E)]
        Craft = 0x18,

        /// <summary>
        /// Green Text
        /// LogTextTypeEnumMapper: Salvaging
        /// </summary>
        [ACIcon(0x60026BA)]
        [ChatColor(0x7FFF7E)]
        Salvaging = 0x19,

        [ACIcon(0x60033BF)]
        [ChatColor(0xB4DCEF)]
        General = 0x1B,
        [ACIcon(0x6002761)]
        [ChatColor(0xB4DCEF)]
        Trade = 0x1C,
        [ACIcon(0x6002FB9)]
        [ChatColor(0xB4DCEF)]
        LFG = 0x1D,
        [ACIcon(0x600624A)]
        [ChatColor(0xB4DCEF)]
        Roleplay = 0x1E,

        
        /// <summary>
        /// Bright Yellow Text
        /// 
        /// LogTextTypeEnumMapper: Admin_Tell
        /// </summary>
        [ACIcon(0x6002632), Description("Admin Tell")]
        [ChatColor(0xFFFF3E)]
        AdminTell = 0x1F,

        [ACIcon(0x60010E7)]
        [ChatColor(0xB4DCEF)]
        Olthoi = 0x20,
        [ACIcon(0x60070A0)]
        [ChatColor(0xB4DCEF)]
        Society = 0x21
    }

    public class ChatColorAttribute : Attribute
    {
        public System.Drawing.Color Color { get; }

        public ChatColorAttribute(System.Drawing.Color color)
        {
            Color = color;
        }

        public ChatColorAttribute(int hexCode)
            : this(System.Drawing.Color.FromArgb(hexCode / 0x10000, (hexCode % 0x10000) / 0x100, hexCode % 0x100)) { }
    }

    public class ShowInSettingsAttribute : Attribute
    {
        public bool Show { get; }

        public ShowInSettingsAttribute(bool show)
        {
            Show = show;
        }
    }

    public class ACIconAttribute : Attribute
    {
        public int Icon { get; }

        public ACIconAttribute(int icon)
        {
            Icon = icon;
        }
    }

    public class ParentAttribute : Attribute
    {
        public ParentAttribute(ChatMessageType parentType)
        {
            ParentType = parentType;
        }

        public ChatMessageType ParentType { get; }
    }

    public static class ChatMessageTypeExtensions
    {
        public static int GetIcon(this ChatMessageType type)
        {
            var memberInfos = typeof(ChatMessageType).GetMember(type.ToString());
            var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == typeof(ChatMessageType));
            var valueAttributes =
                  enumValueMemberInfo.GetCustomAttributes(typeof(ACIconAttribute), false);
            return ((ACIconAttribute)valueAttributes[0]).Icon;
        }

        public static string GetDescription(this ChatMessageType type)
        {
            var memberInfos = typeof(ChatMessageType).GetMember(type.ToString());
            var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == typeof(ChatMessageType));
            var valueAttributes =
                  enumValueMemberInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (valueAttributes.Length > 0)
                return ((DescriptionAttribute)valueAttributes[0]).Description;
            return type.ToString();
        }

        public static bool ShowInSettings(this ChatMessageType type)
        {
            var memberInfos = typeof(ChatMessageType).GetMember(type.ToString());
            var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == typeof(ChatMessageType));
            var valueAttributes =
                  enumValueMemberInfo.GetCustomAttributes(typeof(ShowInSettingsAttribute), false);
            if (valueAttributes.Length > 0)
                return ((ShowInSettingsAttribute)valueAttributes[0]).Show;
            return true;
        }

        public static ChatMessageType? GetParent(this ChatMessageType type)
        {
            var memberInfos = typeof(ChatMessageType).GetMember(type.ToString());
            var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == typeof(ChatMessageType));
            var valueAttributes =
                  enumValueMemberInfo.GetCustomAttributes(typeof(ParentAttribute), false);
            if (valueAttributes.Length > 0)
                return ((ParentAttribute)valueAttributes[0]).ParentType;
            return null;
        }

        public static System.Drawing.Color GetChatColor(this ChatMessageType type)
        {
            var memberInfos = typeof(ChatMessageType).GetMember(type.ToString());
            var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == typeof(ChatMessageType));
            var valueAttributes =
                  enumValueMemberInfo.GetCustomAttributes(typeof(ChatColorAttribute), false);
            if (valueAttributes.Length > 0)
                return ((ChatColorAttribute)valueAttributes[0]).Color;
            return System.Drawing.Color.White;
        }
    }
}
