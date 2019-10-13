using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using VirindiViewService.Controls;

namespace UtilityBelt.Views {
    public class MarkerOptionsSaveEventArgs : EventArgs {
        public MarkerToggleOption Options;

        public MarkerOptionsSaveEventArgs(MarkerToggleOption options) {
            Options = options;
        }
    }
    public class MarkerOptionsChangeEventArgs : EventArgs {
        public MarkerToggleOption Options;

        public MarkerOptionsChangeEventArgs(MarkerToggleOption options) {
            Options = options;
        }
    }

    public class MarkerOptions : IDisposable {
        public VirindiViewService.ViewProperties properties;
        public VirindiViewService.ControlGroup controls;
        public VirindiViewService.HudView view;

        HudHSlider Alpha { get; set; }
        HudHSlider Red { get; set; }
        HudHSlider Green { get; set; }
        HudHSlider Blue { get; set; }

        HudTextBox AlphaText { get; set; }
        HudTextBox RedText { get; set; }
        HudTextBox BlueText { get; set; }
        HudTextBox GreenText { get; set; }
        HudTextBox HexText { get; set; }

        HudCheckBox ShowLabelText { get; set; }
        HudCheckBox UseIcon { get; set; }
        HudTextBox Size { get; set; }

        HudButton Cancel { get; set; }
        HudButton Save { get; set; }
        HudPictureBox ColorPreview { get; set; }
        HudFixedLayout ColorPreviewLayout { get; set; }

        Color Color;

        MarkerToggleOption Options;

        bool internallyUpdating = false;
        bool disposed = false;

        public event EventHandler<MarkerOptionsSaveEventArgs> RaiseSaveEvent;
        public event EventHandler<MarkerOptionsChangeEventArgs> RaiseChangeEvent;
        public event EventHandler<EventArgs> RaiseCancelEvent;

        public MarkerOptions(MainView mainView, MarkerToggleOption options) {
            Options = options;

            VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
            parser.ParseFromResource("UtilityBelt.Views.MarkerOptions.xml", out properties, out controls);

            view = new VirindiViewService.HudView(properties, controls);

            view.Title = $"Marker options for {options.Name}";

            int x = (mainView.view.Location.X + (mainView.view.Width / 2)) - (view.Width / 2);
            int y = (mainView.view.Location.Y + (mainView.view.Height / 2)) - (view.Height / 2);

            view.Location = new System.Drawing.Point(x, y);

            view.ForcedZOrder = 9999;
            view.Icon = GetIconImage();
            view.Visible = true;

            Alpha = (HudHSlider)view["Alpha"];
            Red = (HudHSlider)view["Red"];
            Green = (HudHSlider)view["Green"];
            Blue = (HudHSlider)view["Blue"];
            AlphaText = (HudTextBox)view["AlphaText"];
            RedText = (HudTextBox)view["RedText"];
            GreenText = (HudTextBox)view["GreenText"];
            BlueText = (HudTextBox)view["BlueText"];
            HexText = (HudTextBox)view["HexText"];
            Size = (HudTextBox)view["Size"];
            UseIcon = (HudCheckBox)view["UseIcon"];
            ShowLabelText = (HudCheckBox)view["ShowLabelText"];
            Cancel = (HudButton)view["Cancel"];
            Save = (HudButton)view["Save"];

            UseIcon.Checked = options.UseIcon;
            Size.Text = options.Size.ToString();
            ShowLabelText.Checked = options.ShowLabel;

            ColorPreviewLayout = view != null ? (HudFixedLayout)view["ColorPreviewLayout"] : new HudFixedLayout();
            ColorPreview = new HudPictureBox();
            ColorPreview.Image = GetIconImage();

            ColorPreviewLayout.AddControl(ColorPreview, new Rectangle(0, 0, 100, 100));

            UpdateColor(Color.FromArgb(options.Color));

            UpdateSliderValues();
            UpdateTextValues();
            UpdateHexValue();

            Alpha.Changed += Sliders_Changed;
            Red.Changed += Sliders_Changed;
            Green.Changed += Sliders_Changed;
            Blue.Changed += Sliders_Changed;

            AlphaText.Change += Text_Change;
            RedText.Change += Text_Change;
            GreenText.Change += Text_Change;
            BlueText.Change += Text_Change;

            HexText.Change += HexText_Change;

            Size.Change += Option_Change;
            UseIcon.Change += Option_Change;
            ShowLabelText.Change += Option_Change;

            Cancel.Hit += Cancel_Hit;
            Save.Hit += Save_Hit;
        }

