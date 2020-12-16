using Decal.Adapter;
using Decal.Interop.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Dungeon;
using UBLoader.Lib.Settings;
using VirindiViewService;

namespace UtilityBelt.Tools {
    [Name("DerethTime")]
    [Summary("Provides a hud that shows the current day/night cycle, and expressions for getting the current game time.")]
    [FullDescription(@"The hud shows the current ingame day/night cycle. The icon rotates counter-clockwise as the day progresses. To move the hud you can hold ctrl and drag it around. Holding ctrl also allows you to quickly disable the hud. You can re-enable the hud on the main ub plugin window.")]
    unsafe public class DerethTime : ToolBase {
        const double TicksInHour = 476.25;
        const double HoursInDay = 16;
        const double DaysInMonth = 30;
        const double MonthsInYear = 12;

        double MinuteLength { get { return TicksInHour / 60; } }
        double HourLength { get { return TicksInHour; } }
        double DayLength { get { return TicksInHour * HoursInDay; } }
        double MonthLength { get { return DayLength * DaysInMonth; } }
        double YearLength { get { return MonthLength * MonthsInYear; } }

        internal readonly List<string> MonthNames = new List<string>() {
            "Morningthaw",
            "Solclaim",
            "Seedsow",
            "Leafdawning",
            "Verdantine",
            "Thistledown",
            "HarvestGain",
            "Leafcull",
            "Frostfell",
            "Snowreap",
            "Coldeve",
            "Wintersebb"
        };

        internal readonly List<string> HourNames = new List<string>() {
            "Darktide",
            "Darktide-and-Half",
            "Foredawn",
            "Foredawn-and-Half",
            "Dawnsong",
            "Dawnsong-and-Half",
            "Morntide",
            "Morntide-and-Half",
            "Midsong",
            "Midsong-and-Half",
            "Warmtide",
            "Warmtide-and-Half",
            "Evensong",
            "Evensong-and-Half",
            "Gloaming",
            "Gloaming-and-Half"
        };

        public double CurrentTime {
            // 0 ticks is actually 10 years, 8 hours, and 210 ticks. so we add that here
            get => *(double*)0x008379A8 - 210 + (HourLength * 8) + (YearLength * 10);
        }

        public int GameYear {
            get => (int)Math.Floor(CurrentTime / YearLength);
        }

        public int GameMonth {
            get => (int)Math.Floor((CurrentTime % YearLength) / MonthLength);
        }

        public int GameDay {
            get => (int)Math.Floor((CurrentTime % MonthLength) / DayLength) + 1;
        }

        public int GameHour {
            get => (int)Math.Floor((CurrentTime % DayLength) / HourLength);
        }

        public int GameMinute {
            get => (int)Math.Floor((CurrentTime % HourLength) / MinuteLength);
        }

        public bool IsDay {
            get { return GameHour >= 4 && GameHour < 12; }
        }

        public int TicksUntilNight {
            get => IsDay ? (int)Math.Round(((12 - GameHour) * HourLength) - (CurrentTime % HourLength)) : 0;
        }

        public int TicksUntilDay {
            get {
                int hour = GameHour;
                if (hour <= 4)
                    hour += 16;
                return !IsDay ? (int) Math.Round(((20 - hour) * HourLength) - (CurrentTime % HourLength)) : 0;
            }
        }

        #region Config
        [Summary("Enabled")]
        public readonly Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("DerethTime HUD Position X")]
        public readonly CharacterState<int> HudX = new CharacterState<int>(415);

        [Summary("DerethTime HUD Position Y")]
        public readonly CharacterState<int> HudY = new CharacterState<int>(5);

        [Summary("Show the label text, with minutes remaining until the next day/night")]
        public readonly Setting<bool> ShowLabel = new Setting<bool>(true);
        #endregion

        #region Expressions
        #region getgameyear[]
        [ExpressionMethod("getgameyear")]
        [ExpressionReturn(typeof(double), "Returns the current ingame year. Years have 360 days.")]
        [Summary("Gets the current ingame year")]
        [Example("getgameyear[]", "Returns the current ingame year, ie 249")]
        public object Getgameyear() {
            return GameYear;
        }
        #endregion //getgameyear[]
        #region getgamemonth[]
        [ExpressionMethod("getgamemonth")]
        [ExpressionReturn(typeof(double), "Returns the current ingame month as a number")]
        [Summary("Gets the current ingame month as a number. Months are: 0=Morningthaw, 1=Solclaim, 2=Seedsow, 3=Leafdawning, 4=Verdantine, 5=Thistledown, 6=Harvestgain, 7=Leafcull, 8=Frostfell, 9=Snowreap, 10=Coldeve, 11=Wintersebb")]
        [Example("getgamemonth[]", "Returns the current ingame month as a number, ie 8")]
        public object Getgamemonth() {
            return GameMonth;
        }
        #endregion //getgamemonth[]
        #region getgamemonthname[]
        [ExpressionMethod("getgamemonthname")]
        [ExpressionParameter(0, typeof(double), "monthIndex", "month index to get the name of")]
        [ExpressionReturn(typeof(string), "Returns the name of a month index")]
        [Summary("Returns the name of a month index. Months are: 0=Morningthaw, 1=Solclaim, 2=Seedsow, 3=Leafdawning, 4=Verdantine, 5=Thistledown, 6=Harvestgain, 7=Leafcull, 8=Frostfell, 9=Snowreap, 10=Coldeve, 11=Wintersebb")]
        [Example("getmonthname[1]", "Returns Solclaim")]
        public object getgamemonthname(double month) {
            if (month >= 0 && month < MonthNames.Count)
                return MonthNames[(int)month];

