using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe partial struct ImGuiStyle
    {
        public float Alpha;
        public float DisabledAlpha;
        public Vector2 WindowPadding;
        public float WindowRounding;
        public float WindowBorderSize;
        public Vector2 WindowMinSize;
        public Vector2 WindowTitleAlign;
        public ImGuiDir WindowMenuButtonPosition;
        public float ChildRounding;
        public float ChildBorderSize;
        public float PopupRounding;
        public float PopupBorderSize;
        public Vector2 FramePadding;
        public float FrameRounding;
        public float FrameBorderSize;
        public Vector2 ItemSpacing;
        public Vector2 ItemInnerSpacing;
        public Vector2 CellPadding;
        public Vector2 TouchExtraPadding;
        public float IndentSpacing;
        public float ColumnsMinSpacing;
        public float ScrollbarSize;
        public float ScrollbarRounding;
        public float GrabMinSize;
        public float GrabRounding;
        public float LogSliderDeadzone;
        public float TabRounding;
        public float TabBorderSize;
        public float TabMinWidthForCloseButton;
        public ImGuiDir ColorButtonPosition;
        public Vector2 ButtonTextAlign;
        public Vector2 SelectableTextAlign;
        public Vector2 DisplayWindowPadding;
        public Vector2 DisplaySafeAreaPadding;
        public float MouseCursorScale;
        public byte AntiAliasedLines;
        public byte AntiAliasedLinesUseTex;
        public byte AntiAliasedFill;
        public float CurveTessellationTol;
        public float CircleTessellationMaxError;
        public Vector4 Colors_0;
        public Vector4 Colors_1;
        public Vector4 Colors_2;
        public Vector4 Colors_3;
        public Vector4 Colors_4;
        public Vector4 Colors_5;
        public Vector4 Colors_6;
        public Vector4 Colors_7;
        public Vector4 Colors_8;
        public Vector4 Colors_9;
        public Vector4 Colors_10;
        public Vector4 Colors_11;
        public Vector4 Colors_12;
        public Vector4 Colors_13;
        public Vector4 Colors_14;
        public Vector4 Colors_15;
        public Vector4 Colors_16;
        public Vector4 Colors_17;
        public Vector4 Colors_18;
        public Vector4 Colors_19;
        public Vector4 Colors_20;
        public Vector4 Colors_21;
        public Vector4 Colors_22;
        public Vector4 Colors_23;
        public Vector4 Colors_24;
        public Vector4 Colors_25;
        public Vector4 Colors_26;
        public Vector4 Colors_27;
        public Vector4 Colors_28;
        public Vector4 Colors_29;
        public Vector4 Colors_30;
        public Vector4 Colors_31;
        public Vector4 Colors_32;
        public Vector4 Colors_33;
        public Vector4 Colors_34;
        public Vector4 Colors_35;
        public Vector4 Colors_36;
        public Vector4 Colors_37;
        public Vector4 Colors_38;
        public Vector4 Colors_39;
        public Vector4 Colors_40;
        public Vector4 Colors_41;
        public Vector4 Colors_42;
        public Vector4 Colors_43;
        public Vector4 Colors_44;
        public Vector4 Colors_45;
        public Vector4 Colors_46;
        public Vector4 Colors_47;
        public Vector4 Colors_48;
        public Vector4 Colors_49;
        public Vector4 Colors_50;
        public Vector4 Colors_51;
        public Vector4 Colors_52;
        public Vector4 Colors_53;
        public Vector4 Colors_54;
    }
    public unsafe partial struct ImGuiStylePtr
    {
        public ImGuiStyle* NativePtr { get; }
        public ImGuiStylePtr(ImGuiStyle* nativePtr) => NativePtr = nativePtr;
        public ImGuiStylePtr(IntPtr nativePtr) => NativePtr = (ImGuiStyle*)nativePtr;
        public static implicit operator ImGuiStylePtr(ImGuiStyle* nativePtr) => new ImGuiStylePtr(nativePtr);
        public static implicit operator ImGuiStyle* (ImGuiStylePtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImGuiStylePtr(IntPtr nativePtr) => new ImGuiStylePtr(nativePtr);
        public ref float Alpha => ref NativePtr->Alpha;
        public ref float DisabledAlpha => ref NativePtr->DisabledAlpha;
        public ref Vector2 WindowPadding => ref NativePtr->WindowPadding;
        public ref float WindowRounding => ref NativePtr->WindowRounding;
        public ref float WindowBorderSize => ref NativePtr->WindowBorderSize;
        public ref Vector2 WindowMinSize => ref NativePtr->WindowMinSize;
        public ref Vector2 WindowTitleAlign => ref NativePtr->WindowTitleAlign;
        public ref ImGuiDir WindowMenuButtonPosition => ref NativePtr->WindowMenuButtonPosition;
        public ref float ChildRounding => ref NativePtr->ChildRounding;
        public ref float ChildBorderSize => ref NativePtr->ChildBorderSize;
        public ref float PopupRounding => ref NativePtr->PopupRounding;
        public ref float PopupBorderSize => ref NativePtr->PopupBorderSize;
        public ref Vector2 FramePadding => ref NativePtr->FramePadding;
        public ref float FrameRounding => ref NativePtr->FrameRounding;
        public ref float FrameBorderSize => ref NativePtr->FrameBorderSize;
        public ref Vector2 ItemSpacing => ref NativePtr->ItemSpacing;
        public ref Vector2 ItemInnerSpacing => ref NativePtr->ItemInnerSpacing;
        public ref Vector2 CellPadding => ref NativePtr->CellPadding;
        public ref Vector2 TouchExtraPadding => ref NativePtr->TouchExtraPadding;
        public ref float IndentSpacing => ref NativePtr->IndentSpacing;
        public ref float ColumnsMinSpacing => ref NativePtr->ColumnsMinSpacing;
        public ref float ScrollbarSize => ref NativePtr->ScrollbarSize;
        public ref float ScrollbarRounding => ref NativePtr->ScrollbarRounding;
        public ref float GrabMinSize => ref NativePtr->GrabMinSize;
        public ref float GrabRounding => ref NativePtr->GrabRounding;
        public ref float LogSliderDeadzone => ref NativePtr->LogSliderDeadzone;
        public ref float TabRounding => ref NativePtr->TabRounding;
        public ref float TabBorderSize => ref NativePtr->TabBorderSize;
        public ref float TabMinWidthForCloseButton => ref NativePtr->TabMinWidthForCloseButton;
        public ref ImGuiDir ColorButtonPosition => ref NativePtr->ColorButtonPosition;
        public ref Vector2 ButtonTextAlign => ref NativePtr->ButtonTextAlign;
        public ref Vector2 SelectableTextAlign => ref NativePtr->SelectableTextAlign;
        public ref Vector2 DisplayWindowPadding => ref NativePtr->DisplayWindowPadding;
        public ref Vector2 DisplaySafeAreaPadding => ref NativePtr->DisplaySafeAreaPadding;
        public ref float MouseCursorScale => ref NativePtr->MouseCursorScale;
        public ref byte AntiAliasedLines => ref NativePtr->AntiAliasedLines;
        public ref byte AntiAliasedLinesUseTex => ref NativePtr->AntiAliasedLinesUseTex;
        public ref byte AntiAliasedFill => ref NativePtr->AntiAliasedFill;
        public ref float CurveTessellationTol => ref NativePtr->CurveTessellationTol;
        public ref float CircleTessellationMaxError => ref NativePtr->CircleTessellationMaxError;
        public RangeAccessor<Vector4> Colors => new RangeAccessor<Vector4>(&NativePtr->Colors_0, 55);
        public void Destroy()
        {
            ImGuiNative.ImGuiStyle_destroy((ImGuiStyle*)(NativePtr));
        }
        public void ScaleAllSizes(float scale_factor)
        {
            ImGuiNative.ImGuiStyle_ScaleAllSizes((ImGuiStyle*)(NativePtr), scale_factor);
        }
    }
}
