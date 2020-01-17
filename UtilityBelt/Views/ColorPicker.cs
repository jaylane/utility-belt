using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using VirindiViewService;
using VirindiViewService.Controls;

namespace UtilityBelt.Views {
    public class ColorPickerSaveEventArgs : EventArgs {
        public Color Color;

        public ColorPickerSaveEventArgs(Color color) {
            Color = color;
        }
    }
    public class ColorPickerChangeEventArgs : EventArgs {
        public Color Color;

        public ColorPickerChangeEventArgs(Color color) {
            Color = color;
        }
    }

    public class ColorPicker : IDisposable {
        public VirindiViewService.ViewProperties properties;
        public VirindiViewService.ControlGroup controls;
        public VirindiViewService.HudView view;

        HudHSlider Alpha { get; set; }
        HudHSlider Red { get; set; }
        HudHSlider Green { get; set; }
        HudHSlider Blue { get; set; }

        HudButton Cancel { get; set; }
        HudButton Save { get; set; }
        HudPictureBox ColorPreview { get; set; }
        HudFixedLayout ColorPreviewLayout { get; set; }

        Color Color;

        public Color DefaultColor;
        bool disposed = false;
        private ACImage colorPreviewImage;

        public event EventHandler<ColorPickerSaveEventArgs> RaiseColorPickerSaveEvent;
        public event EventHandler<ColorPickerChangeEventArgs> RaiseColorPickerChangeEvent;
        public event EventHandler<EventArgs> RaiseColorPickerCancelEvent;

        public ColorPicker(MainView mainView, string setting, Color defaultColor) {
            DefaultColor = defaultColor;
            Color = DefaultColor;

            VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
            parser.ParseFromResource("UtilityBelt.Views.ColorPicker.xml", out properties, out controls);

            view = new VirindiViewService.HudView(properties, controls);

            view.Title = $"Set a color for {setting}";

            int x = (mainView.view.Location.X + (mainView.view.Width / 2)) - (view.Width / 2);
            int y = (mainView.view.Location.Y + (mainView.view.Height / 2)) - (view.Height / 2);

            view.Location = new System.Drawing.Point(x, y);

            view.ForcedZOrder = 9999;
            view.Icon = GetIconImage();
            view.Visible = true;

            Cancel = (HudButton)view["Cancel"];
            Save = (HudButton)view["Save"];

            ColorPreviewLayout = (HudFixedLayout)view["ColorPreviewLayout"];
            ColorPreview = new HudPictureBox();
            ColorPreview.Image = GetIconImage();

            ColorPreviewLayout.AddControl(ColorPreview, new Rectangle(0, 0, 100, 100));

            Alpha = (HudHSlider)view["Alpha"];
            Red = (HudHSlider)view["Red"];
            Green = (HudHSlider)view["Green"];
            Blue = (HudHSlider)view["Blue"];

            Alpha.Position = Color.A;
            Red.Position = Color.R;
            Green.Position = Color.G;
            Blue.Position = Color.B;

            Alpha.Changed += Sliders_Changed;
            Red.Changed += Sliders_Changed;
            Green.Changed += Sliders_Changed;
            Blue.Changed += Sliders_Changed;

            Cancel.Hit += Cancel_Hit;
            Save.Hit += Save_Hit;
        }

        private ACImage GetColorPreviewImage() {
            if (colorPreviewImage != null)
                return colorPreviewImage;
            using (var bmp = new Bitmap(ColorPreview.ClipRegion.Width, ColorPreview.ClipRegion.Height)) {

                using (Graphics gfx = Graphics.FromImage(bmp)) {
                    using (SolidBrush brush = new SolidBrush(Color)) {
                        gfx.FillRectangle(brush, 0, 0, ColorPreview.ClipRegion.Width, ColorPreview.ClipRegion.Height);
                    }
                }
                colorPreviewImage = new ACImage(bmp);
            }

            return colorPreviewImage;
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

        private void Sliders_Changed(int min, int max, int pos) {
            try {
                Color = Color.FromArgb(Alpha.Position, Red.Position, Green.Position, Blue.Position);
                ColorPreview.Image = GetColorPreviewImage();
                view.Icon = GetIconImage();
                RaiseColorPickerChangeEvent?.Invoke(null, new ColorPickerChangeEventArgs(Color));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Save_Hit(object sender, EventArgs e) {
            try {
                RaiseColorPickerSaveEvent?.Invoke(null, new ColorPickerSaveEventArgs(Color));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Cancel_Hit(object sender, EventArgs e) {
            try {
                RaiseColorPickerCancelEvent?.Invoke(null, e);
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
