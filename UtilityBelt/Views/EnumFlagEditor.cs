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

namespace UtilityBelt.Views {
    public class EnumFlagEditor : IDisposable {
        public VirindiViewService.ViewProperties properties;
        public VirindiViewService.ControlGroup controls;
        public VirindiViewService.HudView view;
        bool disposed = false;

        private HudList ChildList;
        private HudView parentView;
        private OptionResult prop;

        public EnumFlagEditor(HudView parentView, string setting) {
            this.parentView = parentView;
            this.prop = UtilityBeltPlugin.Instance.Settings.GetOptionProperty(setting);

            VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
            parser.ParseFromResource("UtilityBelt.Views.EnumFlagEditor.xml", out properties, out controls);

            view = new VirindiViewService.HudView(properties, controls);

            view.Title = $"Editing {setting}";

            int x = (parentView.Location.X + (parentView.Width / 2)) - (view.Width / 2);
            int y = (parentView.Location.Y + (parentView.Height / 2)) - (view.Height / 2);

            view.Location = new System.Drawing.Point(x, y);

            view.ForcedZOrder = 9999;
            view.Visible = true;

            view.VisibleChanged += View_VisibleChanged;

            ChildList = view != null ? (HudList)view["ChildList"] : new HudList();

            Draw();
        }

        private void View_VisibleChanged(object sender, EventArgs e) {
            try {
                if (!view.Visible) Dispose();
            }
            catch { }
        }

        private void Draw() {
            foreach (var value in Enum.GetValues(prop.Object.GetType())) {
                // this could probably be improved, but we want to sort this list
                HudList.HudListRowAccessor row = null;
                for (var i = 0; i < ChildList.RowCount; i++) {
                    if (string.Compare(value.ToString(), ((HudCheckBox)ChildList[i][0]).Text) < 0) {
                        row = ChildList.InsertRow(i);
                        break;
                    }
                }

                if (row == null) row = ChildList.AddRow();

                ((HudCheckBox)row[0]).Checked = (((uint)prop.Object & (uint)value) != 0);
                ((HudCheckBox)row[0]).Text = value.ToString();
                ((HudCheckBox)row[0]).Change += (s, e) => {
                    var flag = (uint)Enum.Parse(prop.Object.GetType(), value.ToString());

                    if (((HudCheckBox)s).Checked) {
                        var newValue = (uint)prop.Property.GetValue(prop.Parent, null) | flag;
                        prop.Property.SetValue(prop.Parent, Enum.ToObject(prop.Object.GetType(), newValue), null);
                    }
                    else {
                        var newValue = (uint)prop.Property.GetValue(prop.Parent, null) & ~flag;
                        prop.Property.SetValue(prop.Parent, Enum.ToObject(prop.Object.GetType(), newValue), null);
                    }
                };
            }
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
