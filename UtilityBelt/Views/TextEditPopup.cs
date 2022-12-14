using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using VirindiViewService.Controls;
using UtilityBelt.Service.Lib.Settings;

namespace UtilityBelt.Views {
    public class TextEditPopup : IDisposable {
        public ViewProperties properties;
        public ControlGroup controls;
        public HudView view;
        bool disposed = false;

        private HudView parentView;
        private HudButton OKButton;
        private HudButton CancelButton;
        private HudStaticText DialogLabel;
        private HudTextBox Textbox;

        public EventHandler ClickedOK;
        public EventHandler ClickedCancel;

        public string Value { get => Textbox.Text; }

        public TextEditPopup(HudView parentView, string initialValue="", string label = "Enter Value:", string okText = "OK", string cancelText = "Cancel") {
            this.parentView = parentView;

            VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
            parser.ParseFromResource("UtilityBelt.Views.TextEditPopup.xml", out properties, out controls);

            view = new HudView(properties, controls);

            view.Title = label;

            int x = (parentView.Location.X + (parentView.Width / 2)) - (view.Width / 2);
            int y = (parentView.Location.Y + (parentView.Height / 2)) - (view.Height / 2);

            view.Location = new System.Drawing.Point(x, y);
            view.ShowInBar = false;
            view.ForcedZOrder = 9999;
            view.Visible = true;

            view.VisibleChanged += View_VisibleChanged;

            Textbox = (HudTextBox)view["Textbox"];
            OKButton = (HudButton)view["OKButton"];
            CancelButton = (HudButton)view["CancelButton"];
            DialogLabel = (HudStaticText)view["DialogLabel"];

            DialogLabel.Text = label;
            Textbox.Text = initialValue;
            OKButton.Text = okText;
            CancelButton.Text = cancelText;

            OKButton.Hit += OKButton_Hit;
            CancelButton.Hit += CancelButton_Hit;
        }

        private void CancelButton_Hit(object sender, EventArgs e) {
            ClickedCancel?.Invoke(this, EventArgs.Empty);
            Dispose();
        }

        private void OKButton_Hit(object sender, EventArgs e) {
            ClickedOK?.Invoke(this, EventArgs.Empty);
            Dispose();
        }

        private void View_VisibleChanged(object sender, EventArgs e) {
            if (!view.Visible) {
                ClickedCancel?.Invoke(this, EventArgs.Empty);
                Dispose();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    OKButton.Hit -= OKButton_Hit;
                    CancelButton.Hit -= CancelButton_Hit;
                    if (view != null) view.Dispose();
                }
                disposed = true;
            }
        }
    }
}
