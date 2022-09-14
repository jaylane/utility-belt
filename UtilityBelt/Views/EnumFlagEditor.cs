﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using VirindiViewService.Controls;
using UBService.Lib.Settings;

namespace UtilityBelt.Views {
    public class EnumFlagEditor : IDisposable {
        public VirindiViewService.ViewProperties properties;
        public VirindiViewService.ControlGroup controls;
        public VirindiViewService.HudView view;
        bool disposed = false;

        private HudList ChildList;
        private HudView parentView;
        private ISetting Setting;

        public EnumFlagEditor(HudView parentView, ISetting setting) {
            this.parentView = parentView;
            Setting = setting;

            VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
            parser.ParseFromResource("UtilityBelt.Views.EnumFlagEditor.xml", out properties, out controls);

            view = new VirindiViewService.HudView(properties, controls);

            view.Title = $"Editing {setting.Name}";

            int x = (parentView.Location.X + (parentView.Width / 2)) - (view.Width / 2);
            int y = (parentView.Location.Y + (parentView.Height / 2)) - (view.Height / 2);

            view.Location = new System.Drawing.Point(x, y);

            view.ForcedZOrder = 9999;
            view.Visible = true;

            view.VisibleChanged += View_VisibleChanged;

            ChildList = (HudList)view["ChildList"];

            Draw();
        }

        private void View_VisibleChanged(object sender, EventArgs e) {
            try {
                if (!view.Visible) Dispose();
            }
            catch { }
        }

        private void Draw() {
            foreach (var value in Enum.GetValues(Setting.GetType())) {
                // this could probably be improved, but we want to sort this list
                HudList.HudListRowAccessor row = null;
                for (var i = 0; i < ChildList.RowCount; i++) {
                    if (string.Compare(value.ToString(), ((HudCheckBox)ChildList[i][0]).Text) < 0) {
                        row = ChildList.InsertRow(i);
                        break;
                    }
                }

                if (row == null) row = ChildList.AddRow();

                ((HudCheckBox)row[0]).Checked = (((uint)(Setting.GetValue()) & (uint)value) != 0);
                ((HudCheckBox)row[0]).Text = value.ToString();
                ((HudCheckBox)row[0]).Change += (s, e) => {
                    try {
                        var flag = (uint)Enum.Parse(Setting.GetType(), value.ToString());

                        if (((HudCheckBox)s).Checked) {
                            var newValue = (uint)Setting.GetValue() | flag;
                            Setting.SetValue(Enum.ToObject(Setting.GetValue().GetType(), newValue));
                        }
                        else {
                            var newValue = (uint)Setting.GetValue() & ~flag;
                            Setting.SetValue(Enum.ToObject(Setting.GetValue().GetType(), newValue));
                        }
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
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
