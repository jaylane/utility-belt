using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UBService.Lib;
using UBService.Lib.Settings;
using UBService.Views;
using static System.Net.Mime.MediaTypeNames;

namespace UBService.Views {

    public class ThemeEditor : IDisposable {
        string colorFilter = "";
        string sizeFilter = "";
        ImGuiColorEditFlags alpha_flags = 0;
        float min_widget_width = 0;
        private Dictionary<string, FieldAttributeInfo> sizeAttributes = new Dictionary<string, FieldAttributeInfo>();
        private Dictionary<string, FieldAttributeInfo> colorAttributes = new Dictionary<string, FieldAttributeInfo>();
        private List<FieldInfo> sizeFields = new List<FieldInfo>();
        private List<FieldInfo> colorFields = new List<FieldInfo>();
        bool showDemoWindow = true;
        bool applyThemeToDemoWindow = true;
        bool applyThemeToEditorWindow = true;
        bool applyThemeToEverything = true;
        string saveAsName = "";

        private struct FieldAttributeInfo {
            public float Min { get; } = float.MinValue;
            public float Max { get; } = float.MaxValue;
            public string Category { get; }
            public string Summary { get; }
            public string Format { get; }

            public FieldAttributeInfo(float min, float max, string category, string summary, string format) {
                Min = min;
                Max = max;
                Category = category ?? "Main";
                Summary = summary;
                Format = format ?? "%.0f";
            }

            public static FieldAttributeInfo FromField(FieldInfo fieldInfo) {
                var summary = fieldInfo.GetCustomAttributes(typeof(SummaryAttribute), false).Cast<SummaryAttribute>().ToList().FirstOrDefault()?.Summary;
                var category = fieldInfo.GetCustomAttributes(typeof(CategoryAttribute), false).Cast<CategoryAttribute>().ToList().FirstOrDefault()?.Category;
                var min = fieldInfo.GetCustomAttributes(typeof(MinMaxAttribute), false).Cast<MinMaxAttribute>().ToList().FirstOrDefault()?.MinValue;
                var max = fieldInfo.GetCustomAttributes(typeof(MinMaxAttribute), false).Cast<MinMaxAttribute>().ToList().FirstOrDefault()?.MaxValue;
                var format = fieldInfo.GetCustomAttributes(typeof(FormatAttribute), false).Cast<FormatAttribute>().ToList().FirstOrDefault()?.Format;

                return new FieldAttributeInfo(min.HasValue ? min.Value : float.MinValue, max.HasValue ? max.Value : float.MaxValue, category, summary, format);
            }
        }

        public Hud Hud { get; }

        public UBServiceTheme ThemeDefaults;
        public UBServiceTheme CurrentTheme;
        private string themesDirectory;
        private string currentThemeFile = "Dark";

        public bool HasModifications => !JsonConvert.SerializeObject(ThemeDefaults).Equals(JsonConvert.SerializeObject(CurrentTheme));

        private string[] builtinThemes = new string[] { "Dark", "Light", "Classic" };
        public bool IsBuiltinTheme => builtinThemes.Contains(currentThemeFile.Replace(".json", ""));

        public unsafe ThemeEditor(string themesDirectory) {
            this.themesDirectory = themesDirectory;
            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream("UBService.Resources.icons.theme-editor.png")) {
                Hud = HudManager.CreateHud("Theme Editor", new Bitmap(manifestResourceStream));
                Hud.PreRender += Hud_PreRender;
                Hud.Render += Hud_Render;
                Hud.WindowSettings &= ~ImGuiWindowFlags.AlwaysAutoResize;
                Hud.WindowSettings |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.MenuBar;
            }

            CurrentTheme = new UBServiceTheme();
            ThemeDefaults = CreateDeepCopy(CurrentTheme);

