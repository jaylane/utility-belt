using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Lib.Settings {
    public class ClientUIProfile : ISetting {
        public class UIElementVector : ISetting {
            public UBHelper.UIElement UIElement;

            [Summary("UI element X position on screen")]
            public readonly Setting<int> X = new Setting<int>();

            [Summary("UI element Y position on screen")]
            public readonly Setting<int> Y = new Setting<int>();

            [Summary("UI element width")]
            public readonly Setting<int> Width = new Setting<int>();

            [Summary("UI element height")]
            public readonly Setting<int> Height = new Setting<int>();

            public UIElementVector(UBHelper.UIElement uiElement, int x, int y, int width, int height) : base() {
                UIElement = uiElement;
                X.Value = x;
                Y.Value = y;
                Width.Value = width;
                Height.Value = height;
            }
        }

        [Summary("SmartBox - 3d area")]
        public readonly UIElementVector SBOX = new UIElementVector(UBHelper.UIElement.SBOX, -1, -1, -1, -1);

        [Summary("Chat Window 1")]
        public readonly UIElementVector FCH1 = new UIElementVector(UBHelper.UIElement.FCH1, -1, -1, -1, -1);

        [Summary("Chat Window 2")]
        public readonly UIElementVector FCH2 = new UIElementVector(UBHelper.UIElement.FCH2, -1, -1, -1, -1);

        [Summary("Chat Window 3")]
        public readonly UIElementVector FCH3 = new UIElementVector(UBHelper.UIElement.FCH3, -1, -1, -1, -1);

        [Summary("Chat Window 4")]
        public readonly UIElementVector FCH4 = new UIElementVector(UBHelper.UIElement.FCH4, -1, -1, -1, -1);

        [Summary("Examination window")]
        public readonly UIElementVector EXAM = new UIElementVector(UBHelper.UIElement.EXAM, -1, -1, -1, -1);

        [Summary("Vitals")]
        public readonly UIElementVector VITS = new UIElementVector(UBHelper.UIElement.VITS, -1, -1, -1, -1);

        [Summary("Vendor/trade/loot window")]
        public readonly UIElementVector ENVP = new UIElementVector(UBHelper.UIElement.ENVP, -1, -1, -1, -1);

        [Summary("Inventory / options / etc")]
        public readonly UIElementVector PANS = new UIElementVector(UBHelper.UIElement.PANS, -1, -1, -1, -1);

        [Summary("Main chat window")]
        public readonly UIElementVector CHAT = new UIElementVector(UBHelper.UIElement.CHAT, -1, -1, -1, -1);

        [Summary("Toolbar (shortcuts, backpack icon)")]
        public readonly UIElementVector TBAR = new UIElementVector(UBHelper.UIElement.TBAR, -1, -1, -1, -1);

        [Summary("Link status / X / etc")]
        public readonly UIElementVector INDI = new UIElementVector(UBHelper.UIElement.INDI, -1, -1, -1, -1);

        [Summary("Jump bar")]
        public readonly UIElementVector PBAR = new UIElementVector(UBHelper.UIElement.PBAR, -1, -1, -1, -1);

        [Summary("Combat UI (attack / spellbar)")]
        public readonly UIElementVector COMB = new UIElementVector(UBHelper.UIElement.COMB, -1, -1, -1, -1);

        [Summary("Radar")]
        public readonly UIElementVector RADA = new UIElementVector(UBHelper.UIElement.RADA, -1, -1, -1, -1);

        [Summary("Side by side vitals")]
        public readonly UIElementVector SVIT = new UIElementVector(UBHelper.UIElement.SVIT, -1, -1, -1, -1);

        public ClientUIProfile() : base() {
            SettingType = SettingType.ClientUI;
        }
    }
}
