using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Lib.Settings {
    public class ColorToggleRenderOption : ColorToggleOption {
        [Summary("Draw as wireframe")]
        public readonly Setting<bool> DrawWireFrame = new Setting<bool>(true);

        [Summary("Draw as solid")]
        public readonly Setting<bool> DrawSolid = new Setting<bool>(true);

        public ColorToggleRenderOption(bool enabled, int defaultColor, bool drawWireFrame, bool drawSolid) : base(enabled, defaultColor) {
            DrawWireFrame.Value = drawWireFrame;
            DrawSolid.Value = drawSolid;
        }

        new public string ToString() {
            return $"Enabled:{Enabled} Color:{Color} WireFrame:{DrawWireFrame} Solid:{DrawSolid}";
        }

        public bool Equals(ColorToggleRenderOption obj) {
            return ToString() == obj.ToString();
        }
    }
}