            ImGui.StyleColorsClassic();
            var themeLight = new UBServiceTheme();
            foreach (var e in Enum.GetValues(typeof(ImGuiCol)).Cast<ImGuiCol>()) {
                var col = new Vector4(ImGui.GetStyleColorVec4(e)->W, ImGui.GetStyleColorVec4(e)->X, ImGui.GetStyleColorVec4(e)->Y, ImGui.GetStyleColorVec4(e)->Z);
                var field = themeLight.Colors.GetType().GetField(e.ToString(), BindingFlags.Public | BindingFlags.Instance);
                if (field != null) {
                    field.SetValue(themeLight.Colors, col);
                }
            }
            File.WriteAllText(Path.Combine(themesDirectory, "Classic.json"), JsonConvert.SerializeObject(themeLight, Formatting.Indented));

            OpenTheme("Dark");

            foreach (var field in CurrentTheme.Options.GetType().GetFields()) {
                sizeAttributes.Add(field.Name, FieldAttributeInfo.FromField(field));
                sizeFields.Add(field);
            }

            foreach (var field in CurrentTheme.Colors.GetType().GetFields()) {
                colorAttributes.Add(field.Name, FieldAttributeInfo.FromField(field));
                colorFields.Add(field);
            }
        }

        private void OpenTheme(string v) {
            if (HasModifications) {
                _ = new PopupModal("Unsaved Modifications", $"You have unsaved modifications to the current theme.\n\nDo you want to discard these changes and continue to open {v}?", new Dictionary<string, Action<string>>() {
                    { "Discard Changes", (t) => { CurrentTheme = CreateDeepCopy(ThemeDefaults); OpenTheme(v); } },
                    { "Cancel", (t) => { } }
                });
                return;
            }

            var themeFile = Path.Combine(themesDirectory, $"{v}.json");
            if (File.Exists(themeFile)) {
                currentThemeFile = v;
                CurrentTheme = JsonConvert.DeserializeObject<UBServiceTheme>(File.ReadAllText(themeFile));
                Hud.Title = $"Theme Editor: {currentThemeFile}";
                ThemeDefaults = CreateDeepCopy(CurrentTheme);
            }
        }

        private bool SaveTheme(string name = null) {
            if (name == null && currentThemeFile != null) {
                name = currentThemeFile;
            }

            if (string.IsNullOrEmpty(name)) {
                _ = new PopupModal("Error Saving Theme", "You must specify a name for the theme");
                return false;
            }

            if (!Regex.IsMatch(name, @"[a-z0-9 \-]", RegexOptions.IgnoreCase)) {
                _ = new PopupModal("Error Saving Theme", "Theme names may only contain letters, numbers, dashes, and spaces."); 
                return false;
            }

            var themeFile = Path.Combine(themesDirectory, $"{name}.json");
            if (builtinThemes.Contains(currentThemeFile) && File.Exists(themeFile)) {
                _ = new PopupModal("Error Saving Theme", $"File already exists!\n\n{themeFile}");
                return false;
            }

            File.WriteAllText(themeFile, JsonConvert.SerializeObject(CurrentTheme, Formatting.Indented));
            
            HudManager.Toaster.Add($"Saved theme as: \n{themeFile}", Toaster.ToastType.Success);
            closeMenu = true;
            ThemeDefaults = CreateDeepCopy(CurrentTheme);
            OpenTheme(saveAsName);
            saveAsName = "";
            currentThemeFile = name;
            return true;
        }

        private static T CreateDeepCopy<T>(T obj) {
            if (obj.GetType().IsPrimitive || obj.GetType().IsEnum)
                return obj;

            var settings = new JsonSerializerSettings() {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
                //SerializationBinder = new SettingsBinder(),
                Formatting = Formatting.Indented
            };

            var json = JsonConvert.SerializeObject(obj, settings);
            var copy = JsonConvert.DeserializeObject<T>(json, settings);
            return copy;
        }