        private ACImage GetColorPreviewImage() {
            var bmp = new Bitmap(ColorPreview.ClipRegion.Width, ColorPreview.ClipRegion.Height);

            using (Graphics gfx = Graphics.FromImage(bmp)) {
                using (SolidBrush brush = new SolidBrush(Color)) {
                    gfx.FillRectangle(brush, 0, 0, ColorPreview.ClipRegion.Width, ColorPreview.ClipRegion.Height);
                }
            }

            return new ACImage(bmp);
        }

        private ACImage GetIconImage() {
            var bmp = new Bitmap(32, 32);

            using (Graphics gfx = Graphics.FromImage(bmp)) {
                using (SolidBrush brush = new SolidBrush(Color)) {
                    gfx.FillRectangle(brush, 0, 0, 32, 32);
                }
            }

            return new ACImage(bmp);
        }
        private void UpdateColor(Color color) {
            Color = color;
            ColorPreview.Image = GetColorPreviewImage();
            view.Icon = GetIconImage();

            Options.Color = color.ToArgb();
        }

        private void Sliders_Changed(int min, int max, int pos) {
            UpdateColor(Color.FromArgb(Alpha.Position, Red.Position, Green.Position, Blue.Position));

            UpdateTextValues();
            UpdateHexValue();

            FireChangeEvent();
        }

        private void Text_Change(object sender, EventArgs e) {
            int a, r, g, b;

            if (!int.TryParse(AlphaText.Text, out a)) return;
            if (!int.TryParse(RedText.Text, out r)) return;
            if (!int.TryParse(GreenText.Text, out g)) return;
            if (!int.TryParse(BlueText.Text, out b)) return;

            UpdateColor(Color.FromArgb(a, r, g, b));

            UpdateSliderValues();
            UpdateHexValue();

            FireChangeEvent();
        }

        private void HexText_Change(object sender, EventArgs e) {
            int c;

            if (!int.TryParse(HexText.Text, System.Globalization.NumberStyles.HexNumber, null, out c)) return;

            UpdateColor(Color.FromArgb(c));

            UpdateSliderValues();
            UpdateTextValues();

            FireChangeEvent();
        }

        private void Option_Change(object sender, EventArgs e) {
            int size;

            Options.ShowLabel = ShowLabelText.Checked;
            Options.UseIcon = UseIcon.Checked;

            if (int.TryParse(Size.Text, out size)) {
                Options.Size = size;
            }

            FireChangeEvent();
        }

        private void FireChangeEvent() {
            int size;

            if (internallyUpdating) return;
            if (!int.TryParse(Size.Text, out size)) return;

            var options = new MarkerToggleOption(null, Options.Enabled, UseIcon.Checked, ShowLabelText.Checked, Color.ToArgb(), size);
            RaiseChangeEvent?.Invoke(null, new MarkerOptionsChangeEventArgs(options));
        }

        private void UpdateSliderValues() {
            internallyUpdating = true;
            Alpha.Position = Color.A;
            Red.Position = Color.R;
            Green.Position = Color.G;
            Blue.Position = Color.B;
            internallyUpdating = false;
        }

        private void UpdateHexValue() {
            internallyUpdating = true;
            AlphaText.Text = Color.A.ToString();
            RedText.Text = Color.R.ToString();
            GreenText.Text = Color.G.ToString();
            BlueText.Text = Color.B.ToString();
            internallyUpdating = false;
        }

        private void UpdateTextValues() {
            internallyUpdating = true;
            HexText.Text = Color.ToArgb().ToString("X8");
            internallyUpdating = false;
        }

        private void Save_Hit(object sender, EventArgs e) {
            try {
                int size;

                if (!int.TryParse(Size.Text, out size)) return;

                var options = new MarkerToggleOption(null, Options.Enabled, UseIcon.Checked, ShowLabelText.Checked, Color.ToArgb(), size);
                RaiseSaveEvent?.Invoke(null, new MarkerOptionsSaveEventArgs(options));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Cancel_Hit(object sender, EventArgs e) {
            try {
                RaiseCancelEvent?.Invoke(null, e);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    if (view != null) view.Dispose();
                }
                disposed = true;
            }
        }
    }
}
