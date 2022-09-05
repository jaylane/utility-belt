using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe partial struct ImFontGlyph
    {
        public uint Colored;
        public uint Visible;
        public uint Codepoint;
        public float AdvanceX;
        public float X0;
        public float Y0;
        public float X1;
        public float Y1;
        public float U0;
        public float V0;
        public float U1;
        public float V1;
    }
    public unsafe partial struct ImFontGlyphPtr
    {
        public ImFontGlyph* NativePtr { get; }
        public ImFontGlyphPtr(ImFontGlyph* nativePtr) => NativePtr = nativePtr;
        public ImFontGlyphPtr(IntPtr nativePtr) => NativePtr = (ImFontGlyph*)nativePtr;
        public static implicit operator ImFontGlyphPtr(ImFontGlyph* nativePtr) => new ImFontGlyphPtr(nativePtr);
        public static implicit operator ImFontGlyph* (ImFontGlyphPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImFontGlyphPtr(IntPtr nativePtr) => new ImFontGlyphPtr(nativePtr);
        public ref uint Colored => ref NativePtr->Colored;
        public ref uint Visible => ref NativePtr->Visible;
        public ref uint Codepoint => ref NativePtr->Codepoint;
        public ref float AdvanceX => ref NativePtr->AdvanceX;
        public ref float X0 => ref NativePtr->X0;
        public ref float Y0 => ref NativePtr->Y0;
        public ref float X1 => ref NativePtr->X1;
        public ref float Y1 => ref NativePtr->Y1;
        public ref float U0 => ref NativePtr->U0;
        public ref float V0 => ref NativePtr->V0;
        public ref float U1 => ref NativePtr->U1;
        public ref float V1 => ref NativePtr->V1;
    }
}