        private void Hud_PreRender(object sender, EventArgs e) {
            if (applyThemeToEverything) {
                // reset theme so we dont override settings for DemoWindow/SettingsEditor
                HudManager.CurrentTheme.Apply();
            }

            if (showDemoWindow) {
                if (applyThemeToDemoWindow) {
                    CurrentTheme.Apply();
                    ImGui.ShowDemoWindow(ref showDemoWindow);
                    HudManager.CurrentTheme.Apply();
                }
                else {
                    ImGui.ShowDemoWindow(ref showDemoWindow);
                }
            }

            var viewportCenter = ImGui.GetMainViewport().Pos + (ImGui.GetMainViewport().Size / 2);
            viewportCenter.X -= (ImGui.GetMainViewport().Size.X / 4);
            ImGui.SetNextWindowPos(viewportCenter, ImGuiCond.Once, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSizeConstraints(new Vector2(350, 500), new Vector2(float.MaxValue, float.MaxValue));

            if (HasModifications) {
                Hud.WindowSettings |= ImGuiWindowFlags.UnsavedDocument;
            }
            else {
                Hud.WindowSettings &= ~ImGuiWindowFlags.UnsavedDocument;
            }

            if (applyThemeToEditorWindow) {
                CurrentTheme.Apply();
            }
        }

        private unsafe void Hud_Render(object sender, EventArgs e) {
            RenderStyleEditor(CurrentTheme);
            if (applyThemeToEditorWindow) {
                HudManager.CurrentTheme.Apply();
            }
            if (applyThemeToEverything) {
                CurrentTheme.Apply();
            }
        }

        bool closeMenu = false;
        private void RenderStyleEditor(UBServiceTheme style) {
            var themesList = Directory.GetFiles(themesDirectory, "*.json").Select(p => p.Split('\\').Last()).ToList();
            themesList.Sort();

            if (ImGui.BeginMenuBar()) {
                themesList.Sort();
                if (!closeMenu && ImGui.BeginMenu("File")) {
                    if (ImGui.BeginMenu("Open")) {
                        foreach (var theme in themesList) {
                            if (ImGui.MenuItem(theme.Replace(".json", ""))) {
                                OpenTheme(theme.Replace(".json", ""));
                            }
                        }
                        ImGui.EndMenu();
                    }
                    if (ImGui.MenuItem("Save", HasModifications && !IsBuiltinTheme)) {
                        SaveTheme();
                    }
                    if (ImGui.BeginMenu("Save As", true)) {
                        ImGui.InputText("Name", ref saveAsName, 50);
                        if (ImGui.Button($"Save as {saveAsName}.json##SaveAsButton")) {
                            SaveTheme(saveAsName);
                        }
                        ImGui.EndMenu();
                    }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Export to Clipboard")) {
                        Clipboard.SetText(JsonConvert.SerializeObject(CurrentTheme, Formatting.Indented));
                        HudManager.Toaster.Add("Exported theme to clipboard", Toaster.ToastType.Success);
                    }
                    ImGui.EndMenu();
                }
                else {
                    closeMenu = false;
                }

                if (ImGui.BeginMenu("Options")) {
                    if (ImGui.MenuItem("Show demo window", null, showDemoWindow)) {
                        showDemoWindow = !showDemoWindow;
                    }
                    if (ImGui.BeginMenu("Apply theme to")) {
                        if (ImGui.MenuItem("Demo window", null, applyThemeToDemoWindow)) {
                            applyThemeToDemoWindow = !applyThemeToDemoWindow;
                        }
                        if (ImGui.MenuItem("Editor window", null, applyThemeToEditorWindow)) {
                            applyThemeToEditorWindow = !applyThemeToEditorWindow;
                        }
                        if (ImGui.MenuItem("Everything else", null, applyThemeToEverything)) {
                            applyThemeToEverything = !applyThemeToEverything;
                        }
                        ImGui.EndMenu();
                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
            }

            if (min_widget_width == 0) {
                min_widget_width = ImGui.CalcTextSize("N: MMM\nR: MMM").X;
            }

            ImGui.PushItemWidth(ImGui.GetWindowWidth() * 0.50f);

            if (ImGui.BeginTabBar("##tabs", ImGuiTabBarFlags.None)) {
                if (ImGui.BeginTabItem("Sizes")) {
                    ImGui.BeginChild("##sizes", new Vector2(0, 0), true, ImGuiWindowFlags.AlwaysVerticalScrollbar | ImGuiWindowFlags.NavFlattened);
                    ImGui.InputText("Filter", ref sizeFilter, 1000);

                    ImGui.PushItemWidth(-200);
                    RenderSizeFieldsCategory("Main", style);
                    RenderSizeFieldsCategory("Borders", style);
                    RenderSizeFieldsCategory("Rounding", style);
                    RenderSizeFieldsCategory("Alignment", style);
                    RenderSizeFieldsCategory("Safe Area Padding", style);

                    ImGui.PopItemWidth();
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Colors")) {
                    ImGui.InputText("Filter", ref colorFilter, 1000);

                    if (ImGui.RadioButton("Opaque", alpha_flags == ImGuiColorEditFlags.None)) {
                        alpha_flags = ImGuiColorEditFlags.None;
                    }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("Alpha", alpha_flags == ImGuiColorEditFlags.AlphaPreview)) {
                        alpha_flags = ImGuiColorEditFlags.AlphaPreview;
                    }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("Both", alpha_flags == ImGuiColorEditFlags.AlphaPreviewHalf)) {
                        alpha_flags = ImGuiColorEditFlags.AlphaPreviewHalf;
                    }
                    ImGui.SameLine();
                    HelpMarker("In the color list:\nLeft-click on color square to open color picker,\nRight-click to open edit options menu.");

                    ImGui.BeginChild("##colors", new Vector2(0, 0), true, ImGuiWindowFlags.AlwaysVerticalScrollbar | ImGuiWindowFlags.NavFlattened);
                    ImGui.PushItemWidth(-180);

                    foreach (var colorField in style.Colors.GetType().GetFields()) {
                        if (!string.IsNullOrEmpty(colorFilter) && !colorField.Name.ToLower().Contains(colorFilter.ToLower())) {
                            continue;
                        }
                        var attrs = colorAttributes[colorField.Name];

                        ImGui.PushID(colorField.Name);
                        var value = (Vector4)colorField.GetValue(style.Colors);
                        if (ImGui.ColorEdit4("##color", ref value, ImGuiColorEditFlags.AlphaBar | alpha_flags)) {
                            colorField.SetValue(style.Colors, value);
                        }
                        if (!value.Equals(colorField.GetValue(ThemeDefaults.Colors))) {
                            ImGui.SameLine(0.0f, style.Options.ItemInnerSpacing.X); if (ImGui.Button("Revert")) {
                                colorField.SetValue(style.Colors, colorField.GetValue(ThemeDefaults.Colors));
                            }
                        }
                        ImGui.SameLine(0.0f, style.Options.ItemInnerSpacing.X);
                        ImGui.TextUnformatted(colorField.Name);
                        if (!string.IsNullOrEmpty(attrs.Summary)) {
                            ImGui.SameLine(0.0f, style.Options.ItemInnerSpacing.X);
                            HelpMarker(attrs.Summary);
                        }
                        ImGui.PopID();
                    }
                    ImGui.PopItemWidth();
                    ImGui.EndChild();

                    ImGui.EndTabItem();
                }

                /*
                if (ImGui.BeginTabItem("Fonts")) {
                    ImGuiIO & io = ImGui.GetIO();
                    ImFontAtlas* atlas = io.Fonts;
                    HelpMarker("Read FAQ and docs/FONTS.md for details on font loading.");
                    ImGui.ShowFontAtlas(atlas);

                    // Post-baking font scaling. Note that this is NOT the nice way of scaling fonts, read below.
                    // (we enforce hard clamping manually as by default DragFloat/SliderFloat allows CTRL+Click text to get out of bounds).
                    const float MIN_SCALE = 0.3f;
                    const float MAX_SCALE = 2.0f;
                    HelpMarker(
                        "Those are old settings provided for convenience.\n"
        
                        "However, the _correct_ way of scaling your UI is currently to reload your font at the designed size, "
        
                        "rebuild the font atlas, and call style.ScaleAllSizes() on a reference ImGuiStyle structure.\n"
        
                        "Using those settings here will give you poor quality results.");
                    static float window_scale = 1.0f;
                    ImGui.PushItemWidth(ImGui.GetFontSize() * 8);
                    if (ImGui.DragFloat("window scale", &window_scale, 0.005f, MIN_SCALE, MAX_SCALE, "%.2f", ImGuiSliderFlags_AlwaysClamp)) // Scale only this window
                        ImGui.SetWindowFontScale(window_scale);
                    ImGui.DragFloat("global scale", &io.FontGlobalScale, 0.005f, MIN_SCALE, MAX_SCALE, "%.2f", ImGuiSliderFlags_AlwaysClamp); // Scale everything
                    ImGui.PopItemWidth();

                    ImGui.EndTabItem();
                }
                */

                if (ImGui.BeginTabItem("Rendering")) {
                    //RenderSizeFieldsCategory("Rendering", style);
                    ImGui.Checkbox("Anti-aliased lines", ref style.Options.AntiAliasedLines);
                    if (CurrentTheme.Options.AntiAliasedLines != ThemeDefaults.Options.AntiAliasedLines) {
                        ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                        if (ImGui.Button("Revert##RevertAntiAliasedLines")) {
                            CurrentTheme.Options.AntiAliasedLines = ThemeDefaults.Options.AntiAliasedLines;
                        }
                    }
                    ImGui.SameLine();
                    HelpMarker("When disabling anti-aliasing lines, you'll probably want to disable borders in your style as well.");

                    ImGui.Checkbox("Anti-aliased lines use texture", ref style.Options.AntiAliasedLinesUseTex);
                    if (CurrentTheme.Options.AntiAliasedLinesUseTex != ThemeDefaults.Options.AntiAliasedLinesUseTex) {
                        ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                        if (ImGui.Button("Revert##RevertAntiAliasedUseTexture")) {
                            CurrentTheme.Options.AntiAliasedLinesUseTex = ThemeDefaults.Options.AntiAliasedLinesUseTex;
                        }
                    }
                    ImGui.SameLine();
                    HelpMarker("Faster lines using texture data. Require backend to render with bilinear filtering (not point/nearest filtering).");

                    ImGui.Checkbox("Anti-aliased fill", ref style.Options.AntiAliasedFill);
                    if (CurrentTheme.Options.AntiAliasedFill != ThemeDefaults.Options.AntiAliasedFill) {
                        ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                        if (ImGui.Button("Revert##RevertAntiAliasedFill")) {
                            CurrentTheme.Options.AntiAliasedFill = ThemeDefaults.Options.AntiAliasedFill;
                        }
                    }
                    ImGui.PushItemWidth(ImGui.GetFontSize() * 8);
                    ImGui.DragFloat("##Curve Tessellation Tolerance", ref style.Options.CurveTessellationTol, 0.02f, 0.10f, 10.0f, "%.2f");
                    if (style.Options.CurveTessellationTol < 0.10f) style.Options.CurveTessellationTol = 0.10f;
                    if (CurrentTheme.Options.CurveTessellationTol != ThemeDefaults.Options.CurveTessellationTol) {
                        ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                        if (ImGui.Button("Revert##RevertCuveTessellationTolerance")) {
                            CurrentTheme.Options.CurveTessellationTol = ThemeDefaults.Options.CurveTessellationTol;
                        }
                    }
                    ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                    ImGui.Text("Curve Tessellation Tolerance");

                    // When editing the "Circle Segment Max Error" value, draw a preview of its effect on auto-tessellated circles.
                    ImGui.DragFloat("##Circle Tessellation Max Error", ref style.Options.CircleTessellationMaxError, 0.005f, 0.10f, 5.0f, "%.2f", ImGuiSliderFlags.AlwaysClamp);

                    if (ImGui.IsItemActive()) {
                        ImGui.SetNextWindowPos(ImGui.GetCursorScreenPos());
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("(R = radius, N = number of segments)");
                        ImGui.Spacing();
                        var draw_list = ImGui.GetWindowDrawList();
                        float min_widget_width = ImGui.CalcTextSize("N: MMM\nR: MMM").X;
                        for (int n = 0; n < 8; n++) {
                            const float RAD_MIN = 5.0f;
                            const float RAD_MAX = 70.0f;
                            float rad = RAD_MIN + (RAD_MAX - RAD_MIN) * (float)n / (8.0f - 1.0f);

                            ImGui.BeginGroup();

                            ImGui.Text($"R: {rad}\nN: {draw_list._CalcCircleAutoSegmentCount(rad)}");

                            float canvas_width = Math.Max(min_widget_width, rad * 2.0f);
                            float offset_x = (float)Math.Floor(canvas_width * 0.5f);
                            float offset_y = (float)Math.Floor(RAD_MAX);

                            var p1 = ImGui.GetCursorScreenPos();
                            draw_list.AddCircle(new Vector2(p1.X + offset_x, p1.Y + offset_y), rad, ImGui.GetColorU32(ImGuiCol.Text));
                            ImGui.Dummy(new Vector2(canvas_width, RAD_MAX * 2));

                            /*
                            const ImVec2 p2 = ImGui::GetCursorScreenPos();
                            draw_list->AddCircleFilled(ImVec2(p2.x + offset_x, p2.y + offset_y), rad, ImGui::GetColorU32(ImGuiCol_Text));
                            ImGui::Dummy(ImVec2(canvas_width, RAD_MAX * 2));
                            */

                            ImGui.EndGroup();
                            ImGui.SameLine();
                        }
                        ImGui.EndTooltip();
                    }
                    if (CurrentTheme.Options.CircleTessellationMaxError != ThemeDefaults.Options.CircleTessellationMaxError) {
                        ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                        if (ImGui.Button("Revert##RevertCircleTessellationMaxError")) {
                            CurrentTheme.Options.CircleTessellationMaxError = ThemeDefaults.Options.CircleTessellationMaxError;
                        }
                    }
                    ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                    ImGui.Text("Circle Tessellation Max Error");
                    ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                    HelpMarker("When drawing circle primitives with \"num_segments == 0\" tesselation will be calculated automatically.");

                    ImGui.DragFloat("##Global Alpha", ref style.Options.Alpha, 0.005f, 0.20f, 1.0f, "%.2f");
                    if (CurrentTheme.Options.Alpha != ThemeDefaults.Options.Alpha) {
                        ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                        if (ImGui.Button("Revert##RevertGlobalAlpha")) {
                            CurrentTheme.Options.Alpha = ThemeDefaults.Options.Alpha;
                        }
                    }
                    ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                    ImGui.Text("Global Alpha");
                    ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X); 
                    HelpMarker("Application wide alpha transparency");

                    ImGui.DragFloat("##Disabled Alpha", ref style.Options.DisabledAlpha, 0.005f, 0.0f, 1.0f, "%.2f");
                    if (CurrentTheme.Options.DisabledAlpha != ThemeDefaults.Options.DisabledAlpha) {
                        ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                        if (ImGui.Button("Revert##RevertDisabledAlpha")) {
                            CurrentTheme.Options.DisabledAlpha = ThemeDefaults.Options.DisabledAlpha;
                        }
                    }
                    ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                    ImGui.Text("Disabled Alpha");
                    ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                    HelpMarker("Additional alpha multiplier for disabled items (multiply over current value of Alpha).");
                    ImGui.PopItemWidth();

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Description")) {
                    ImGui.InputText("##AuthorInput", ref CurrentTheme.Author, 100);
                    if (CurrentTheme.Author != ThemeDefaults.Author) {
                        ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                        if (ImGui.Button("Revert##RevertAuthor")) {
                            CurrentTheme.Author = ThemeDefaults.Author;
                        }
                    }
                    ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                    ImGui.Text("Author");


                    ImGui.Text("Description:");
                    ImGui.InputTextMultiline("##DescriptionInput", ref CurrentTheme.Description, 1000, new Vector2(ImGui.GetContentRegionAvail().X, 150));
                    if (CurrentTheme.Description != ThemeDefaults.Description) {
                        ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                        if (ImGui.Button("Revert##RevertDescription")) {
                            CurrentTheme.Description = ThemeDefaults.Description;
                        }
                    }
                }

                ImGui.EndTabBar();
            }