            Logger.WriteToChat($"Bad Month index: {(int)month}");
            return "";
        }
        #endregion //getgamemonthname[]
        #region getgameday[]
        [ExpressionMethod("getgameday")]
        [ExpressionReturn(typeof(double), "Returns the current ingame day of the month. 1-30")]
        [Summary("Returns the current ingame day of the month. 1-30.")]
        [Example("getgamemonth[]", "returns the current ingame day of the month, ie 8")]
        public object Getgameday() {
            return GameDay;
        }
        #endregion //getgameday[]
        #region getgamehourname[]
        [ExpressionMethod("getgamehourname")]
        [ExpressionParameter(0, typeof(double), "hourIndex", "hour index to get the name of")]
        [ExpressionReturn(typeof(string), "Returns the name of a hour index")]
        [Summary("Returns the name of a hour index.")]
        [Example("getgamehourname[1]", "Returns Darktide-and-Half")]
        public object getgamehourname(double hour) {
            if (hour >= 0 && hour < HourNames.Count)
                return HourNames[(int)hour];

            Logger.WriteToChat($"Bad Hour index: {(int)hour}");
            return "";
        }
        #endregion //getgamemonthname[]
        #region getgamehour[]
        [ExpressionMethod("getgamehour")]
        [ExpressionReturn(typeof(double), "Returns the current ingame hour. 0-16")]
        [Summary("Returns the current ingame hour of the day. Days have 16 hours")]
        [Example("getgamehour[]", "returns the current ingame hour of the day, ie 3")]
        public object Getgamehour() {
            return GameHour;
        }
        #endregion //getgameday[]
        #region getminutesuntilday[]
        [ExpressionMethod("getminutesuntilday")]
        [ExpressionReturn(typeof(double), "Returns real world minutes left until the next day cycle")]
        [Summary("Returns real world minutes left until the next day cycle")]
        [Example("getminutesuntilday[]", "gets the number of real world minutes left until the next day cycle")]
        public object Getminutesuntilday() {
            return IsDay ? 0 : TicksUntilDay / 60;
        }
        #endregion //getminutesuntilday[]
        #region getminutesuntilnight[]
        [ExpressionMethod("getminutesuntilnight")]
        [ExpressionReturn(typeof(double), "Returns real world minutes left until the next night cycle")]
        [Summary("Returns real world minutes left until the next night cycle")]
        [Example("getminutesuntilnight[]", "gets the number of real world minutes left until the next night cycle")]
        public object Getminutesuntilnight() {
            return !IsDay ? 0 : TicksUntilNight / 60;
        }
        #endregion //getminutesuntilday[]
        #region getgameticks[]
        [ExpressionMethod("getgameticks")]
        [ExpressionReturn(typeof(double), "Returns the current ingame ticks")]
        [Summary("Returns the current ingame ticks.")]
        [Example("getgameticks[]", "returns the current ingame ticks")]
        public object Getgameticks() {
            return CurrentTime;
        }
        #endregion //getgameticks[]
        #region getisday[]
        [ExpressionMethod("getisday")]
        [ExpressionReturn(typeof(double), "Returns 1 if it is currently day time, 0 otherwise")]
        [Summary("Returns 1 if it is currently day time, 0 otherwise")]
        [Example("getisday[]", "returns 1 if it is currently day time")]
        public object Isday() {
            return IsDay;
        }
        #endregion //isday[]
        #region getisnight[]
        [ExpressionMethod("getisnight")]
        [ExpressionReturn(typeof(double), "Returns 1 if it is currently night time, 0 otherwise")]
        [Summary("Returns 1 if it is currently night time, 0 otherwise")]
        [Example("getisnight[]", "returns 1 if it is currently night time")]
        public object Isnight() {
            return !IsDay;
        }
        #endregion //isnight[]
        #endregion //Expressions

        private DxTexture daynightIcon = null;
        private DxTexture pointerIcon;
        private string fontFace;
        private int fontWeight;
        private UBHud hud;
        private int LabelFontSize = 10;

        private TimerClass drawTimer;

