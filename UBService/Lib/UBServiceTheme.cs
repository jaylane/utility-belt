using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UBService.Lib.Settings;

namespace UBService.Lib {
    public class ThemeColors {
        [Summary("Success color")]
        public Vector4 Success = new Vector4(0.00f, 1.00f, 0.00f, 1.00f);

        [Summary("Warning color")]
        public Vector4 Warning = new Vector4(1.00f, 1.00f, 0.00f, 1.00f);

        [Summary("Error color")]
        public Vector4 Error = new Vector4(1.00f, 0.00f, 0.00f, 1.00f);

        [Summary("Text color")]
        public Vector4 Text = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);

        [Summary("Disabled text color")]
        public Vector4 TextDisabled = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);

        [Summary("Inactive icon button color")]
        public Vector4 IconButton = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);

        [Summary("Active icon button color")]
        public Vector4 IconButtonActive = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);

        [Summary("Hovered icon button color")]
        public Vector4 IconButtonHovered = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);

        [Summary("Window background color")]
        public Vector4 WindowBg = new Vector4(0.06f, 0.06f, 0.06f, 0.94f);

        [Summary("Child background color")]
        public Vector4 ChildBg = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);

        [Summary("Popup background color")]
        public Vector4 PopupBg = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);

        [Summary("Border color")]
        public Vector4 Border = new Vector4(0.43f, 0.43f, 0.50f, 0.50f);

        [Summary("Border shadow color")]
        public Vector4 BorderShadow = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);

        [Summary("Inactive Frame background color")]
        public Vector4 FrameBg = new Vector4(0.16f, 0.29f, 0.48f, 0.54f);

        [Summary("Hovered frame background color")]
        public Vector4 FrameBgHovered = new Vector4(0.26f, 0.59f, 0.98f, 0.40f);

        [Summary("Active frame background color")]
        public Vector4 FrameBgActive = new Vector4(0.26f, 0.59f, 0.98f, 0.67f);

        [Summary("Inactive window title bar background color")]
        public Vector4 TitleBg = new Vector4(0.04f, 0.04f, 0.04f, 1.00f);

        [Summary("Active window title bar background color")]
        public Vector4 TitleBgActive = new Vector4(0.16f, 0.29f, 0.48f, 1.00f);

        [Summary("Collapsed window title bar background color")]
        public Vector4 TitleBgCollapsed = new Vector4(0.00f, 0.00f, 0.00f, 0.51f);

        [Summary("Menu bar background color")]
        public Vector4 MenuBarBg = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);

        [Summary("Scroll bar background color")]
        public Vector4 ScrollbarBg = new Vector4(0.02f, 0.02f, 0.02f, 0.53f);

        [Summary("Inactive scroll bar grabber color")]
        public Vector4 ScrollbarGrab = new Vector4(0.31f, 0.31f, 0.31f, 1.00f);

        [Summary("Hovered scroll bar grabber color")]
        public Vector4 ScrollbarGrabHovered = new Vector4(0.41f, 0.41f, 0.41f, 1.00f);

        [Summary("Active scroll bar grabber color")]
        public Vector4 ScrollbarGrabActive = new Vector4(0.51f, 0.51f, 0.51f, 1.00f);

        [Summary("Check mark color")]
        public Vector4 CheckMark = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);

        [Summary("Inactive slider grabber color")]
        public Vector4 SliderGrab = new Vector4(0.24f, 0.52f, 0.88f, 1.00f);

        [Summary("Active slider grabber color")]
        public Vector4 SliderGrabActive = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);

        [Summary("Button background color")]
        public Vector4 Button = new Vector4(0.26f, 0.59f, 0.98f, 0.40f);

        [Summary("Hovered button background color")]
        public Vector4 ButtonHovered = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);

        [Summary("Active button background color")]
        public Vector4 ButtonActive = new Vector4(0.06f, 0.53f, 0.98f, 1.00f);

        [Summary("Header background color")]
        public Vector4 Header = new Vector4(0.26f, 0.59f, 0.98f, 0.31f);

        [Summary("Hovered header background color")]
        public Vector4 HeaderHovered = new Vector4(0.26f, 0.59f, 0.98f, 0.80f);

        [Summary("Active header background color")]
        public Vector4 HeaderActive = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);

        [Summary("Separator color")]
        public Vector4 Separator = new Vector4(0.43f, 0.43f, 0.50f, 0.50f);

        [Summary("Hovered separator color")]
        public Vector4 SeparatorHovered = new Vector4(0.10f, 0.40f, 0.75f, 0.78f);

        [Summary("Active separator color")]
        public Vector4 SeparatorActive = new Vector4(0.10f, 0.40f, 0.75f, 1.00f);

        [Summary("Resize gripper color")]
        public Vector4 ResizeGrip = new Vector4(0.26f, 0.59f, 0.98f, 0.20f);

        [Summary("Hovered resize gripper color")]
        public Vector4 ResizeGripHovered = new Vector4(0.26f, 0.59f, 0.98f, 0.67f);

        [Summary("Active resize gripper color")]
        public Vector4 ResizeGripActive = new Vector4(0.26f, 0.59f, 0.98f, 0.95f);

        [Summary("Tab background color")]
        public Vector4 Tab = new Vector4(0.180f, 0.350f, 0.580f, 0.862f);

        [Summary("Hovered tab background color")]
        public Vector4 TabHovered = new Vector4(0.260f, 0.590f, 0.980f, 0.800f);

        [Summary("Active tab background color")]
        public Vector4 TabActive = new Vector4(0.200f, 0.410f, 0.680f, 1.000f);

        [Summary("Unfocused tab background color")]
        public Vector4 TabUnfocused = new Vector4(0.068f, 0.102f, 0.148f, 0.972f);

        [Summary("Active unfocused tab background color")]
        public Vector4 TabUnfocusedActive = new Vector4(0.136f, 0.262f, 0.424f, 1.000f);

        [Summary("Docking preview color")]
        public Vector4 DockingPreview = new Vector4(0.260f, 0.590f, 0.980f, 0.700f);

        [Summary("Docking empty background color")]
        public Vector4 DockingEmptyBg = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);

        [Summary("Plot line color")]
        public Vector4 PlotLines = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);

        [Summary("Hovered plot line color")]
        public Vector4 PlotLinesHovered = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);

        [Summary("Plot histogram color")]
        public Vector4 PlotHistogram = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);

        [Summary("Hovered plot histogram color")]
        public Vector4 PlotHistogramHovered = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);

        [Summary("Table header background color")]
        public Vector4 TableHeaderBg = new Vector4(0.19f, 0.19f, 0.20f, 1.00f);

        [Summary("Strong table border color. Prefer using Alpha=1.0 here")]
        public Vector4 TableBorderStrong = new Vector4(0.31f, 0.31f, 0.35f, 1.00f);

        [Summary("Light table border color. Prefer using Alpha=1.0 here")]
        public Vector4 TableBorderLight = new Vector4(0.23f, 0.23f, 0.25f, 1.00f);

        [Summary("Table row background color")]
        public Vector4 TableRowBg = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);

        [Summary("Alternate table row background color")]
        public Vector4 TableRowBgAlt = new Vector4(1.00f, 1.00f, 1.00f, 0.06f);

        [Summary("Selected text background color")]
        public Vector4 TextSelectedBg = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);

        [Summary("Drag/Drop target color")]
        public Vector4 DragDropTarget = new Vector4(1.00f, 1.00f, 0.00f, 0.90f);

        [Summary("Nav highlight color")]
        public Vector4 NavHighlight = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);

        [Summary("Nav window highlight color")]
        public Vector4 NavWindowingHighlight = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);

        [Summary("Nav window dim background color")]
        public Vector4 NavWindowingDimBg = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);

        [Summary("Modal window dim background color")]
        public Vector4 ModalWindowDimBg = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);
    }

    public class ThemeSizes {
        [Summary("Global alpha applies to everything")]
        [Category("Rendering")]
        [MinMax(0.0f, 1.0f)]
        public float Alpha = 1.0f;

        [Summary("Additional alpha multiplier applied by BeginDisabled(). Multiply over current value of Alpha.")]
        [Category("Rendering")]
        [MinMax(0.0f, 1.0f)]
        public float DisabledAlpha = 0.60f;

        [Summary("Padding within a window")]
        [Category("Main")]
        [MinMax(0.0f, 20.0f)]
        public Vector2 WindowPadding = new Vector2(8, 8);

        [Summary("Radius of window corners rounding. Set to 0.0f to have rectangular windows. Large values tend to lead to variety of artifacts and are not recommended.")]
        [Category("Rounding")]
        [MinMax(0.0f, 12.0f)]
        public float WindowRounding = 0.0f;

        [Summary("Thickness of border around windows. Generally set to 0.0f or 1.0f. Other values not well tested.")]
        [Category("Borders")]
        [MinMax(0.0f, 5.0f)]
        public float WindowBorderSize = 1.0f;

        [Summary("Minimum window size")]
        [Category("Main")]
        [MinMax(5.0f, 500.0f)]
        public Vector2 WindowMinSize = new Vector2(32, 32);

        [Summary("Alignment for title bar text")]
        [Category("Alignment")]
        [MinMax(0.0f, 1.0f)]
        [Format("%.2f")]
        public Vector2 WindowTitleAlign = new Vector2(0.0f, 0.5f);

        [Summary("Position of the collapsing/docking button in the title bar (left/right). Defaults to ImGuiDir.Left.")]
        [Category("Alignment")]
        public ImGuiDir WindowMenuButtonPosition = ImGuiDir.Left;

        [Summary("Radius of child window corners rounding. Set to 0.0f to have rectangular child windows")]
        [Category("Rounding")]
        [MinMax(0.0f, 12.0f)]
        public float ChildRounding = 0.0f;

        [Summary("Thickness of border around child windows. Generally set to 0.0f or 1.0f. Other values not well tested.")]
        [Category("Borders")]
        [MinMax(0.0f, 5.0f)]
        public float ChildBorderSize = 1.0f;

        [Summary("Radius of popup window corners rounding. Set to 0.0f to have rectangular child windows")]
        [Category("Rounding")]
        [MinMax(0.0f, 12.0f)]
        public float PopupRounding = 0.0f;

        [Summary("Thickness of border around popup or tooltip windows. Generally set to 0.0f or 1.0f. Other values not well tested.")]
        [Category("Borders")]
        [MinMax(0.0f, 5.0f)]
        public float PopupBorderSize = 1.0f;

        [Summary("Padding within a framed rectangle (used by most widgets)")]
        [Category("Main")]
        [MinMax(0.0f, 20.0f)]
        public Vector2 FramePadding = new Vector2(4, 3);

        [Summary("Radius of frame corners rounding. Set to 0.0f to have rectangular frames (used by most widgets).")]
        [Category("Rounding")]
        [MinMax(0.0f, 12.0f)]
        public float FrameRounding = 0.0f;

        [Summary("Thickness of border around frames. Generally set to 0.0f or 1.0f. Other values not well tested.")]
        [Category("Borders")]
        [MinMax(0.0f, 5.0f)]
        public float FrameBorderSize = 0.0f;

        [Summary("Horizontal and vertical spacing between widgets/lines")]
        [Category("Main")]
        [MinMax(0.0f, 20.0f)]
        public Vector2 ItemSpacing = new Vector2(8, 4);

        [Summary("Horizontal and vertical spacing between within elements of a composed widget (e.g. a slider and its label)")]
        [Category("Main")]
        [MinMax(0.0f, 20.0f)]
        public Vector2 ItemInnerSpacing = new Vector2(4, 4);

        [Summary("Padding within a table cell")]
        [Category("Main")]
        [MinMax(0.0f, 20.0f)]
        public Vector2 CellPadding = new Vector2(4, 2);

        //[Summary("Expand reactive bounding box for touch-based system where touch position is not accurate enough. Unfortunately we don't sort widgets so priority on overlap will always be given to the first widget. So don't grow this too much!")]
        //public Vector2 TouchExtraPadding = new Vector2(0, 0);

        [Summary("Horizontal spacing when e.g. entering a tree node. Generally == (FontSize + FramePadding.x*2).")]
        [Category("Main")]
        [MinMax(0.0f, 30.0f)]
        public float IndentSpacing = 21.0f;

        [Summary("Minimum horizontal spacing between two columns. Preferably > (FramePadding.x + 1).")]
        [Category("Main")]
        [MinMax(0.0f, 21.0f)]
        public float ColumnsMinSpacing = 6.0f;

        [Summary("Width of the vertical scrollbar, Height of the horizontal scrollbar")]
        [Category("Main")]
        [MinMax(1.0f, 20.0f)]
        public float ScrollbarSize = 14.0f;

        [Summary("Radius of grab corners rounding for scrollbar")]
        [Category("Rounding")]
        [MinMax(0.0f, 12.0f)]
        public float ScrollbarRounding = 9.0f;

        [Summary("Minimum width/height of a grab box for slider/scrollbar")]
        [Category("Main")]
        [MinMax(1.0f, 20.0f)]
        public float GrabMinSize = 10.0f;

        [Summary("Radius of grabs corners rounding. Set to 0.0f to have rectangular slider grabs.")]
        [Category("Rounding")]
        [MinMax(0.0f, 12.0f)]
        public float GrabRounding = 0.0f;

        [Summary("The size in pixels of the dead-zone around zero on logarithmic sliders that cross zero.")]
        [Category("Rounding")]
        [MinMax(0.0f, 12.0f)]
        public float LogSliderDeadzone = 4.0f;

        [Summary("Radius of upper corners of a tab. Set to 0.0f to have rectangular tabs.")]
        [Category("Rounding")]
        [MinMax(0.0f, 12.0f)]
        public float TabRounding = 4.0f;

        [Summary("Thickness of border around tabs.")]
        [Category("Borders")]
        [MinMax(0.0f, 5.0f)]
        public float TabBorderSize = 0.0f;

        [Summary("Minimum width for close button to appears on an unselected tab when hovered. Set to 0.0f to always show when hovering, set to FLT_MAX to never show close button unless selected.")]
        [Category("Main")]
        [MinMax(0.0f, float.MaxValue)]
        public float TabMinWidthForCloseButton = 0.0f;

        [Summary("Side of the color button in the ColorEdit4 widget (left/right). Defaults to ImGuiDir_Right.")]
        [Category("Alignment")]
        public ImGuiDir ColorButtonPosition = ImGuiDir.Right;

        [Summary("Alignment of button text when button is larger than text.")]
        [Category("Alignment")]
        [Format("%.2f")]
        [MinMax(0.0f, 1.0f)]
        public Vector2 ButtonTextAlign = new Vector2(0.5f, 0.5f);

        [Summary("Alignment of selectable text. Defaults to (0.0f, 0.0f) (top-left aligned). It's generally important to keep this left-aligned if you want to lay multiple items on a same line.")]
        [Category("Alignment")]
        [Format("%.2f")]
        [MinMax(0.0f, 1.0f)]
        public Vector2 SelectableTextAlign = new Vector2(0.0f, 0.0f);

        [Summary("Window position are clamped to be visible within the display area or monitors by at least this amount. Only applies to regular windows.")]
        [Category("Safe Area Padding")]
        [MinMax(0.0f, 100.0f)]
        public Vector2 DisplayWindowPadding = new Vector2(19, 19);

        [Summary("If you cannot see the edge of your screen (e.g. on a TV) increase the safe area padding. Covers popups/tooltips as well regular windows.")]
        [Category("Safe Area Padding")]
        [MinMax(0.0f, 100.0f)]
        public Vector2 DisplaySafeAreaPadding = new Vector2(3, 3);

        //[Summary("Scale software rendered mouse cursor (when io.MouseDrawCursor is enabled). May be removed later.")]
        //public float MouseCursorScale = 1.0f;

        [Summary("Enable anti-aliased lines/borders. Disable if you are really tight on CPU/GPU.")]
        [Category("Rendering")]
        public bool AntiAliasedLines = true;

        [Summary("Enable anti-aliased lines/borders using textures where possible. Require backend to render with bilinear filtering.")]
        [Category("Rendering")]
        public bool AntiAliasedLinesUseTex = true;

        [Summary("Enable anti-aliased filled shapes (rounded rectangles, circles, etc.).")]
        [Category("Rendering")]
        public bool AntiAliasedFill = true;

        [Summary("Tessellation tolerance when using PathBezierCurveTo() without a specific number of segments. Decrease for highly tessellated curves (higher quality, more polygons), increase to reduce quality.")]
        [Category("Rendering")]
        public float CurveTessellationTol = 1.25f;

        [Summary("Maximum error (in pixels) allowed when using AddCircle()/AddCircleFilled() or drawing rounded corner rectangles with no explicit segment count specified. Decrease for higher quality but more geometry.\r\n")]
        [Category("Rendering")]
        public float CircleTessellationMaxError = 0.30f;
    }

    public class UBServiceTheme {
        public string Author = "";
        public string Description = "";

        public ThemeColors Colors = new ThemeColors();
        public ThemeSizes Options = new ThemeSizes();

        public UBServiceTheme() {

        }

        public unsafe void Apply() {
            var style = ImGui.GetStyle();

            style.Alpha = Options.Alpha;
            style.Alpha = Options.Alpha;
            style.DisabledAlpha = Options.DisabledAlpha;
            style.WindowPadding = Options.WindowPadding;
            style.WindowRounding = Options.WindowRounding;
            style.WindowBorderSize = Options.WindowBorderSize;
            style.WindowMinSize = Options.WindowMinSize;
            style.WindowTitleAlign = Options.WindowTitleAlign;
            style.WindowMenuButtonPosition = Options.WindowMenuButtonPosition;
            style.ChildRounding = Options.ChildRounding;
            style.ChildBorderSize = Options.ChildBorderSize;
            style.PopupRounding = Options.PopupRounding;
            style.PopupBorderSize = Options.PopupBorderSize;
            style.FramePadding = Options.FramePadding;
            style.FrameRounding = Options.FrameRounding;
            style.FrameBorderSize = Options.FrameBorderSize;
            style.ItemSpacing = Options.ItemSpacing;
            style.ItemInnerSpacing = Options.ItemInnerSpacing;
            style.CellPadding = Options.CellPadding;
            //style.TouchExtraPadding = Options.TouchExtraPadding;
            style.IndentSpacing = Options.IndentSpacing;
            style.ColumnsMinSpacing = Options.ColumnsMinSpacing;
            style.ScrollbarSize = Options.ScrollbarSize;
            style.ScrollbarRounding = Options.ScrollbarRounding;
            style.GrabMinSize = Options.GrabMinSize;
            style.GrabRounding = Options.GrabRounding;
            style.LogSliderDeadzone = Options.LogSliderDeadzone;
            style.TabRounding = Options.TabRounding;
            style.TabBorderSize = Options.TabBorderSize;
            style.TabMinWidthForCloseButton = Options.TabMinWidthForCloseButton;
            style.ColorButtonPosition = Options.ColorButtonPosition;
            style.ButtonTextAlign = Options.ButtonTextAlign;
            style.SelectableTextAlign = Options.SelectableTextAlign;
            style.DisplayWindowPadding = Options.DisplayWindowPadding;
            style.DisplaySafeAreaPadding = Options.DisplaySafeAreaPadding;
            //style.MouseCursorScale = Options.MouseCursorScale;
            style.AntiAliasedLines = Options.AntiAliasedLines ? (byte)1 : (byte)0;
            style.AntiAliasedLinesUseTex = Options.AntiAliasedLinesUseTex ? (byte)1 : (byte)0;
            style.AntiAliasedFill = Options.AntiAliasedFill ? (byte)1 : (byte)0;
            style.CurveTessellationTol = Options.CurveTessellationTol;
            style.CircleTessellationMaxError = Options.CircleTessellationMaxError;

            style.NativePtr->Colors_0 = Colors.Text;
            style.NativePtr->Colors_1 = Colors.TextDisabled;
            style.NativePtr->Colors_2 = Colors.WindowBg;
            style.NativePtr->Colors_3 = Colors.ChildBg;
            style.NativePtr->Colors_4 = Colors.PopupBg;
            style.NativePtr->Colors_5 = Colors.Border;
            style.NativePtr->Colors_6 = Colors.BorderShadow;
            style.NativePtr->Colors_7 = Colors.FrameBg;
            style.NativePtr->Colors_8 = Colors.FrameBgHovered;
            style.NativePtr->Colors_9 = Colors.FrameBgActive;
            style.NativePtr->Colors_10 = Colors.TitleBg;
            style.NativePtr->Colors_11 = Colors.TitleBgActive;
            style.NativePtr->Colors_12 = Colors.TitleBgCollapsed;
            style.NativePtr->Colors_13 = Colors.MenuBarBg;
            style.NativePtr->Colors_14 = Colors.ScrollbarBg;
            style.NativePtr->Colors_15 = Colors.ScrollbarGrab;
            style.NativePtr->Colors_16 = Colors.ScrollbarGrabHovered;
            style.NativePtr->Colors_17 = Colors.ScrollbarGrabActive;
            style.NativePtr->Colors_18 = Colors.CheckMark;
            style.NativePtr->Colors_19 = Colors.SliderGrab;
            style.NativePtr->Colors_20 = Colors.SliderGrabActive;
            style.NativePtr->Colors_21 = Colors.Button;
            style.NativePtr->Colors_22 = Colors.ButtonHovered;
            style.NativePtr->Colors_23 = Colors.ButtonActive;
            style.NativePtr->Colors_24 = Colors.Header;
            style.NativePtr->Colors_25 = Colors.HeaderHovered;
            style.NativePtr->Colors_26 = Colors.HeaderActive;
            style.NativePtr->Colors_27 = Colors.Separator;
            style.NativePtr->Colors_28 = Colors.SeparatorHovered;
            style.NativePtr->Colors_29 = Colors.SeparatorActive;
            style.NativePtr->Colors_30 = Colors.ResizeGrip;
            style.NativePtr->Colors_31 = Colors.ResizeGripHovered;
            style.NativePtr->Colors_32 = Colors.ResizeGripActive;
            style.NativePtr->Colors_33 = Colors.Tab;
            style.NativePtr->Colors_34 = Colors.TabHovered;
            style.NativePtr->Colors_35 = Colors.TabActive;
            style.NativePtr->Colors_36 = Colors.TabUnfocused;
            style.NativePtr->Colors_37 = Colors.TabUnfocusedActive;
            style.NativePtr->Colors_38 = Colors.DockingPreview;
            style.NativePtr->Colors_39 = Colors.DockingEmptyBg;
            style.NativePtr->Colors_40 = Colors.PlotLines;
            style.NativePtr->Colors_41 = Colors.PlotLinesHovered;
            style.NativePtr->Colors_42 = Colors.PlotHistogram;
            style.NativePtr->Colors_43 = Colors.PlotHistogramHovered;
            style.NativePtr->Colors_44 = Colors.TableHeaderBg;
            style.NativePtr->Colors_45 = Colors.TableBorderStrong;
            style.NativePtr->Colors_46 = Colors.TableBorderLight;
            style.NativePtr->Colors_47 = Colors.TableRowBg;
            style.NativePtr->Colors_48 = Colors.TableRowBgAlt;
            style.NativePtr->Colors_49 = Colors.TextSelectedBg;
            style.NativePtr->Colors_50 = Colors.DragDropTarget;
            style.NativePtr->Colors_51 = Colors.NavHighlight;
            style.NativePtr->Colors_52 = Colors.NavWindowingHighlight;
            style.NativePtr->Colors_53 = Colors.NavWindowingDimBg;
            style.NativePtr->Colors_54 = Colors.ModalWindowDimBg;
        }
    }
}
