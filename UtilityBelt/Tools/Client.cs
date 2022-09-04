using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UBLoader.Lib.Settings;
using VirindiViewService.Controls;
using System.IO;
using UtilityBelt.Views;
using System.Drawing;
using System.Runtime.InteropServices;

namespace UtilityBelt.Tools {
    [Name("Client")]
    public class Client : ToolBase {
        internal HudCombo ClientUIProfilesCombo;
        private HudButton ClientUIProfileCopyTo;
        private HudButton ClientUIProfileReset;
        private HudButton ClientUIProfileImport;

        /// <summary>
        /// The default character client ui path
        /// </summary>
        public string CharacterClientUIFile { get => Path.Combine(Util.GetCharacterDirectory(), ClientUIProfileExtension); }

        /// <summary>
        /// The file path to the currently loaded settings profile
        /// </summary>
        public string ClientUIProfilePath {
            get {
                if (UIProfile == "[character]")
                    return CharacterClientUIFile;
                else
                    return Path.Combine(Util.GetProfilesDirectory(), $"{UIProfile}.{ClientUIProfileExtension}");
            }
        }

        public static readonly string ClientUIProfileExtension = "clientui.json";

        #region Config
        [Summary("Client UI profile. Choose [character] to use a private copy of settings for this character.")]
        public readonly CharacterState<string> UIProfile = new CharacterState<string>("[character]");

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
                SettingType = SettingType.ClientUI;
            }
        }

        public class ClientUISettings : ISetting {
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
        }

        public readonly ClientUISettings ClientUI = new ClientUISettings();
        #endregion

        #region Commands
        #region /ub getui
        [Summary("Returns the client window positions, encoded as a base64 string")]
        [Usage("/ub getui")]
        [CommandPattern("getui", @"^$")]
        public void DoGetUI(string _, Match _2) {
            uint[] positions = new uint[32];
            int pos = 0;
            foreach (int i in Enum.GetValues(typeof(UBHelper.UIElement))) {
                System.Drawing.Rectangle t = UBHelper.Core.GetElementPosition((UBHelper.UIElement)i);
                //Logger.WriteToChat($"Elem {(UBHelper.UIElement)i}: {pos}={(t.X << 16) + t.Y:X8},{pos + 1}={(t.Width << 16) + t.Height:X8}");
                positions[pos] = ((uint)t.X << 16) + (uint)t.Y;
                positions[pos + 1] = ((uint)t.Width << 16) + (uint)t.Height;
                pos += 2;
            }

            byte[] barr = new byte[128];
            Buffer.BlockCopy(positions, 0, barr, 0, 128);
            string output = System.Convert.ToBase64String(barr);
            Logger.WriteToChat($"UI:{output}");
        }
        #endregion
        #region /ub setui <value>
        [Summary("Sets the client window positions, based on a base64 string")]
        [Usage("/ub setui UI:<string from getui>")]
        //                  UI:igAAAJYBzgIAABEAigChAZEAAABoAPoA/wAAAGgA+gA2AqIAkAAWAoQAmQHKATYBHQCsAToAoACqARQAeAC6AgAAygKBAjYBHQIAAOMAzwJ8AsoChAA2AQAAtwEeAJYAkAFfABkAKgPIARQAWgC7AgMATAKMAHgAAAA0AhoAzAE=
        [Example("/ub setui UI:UI:AAAAAMkBzgKLAA0BbgDCAZEAAABoAPoA8wANAWgAwwFVAQ0BaADCAWwAmQGTAjYBAACwAToAoABSAQAAeADPAgAAygKhAjYBxgEAADoB0AKcAsoCZAA2AQAAAAAeAJYAkAFfABkAKgNxAQAAWgDOAhoAJgKMAHgAAACXABoAzAE=", "Sets the UI to a pretty standard 1024x768 layout")]
        [CommandPattern("setui", @"^UI:(?<ui>[A-Za-z0-9\+/=]{172,172})$", true)]
        public void DoSetUI(string _, Match args) {
            string input = args.Groups["ui"].Value;
            byte[] barr = System.Convert.FromBase64String(input);
            uint[] positions = new uint[32];
            Buffer.BlockCopy(barr, 0, positions, 0, 128);
            int pos = 0;
            foreach (int i in Enum.GetValues(typeof(UBHelper.UIElement))) {
                //Logger.WriteToChat($"Elem {(UBHelper.UIElement)i}: X:{positions[pos]>>16} Y:{positions[pos]&0xFFFF} Width:{positions[pos+1]>>16} Height:{positions[pos+1]&0xFFFF}");
                UBHelper.Core.MoveElement((UBHelper.UIElement)i, new System.Drawing.Rectangle((int)(positions[pos] >> 16), (int)(positions[pos] & 0xFFFF), (int)(positions[pos + 1] >> 16), (int)(positions[pos + 1] & 0xFFFF)));
                pos += 2;
            }
        }
        #endregion
        #region /ub resolution <width> <height>
        [Summary("Set client resolution. This will take effect immediately, but will not change the settings page, or persist through relogging.")]
        [Usage("/ub resolution <width> <height>")]
        [Example("/ub resolution 640 600", "Set client resolution to 640 x 600")]
        [CommandPattern("resolution", @"^(?<Width>\d+)[x ](?<Height>\d+)$")]
        public void DoResolution(string _, Match args) {
            Util.Rect windowStartRect = new Util.Rect();
            DateTime lastResolutionChange = DateTime.MinValue;
            ushort.TryParse(args.Groups["Width"].Value, out ushort width);
            ushort.TryParse(args.Groups["Height"].Value, out ushort height);
            if (height < 100 || height > 10000 || width < 100 || width > 10000) {
                LogError($"Requested resolution was not valid ({width}x{height})");
                return;
            }

            void core_WindowMessage(object sender, Decal.Adapter.WindowMessageEventArgs e) {
                try {
                    var elapsed = DateTime.UtcNow - lastResolutionChange;
                    var didMove = false;
                    if ((e.Msg == 0x0046 || e.Msg == 0x0047) && elapsed < TimeSpan.FromSeconds(1)) {
                        Util.Rect windowCurrentRect = new Util.Rect();
                        Util.GetWindowRect(UB.Core.Decal.Hwnd, ref windowCurrentRect);
                        if (windowCurrentRect.Left != windowStartRect.Left || windowCurrentRect.Top != windowStartRect.Top) {
                            Util.SetWindowPos(UB.Core.Decal.Hwnd, 0, windowStartRect.Left, windowStartRect.Top, 0, 0, 0x0005);
                            didMove = true;
                        }
                    }
                    if (didMove || elapsed > TimeSpan.FromSeconds(1)) {
                        UB.Core.WindowMessage -= core_WindowMessage;
                    }
                }
                catch (Exception ex) { Logger.LogException(ex); }
            }

            Util.GetWindowRect(UB.Core.Decal.Hwnd, ref windowStartRect);
            UB.Core.WindowMessage += core_WindowMessage;
            lastResolutionChange = DateTime.UtcNow;

            WriteToChat($"Setting Resolution {width}x{height}");
            UBHelper.Core.SetResolution(width, height);
        }
        #endregion
        #region /ub textures <landscape> <landscapeDetail> <environment> <environmentDetail> <sceneryDraw> <landscapeDraw>
        [Summary("Sets Client texture options. This will take effect immediately, but will not change the settings page, or persist through relogging.")]
        [Usage("/ub textures <landscape[0-4]> <landscapeDetail[0-1]> <environment[0-4]> <environmentDetail[0-1]> <sceneryDraw[1-25]> <landscapeDraw[1-25]>")]
        [Example("/ub textures 0 1 0 1 25 25", "Sets max settings")]
        [Example("/ub textures 4 0 4 0 1 1", "Sets min settings")]
        [CommandPattern("textures", @"^(?<landscape>[0-4]) (?<landscapeDetail>[01]) (?<environment>[0-4]) (?<environmentDetail>[01]) (?<sceneryDraw>\d+) (?<landscapeDraw>\d+)$")]
        public void DoTextures(string _, Match args) {
            uint.TryParse(args.Groups["landscape"].Value, out uint landscape);
            byte.TryParse(args.Groups["landscapeDetail"].Value, out byte landscapeDetail);
            uint.TryParse(args.Groups["environment"].Value, out uint environment);
            byte.TryParse(args.Groups["environmentDetail"].Value, out byte environmentDetail);
            uint.TryParse(args.Groups["sceneryDraw"].Value, out uint sceneryDraw);
            uint.TryParse(args.Groups["landscapeDraw"].Value, out uint landscapeDraw);

            if (sceneryDraw > 25 || landscapeDraw > 25) {
                LogError($"Requested Texture options were not valid");
                return;
            }
            WriteToChat($"Setting Textures...");
            UBHelper.Core.SetTextures(landscape, landscapeDetail, environment, environmentDetail, sceneryDraw, landscapeDraw);
        }
        #endregion
        #region /ub element[q][ <element>[ x y width height]]
        [Summary("Moves AC UI Elements")]
        [Usage("/ub element[q][ <element>[ x y width height]]")]
        [Example("/ub element", "Lists available elements")]
        [Example("/ub element sbox", "Shows the current position and size of the SBOX element")]
        [Example("/ub element sbox 0 0 800 600", "Moves the SBOX element to 0,0, and resizes it to 800x600")]
        [Example("/ub elementq sbox 0 0 800 600", "Moves the SBOX element to 0,0, and resizes it to 800x600, without spamming chat about it")]
        [CommandPattern("element", @"^(?<element>SBOX|CHAT|FCH1|FCH2|FCH3|FCH4|EXAM|VITS|SVIT|ENVP|PANS|TBAR|INDI|PBAR|COMB|RADA)?( (?<x>\d+) (?<y>\d+) (?<w>\d+) (?<h>\d+))?$", true)]
        public void DoElement(string a, Match args) {
            if (args.Groups["element"].Length == 0) {
                Logger.WriteToChat($"Available Elements: {string.Join(", ", Enum.GetNames(typeof(UBHelper.UIElement)))}");
                return;
            }
            string optname = args.Groups["element"].Value.ToUpper();
            int opt;
            try {
                opt = (int)Enum.Parse(typeof(UBHelper.UIElement), optname);
            }
            catch {
                Logger.Error($"{optname} is not a valid element.");
                return;
            }
            bool quiet = a.Equals("elementq");
            if (args.Groups["x"].Length == 0) {
                System.Drawing.Rectangle current = UBHelper.Core.GetElementPosition((UBHelper.UIElement)opt);
                Logger.WriteToChat($"Element {optname}: X:{current.X}, Y:{current.Y}, Width:{current.Width}, Height:{current.Height}");
                return;
            }
            int.TryParse(args.Groups["x"].Value, out int x);
            int.TryParse(args.Groups["y"].Value, out int y);
            int.TryParse(args.Groups["w"].Value, out int w);
            int.TryParse(args.Groups["h"].Value, out int h);
            UBHelper.Core.MoveElement((UBHelper.UIElement)opt, new System.Drawing.Rectangle(x, y, w, h));
            if (!quiet) Logger.WriteToChat($"Moved {optname} to: X:{x}, Y:{y}, Width:{w}, Height:{h}");
            return;
        }
        #endregion
        #region /ub pcap {enable [bufferDepth],disable,print}
        [Summary("Manage packet captures")]
        [Usage("/ub pcap {enable [bufferDepth] | disable | print}")]
        [Example("/ub pcap enable", "Enable pcap functionality (nothing will be saved until you call /ub pcap print)")]
        [Example("/ub pcap print", "Saves the current pcap buffer to a new file in your plugin storage directory.")]
        [CommandPattern("pcap", @"^ *(?<params>(enable( \d+)?|disable|print)) *$")]
        public void DoPcap(string _, Match args) {
            UB_pcap(args.Groups["params"].Value);
        }
        public void UB_pcap(string parameters) {
            char[] stringSplit = { ' ' };
            string[] parameter = parameters.Split(stringSplit, 2);
            switch (parameter[0]) {
                case "enable":
                    if (parameter.Length == 2 && Int32.TryParse(parameter[1], out int parsedBufferDepth)) {
                        UB.Plugin.PCapBufferDepth.Value = parsedBufferDepth;
                    }

                    if (UB.Plugin.PCapBufferDepth > 65535)
                        Logger.Error($"WARNING: Large buffers can have negative performance impacts on the game. Buffer depths between 1000 and 20000 are recommended.");
                    Logger.WriteToChat($"Enabled rolling PCap logger with a bufferDepth of {UB.Plugin.PCapBufferDepth:n0}. This will consume {(UB.Plugin.PCapBufferDepth * 505):n0} bytes of memory.");
                    Logger.WriteToChat($"Issue the command [/ub pcap print] to write this out to a .pcap file for submission!");
                    UB.Plugin.PCap.Value = true;
                    break;
                case "disable":
                    UB.Plugin.PCap.Value = false;
                    break;
                case "print":
                    string filename = $"{Util.GetPluginDirectory()}\\pkt_{DateTime.UtcNow:yyyy-M-d}_{(int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds}_log.pcap";
                    UBHelper.PCap.Print(filename);
                    break;
                default:
                    Logger.Error("Usage: /ub pcap {enable,disable,print}");
                    break;
            }
        }
        #endregion
        #region /ub videopatch {enable,disable,toggle}
        [Summary("Disables rendering of the 3d world to conserve CPU")]
        [Usage("/ub videopatch {enable | disable | toggle}")]
        [Example("/ub videopatch enable", "Enables the video patch")]
        [Example("/ub videopatch disable", "Disables the video patch")]
        [Example("/ub videopatch toggle", "Toggles the video patch")]
        [CommandPattern("videopatch", @"^ *(?<params>(enable|disable|toggle)) *$")]
        public void DoVideoPatch(string _, Match args) {
            char[] stringSplit = { ' ' };
            string[] parameter = args.Groups["params"].Value.Split(stringSplit, 2);
            switch (parameter[0]) {
                case "enable":
                    UB.Plugin.VideoPatch.Value = true;
                    break;
                case "disable":
                    UB.Plugin.VideoPatch.Value = false;
                    break;
                case "toggle":
                    UB.Plugin.VideoPatch.Value = !UB.Plugin.VideoPatch;
                    break;
                default:
                    Logger.Error("Usage: /ub videopatch {enable,disable,toggle}");
                    break;
            }
        }
        #endregion
        #region /ub globalframelimit <frameRate>
        [Summary("Globally limits frames")]
        [Usage("/ub globalframelimit <frameRate>")]
        [Example("/ub globalframelimit 0", "Disables the frame limiter")]
        [Example("/ub globalframelimit 10", "Sets frame limit to 10fps")]
        [CommandPattern("globalframelimit", @"^(?<frameRate>\d+)$")]
        public void DoGlobalFrameLimit(string _, Match args) {
            int.TryParse(args.Groups["frameRate"].Value, out int frameRate);
            if (frameRate < 0 || frameRate > 500) {
                LogError($"Requested frameRate was not valid ({frameRate}). Must be betwen 0-500");
                return;
            }
            UBLoader.FilterCore.Global.FrameRate.Value = frameRate;
            if (!UB.Plugin.Debug)
                Logger.WriteToChat(UBLoader.FilterCore.Global.FrameRate.FullDisplayValue());
        }
        #endregion
        #region /ub bgframelimit <frameRate>
        [Summary("limits frames while client is not activated")]
        [Usage("/ub bgframelimit <frameRate>")]
        [Example("/ub bgframelimit 0", "Disables the frame limiter")]
        [Example("/ub bgframelimit 10", "Sets frame limit to 10fps")]
        [CommandPattern("bgframelimit", @"^(?<frameRate>\d+)$")]
        public void DoBGFrameLimit(string _, Match args) {
            int.TryParse(args.Groups["frameRate"].Value, out int frameRate);
            if (frameRate < 0 || frameRate > 500) {
                LogError($"Requested frameRate was not valid ({frameRate})");
                return;
            }
            UB.Plugin.BackgroundFrameLimit.Value = frameRate;
            WriteToChat($"Background FrameRate Limited to {frameRate} fps, and saved to Plugin.BackgroundFrameLimit");
        }
        #endregion
        #region /ub logout
        [Summary("Loggs out of the current character")]
        [Usage("/ub logout")]
        [CommandPattern("logout", @"^$")]
        public void Logout(string _, Match _2) {
            Logger.WriteToChat($"Logging Out");
            UB.Core.Actions.Logout();
        }
        #endregion
        #region /ub quit
        [Summary("Closes the client")]
        [Usage("/ub quit")]
        [CommandPattern("quit", @"^$")]
        public void Quit(string _, Match _2) {
            Logger.WriteToChat($"Quitting Client");
            PostMessage(UB.Core.Decal.Hwnd, 0x0002 /* WM_DESTROY */, (IntPtr)0, (UIntPtr)0);
        }
        #endregion
        #endregion Commands

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hhwnd, uint msg, IntPtr wparam, UIntPtr lparam);

        public Client(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            ClientUIProfilesCombo = (HudCombo)UB.MainView.view["ClientUIProfilesCombo"];
            ClientUIProfileCopyTo = (HudButton)UB.MainView.view["ClientUIProfileCopyTo"];
            ClientUIProfileReset = (HudButton)UB.MainView.view["ClientUIProfileReset"];
            ClientUIProfileImport = (HudButton)UB.MainView.view["ClientUIProfileImport"];

            ClientUIProfilesCombo.Change += ClientUIProfilesCombo_Change;
            ClientUIProfileCopyTo.Hit += ClientUIProfileCopyTo_Hit;
            ClientUIProfileReset.Hit += ClientUIProfileReset_Hit;
            ClientUIProfileImport.Hit += ClientUIProfileImport_Hit;

            UIProfile.Changed += SelectedClientUIProfile_Changed;

            UB.ClientUISettings.Changed += ClientUI_Changed;

            UB.MainView.PopulateProfiles(ClientUIProfileExtension, ClientUIProfilesCombo, UIProfile);
            RestoreClientUI();
        }

        private void SelectedClientUIProfile_Changed(object sender, SettingChangedEventArgs e) {
            UB.ClientUISettings.SettingsPath = ClientUIProfilePath;
            UB.MainView.PopulateProfiles(ClientUIProfileExtension, ClientUIProfilesCombo, UIProfile);
        }

        private void ClientUI_Changed(object sender, SettingChangedEventArgs e) {
            if (e.Setting.Parent is UIElementVector v) {
                RestoreClientUIElement(v);
            }
        }

        private void RestoreClientUI() {
            foreach (var element in ClientUI.GetChildren()) {
                if (element is UIElementVector vector) {
                    RestoreClientUIElement(vector);
                }
            }
        }

        private void RestoreClientUIElement(UIElementVector vector) {
            if (!vector.IsDefault) {
                var t = UBHelper.Core.GetElementPosition(vector.UIElement);
                var x = vector.X.IsDefault ? t.X : vector.X;
                var y = vector.Y.IsDefault ? t.Y : vector.Y;
                var width = vector.Width.IsDefault ? t.Width : vector.Width;
                var height = vector.Height.IsDefault ? t.Height : vector.Height;
                UBHelper.Core.MoveElement(vector.UIElement, new Rectangle(x, y, width, height));
            }
        }

        private void ClientUIProfileReset_Hit(object sender, EventArgs e) {
            UB.MainView.ResetProfile(SettingType.ClientUI, UB.ClientUISettings, UIProfile);
        }

        private void ClientUIProfileCopyTo_Hit(object sender, EventArgs e) {
            UB.MainView.CopyProfile(UIProfile, ClientUIProfilePath, (v) => {
                return Path.Combine(Util.GetProfilesDirectory(), $"{v}.{ClientUIProfileExtension}");
            });
        }

        private void ClientUIProfilesCombo_Change(object sender, EventArgs e) {
            UIProfile.Value = ((HudStaticText)ClientUIProfilesCombo[ClientUIProfilesCombo.Current]).Text;
        }

        private void ClientUIProfileImport_Hit(object sender, EventArgs e) {
            UB.MainView.ImportProfile(() => {
                foreach (int i in Enum.GetValues(typeof(UBHelper.UIElement))) {
                    System.Drawing.Rectangle t = UBHelper.Core.GetElementPosition((UBHelper.UIElement)i);
                    var field = ClientUI.GetType().GetField(((UBHelper.UIElement)i).ToString(), Settings.BindingFlags);
                    if (field != null) {
                        ((ISetting)field.FieldType.GetField("X", Settings.BindingFlags).GetValue(field.GetValue(ClientUI))).SetValue(t.X);
                        ((ISetting)field.FieldType.GetField("Y", Settings.BindingFlags).GetValue(field.GetValue(ClientUI))).SetValue(t.Y);
                        ((ISetting)field.FieldType.GetField("Width", Settings.BindingFlags).GetValue(field.GetValue(ClientUI))).SetValue(t.Width);
                        ((ISetting)field.FieldType.GetField("Height", Settings.BindingFlags).GetValue(field.GetValue(ClientUI))).SetValue(t.Height);
                    }
                }
                Logger.WriteToChat($"Imported current ClientUI into profile '{UIProfile}'");
                return true;
            });
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    ClientUIProfilesCombo.Change -= ClientUIProfilesCombo_Change;
                    ClientUIProfileCopyTo.Hit -= ClientUIProfileCopyTo_Hit;
                    ClientUIProfileReset.Hit -= ClientUIProfileReset_Hit;
                    UIProfile.Changed -= SelectedClientUIProfile_Changed;
                }
                disposedValue = true;
            }
        }
    }
}