        public DerethTime(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            try {
                fontFace = UB.LandscapeMapView.view.MainControl.Theme.GetVal<string>("DefaultTextFontFace");
                fontWeight = UB.LandscapeMapView.view.MainControl.Theme.GetVal<int>("ViewTextFontWeight");

                Enabled.Changed += DerethTime_PropertyChanged;
                ShowLabel.Changed += DerethTime_PropertyChanged;

                if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                    UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
                else
                    TryEnable();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void TryEnable() {
            if (Enabled) {
                CreateHud();
                if (drawTimer == null) {
                    drawTimer = new TimerClass();
                    drawTimer.Timeout += DrawTimer_Timeout;
                    drawTimer.Start(1000 * 5); // 0.2 fps max
                }
                hud.Render();
            }
            else {
                if (drawTimer != null)
                    drawTimer.Stop();
                ClearHud();
                drawTimer = null;
            }
        }

        #region Event Handlers
        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
            TryEnable();
        }

        private void DerethTime_PropertyChanged(object sender, SettingChangedEventArgs e) {
            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                return;

            switch (e.PropertyName) {
                case "Enabled":
                    TryEnable();
                    break;
                case "ShowLabel":
                    hud?.Resize(ShowLabel ? 150 : 32, 32);
                    hud?.Render();
                    break;
            }
        }

        private void DrawTimer_Timeout(Decal.Interop.Input.Timer Source) {
            hud.Render();
        }

        private void Hud_OnMove(object sender, EventArgs e) {
            HudX.Value = hud.X;
            HudY.Value = hud.Y;
        }

        private void Hud_OnClose(object sender, EventArgs e) {
            Enabled.Value = false;
        }
        #endregion // Event Handlers

        #region Hud Rendering
        internal void CreateHud() {
            try {
                if (daynightIcon == null)
                    daynightIcon = TextureCache.TextureFromBitmapResource("UtilityBelt.Resources.icons.daynight.png");

                if (pointerIcon == null)
                    pointerIcon = TextureCache.TextureFromBitmapResource("UtilityBelt.Resources.icons.arrow.png");

                if (hud == null) {
                    hud = UB.Huds.CreateHud(HudX, HudY, ShowLabel ? 150 : 32, 32);
                    hud.OnRender += Hud_OnRender;
                    hud.OnMove += Hud_OnMove;
                    hud.OnClose += Hud_OnClose;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void ClearHud() {
            if (hud == null)
                return;
            hud.OnRender -= Hud_OnRender;
            hud.OnMove -= Hud_OnMove;
            hud.OnClose -= Hud_OnClose;
            hud.Dispose();
            hud = null;
        }


        private void Hud_OnRender(object sender, EventArgs e) {
            if (!Enabled || hud == null || hud.Texture == null || hud.Texture.IsDisposed)
                return;

            try {
                hud.Texture.BeginRender();
                hud.Texture.Clear();
                hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.FromArgb(0, 0, 0, 0));

                var rot = ((((GameHour + 4) * HourLength) % (HourLength * 16)) + (GameMinute * MinuteLength)) / (HourLength * 16) * 360f;

                hud.Texture.DrawTextureRotated(daynightIcon, new Rectangle(0, 0, daynightIcon.Width, daynightIcon.Height), new Point(16, 16), Color.White.ToArgb(), (float)((360-rot) * Math.PI / 180));

                var arrowSize = 8;
                hud.Texture.DrawTextureRotated(pointerIcon, new Rectangle(0, 0, 32, arrowSize), new Point(16, arrowSize / 2), Color.White.ToArgb(), (float)(180 * Math.PI / 180));

                if (ShowLabel) {
                    string text = "";
                    int minutesLeft = 0;
                    if (IsDay) {
                        minutesLeft = TicksUntilNight / 60;
                        text = $"{minutesLeft:N0}m until night";
                    }
                    else {
                        minutesLeft = TicksUntilDay / 60;
                        text = $"{minutesLeft:N0}m until day";
                    }

                    Color labelColor = Color.White;
                    if (minutesLeft <= 1)
                        labelColor = Color.Red;
                    else if (minutesLeft <= 3)
                        labelColor = Color.DarkOrange;
                    else if (minutesLeft <= 5)
                        labelColor = Color.Orange;

                    hud.Texture.BeginText(fontFace, LabelFontSize, 200, false);
                    hud.DrawShadowText(text, 34, 8, hud.Width - 32, hud.Height - 32, labelColor, Color.Black);
                    hud.Texture.EndText();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                hud.Texture.EndRender();
            }
        }
        #endregion

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Enabled.Changed -= DerethTime_PropertyChanged;
            ShowLabel.Changed -= DerethTime_PropertyChanged;
            if (drawTimer != null) drawTimer.Stop();
            if (hud != null) {
                hud.OnRender -= Hud_OnRender;
                hud.OnMove -= Hud_OnMove;
                hud.OnClose -= Hud_OnClose;
                hud.Dispose();
            }
            if (daynightIcon != null) daynightIcon.Dispose();
            if (pointerIcon != null) pointerIcon.Dispose();
        }
    }
}