            ImGui.PopItemWidth();
        }

        private void RenderSizeFieldsCategory(string category, UBServiceTheme theme) {
            try {
                var fields = sizeFields.Where(f => sizeAttributes[f.Name].Category.ToLower() == category.ToLower());
                var shouldShow = string.IsNullOrEmpty(sizeFilter) ? true : fields.Any(f => f.Name.ToLower().Contains(sizeFilter));

                if (!shouldShow)
                    return;

                ImGui.Text(category);

                foreach (var field in fields) {
                    if (!string.IsNullOrEmpty(sizeFilter) && !field.Name.ToLower().Contains(sizeFilter.ToLower()))
                        continue;

                    var fieldValue = field.GetValue(theme.Options);
                    if (InputSize(field, ref fieldValue)) {
                        field.SetValue(theme.Options, fieldValue);
                    }
                }
            }
            catch (Exception ex) { UBService.LogException(ex); }
        }

        private bool InputSize(FieldInfo field, ref object value) {
            var attrs = sizeAttributes[field.Name];
            var ret = false;

            ImGui.PushID(field.Name);

            if (field.FieldType == typeof(Vector2)) {
                var refValue = (Vector2)value;
                if (ImGui.SliderFloat2($"##{field.Name}", ref refValue, attrs.Min, attrs.Max, attrs.Format)) {
                    value = refValue;
                    ret = true;
                }
            }
            else if (field.FieldType == typeof(float)) {
                var refValue = (float)value;
                if (ImGui.SliderFloat($"##{field.Name}", ref refValue, attrs.Min, attrs.Max, attrs.Format)) {
                    value = refValue;
                    ret = true;
                }
            }
            else if (field.FieldType.IsEnum) {
                var refValue = (int)value;
                var opts = string.Join("", Enum.GetValues(field.FieldType).Cast<object>().Select(f => f.ToString() + "\0").ToArray());
                if (ImGui.Combo($"##{field.Name}", ref refValue, opts)) {
                    value = refValue;
                    ret = true;
                }
            }
            else if (field.FieldType == typeof(bool)) {
                var refValue = (bool)value;
                if (ImGui.Checkbox($"##{field.Name}", ref refValue)) {
                    value = refValue;
                    ret = true;
                }
            }
            else {
                ImGui.Text($"nothing for{field.FieldType}");
            }
            var current = CurrentTheme.Options.GetType().GetField(field.Name).GetValue(CurrentTheme.Options);
            var defaults = ThemeDefaults.Options.GetType().GetField(field.Name).GetValue(ThemeDefaults.Options);
            if (!current.Equals(defaults)) {
                ImGui.SameLine(0.0f, CurrentTheme.Options.ItemInnerSpacing.X);
                if (ImGui.Button("Revert")) {
                    field.SetValue(CurrentTheme.Options, field.GetValue(ThemeDefaults.Options));
                }
            }

            ImGui.SameLine(0, CurrentTheme.Options.ItemInnerSpacing.X);
            ImGui.Text(field.Name);

            if (!string.IsNullOrEmpty(attrs.Summary)) {
                ImGui.SameLine(0, CurrentTheme.Options.ItemInnerSpacing.X);
                HelpMarker(attrs.Summary);
            }

            ImGui.PopID();

            return ret;
        }

        // Helper to display a little (?) mark which shows a tooltip when hovered.
        // In your own code you may want to display an actual icon if you are using a merged icon fonts (see docs/FONTS.md)
        static void HelpMarker(string desc) {
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(desc);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

        public void Dispose() {
            Hud.PreRender -= Hud_PreRender;
            Hud.Render -= Hud_Render;
            Hud?.Dispose();
        }
    }
}
