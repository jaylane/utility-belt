using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using VirindiViewService.Controls;
using UBLoader.Lib.Settings;
using Decal.Adapter;
using System.Runtime.InteropServices;

namespace UtilityBelt.Views {

    public class LongStringEditor : IDisposable {
        public VirindiViewService.ViewProperties properties;
        public VirindiViewService.ControlGroup controls;
        public VirindiViewService.HudView view;
        public bool IsDisposed { get; private set; }

        private HudView Parent;
        private HudTextBox Textbox;

        private HudTextBox Text;
        private HudButton Cancel;
        private HudButton Save;

        public event EventHandler Saved;

        #region Imports
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }
        #endregion Imports

        public LongStringEditor(HudView parentView, HudTextBox textbox) {
            Parent = parentView;
            Textbox = textbox;
            VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
            parser.ParseFromResource("UtilityBelt.Views.LongStringEditor.xml", out properties, out controls);

            view = new VirindiViewService.HudView(properties, controls);
            view.Location = new Point(10, parentView.Location.Y + 50);
            if (GetWindowRect((IntPtr)CoreManager.Current.Decal.Hwnd, out RECT rect))
                view.Width = rect.Right - rect.Left - 40;
            view.ForcedZOrder = 9999;
            view.ShowInBar = false;
            view.Visible = true;

            Text = (HudTextBox)view["Text"];
            Cancel = (HudButton)view["Cancel"];
            Save = (HudButton)view["Save"];

            Cancel.Hit += Cancel_Hit;
            Save.Hit += Save_Hit;
            Text.Text = Textbox.Text;
            view.VisibleChanged += View_VisibleChanged;
        }

        private void View_VisibleChanged(object sender, EventArgs e) {
            Dispose();
        }

        private void Save_Hit(object sender, EventArgs e) {
            Textbox.Text = Text.Text;
            Saved?.Invoke(this, EventArgs.Empty);
            Dispose();
        }

        private void Cancel_Hit(object sender, EventArgs e) {
            try {
                Dispose();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!IsDisposed) {
                if (disposing) {
                    view.VisibleChanged -= View_VisibleChanged;

                    if (Cancel != null) Cancel.Hit -= Cancel_Hit;
                    if (Save != null) Save.Hit += Save_Hit;
                    if (view != null) view.Dispose();
                }
                IsDisposed = true;
            }
        }
    }
}
