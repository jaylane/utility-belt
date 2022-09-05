using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe partial struct ImFontConfig
    {
        public void* FontData;
        public int FontDataSize;
        public byte FontDataOwnedByAtlas;
        public int FontNo;
        public float SizePixels;
        public int OversampleH;
        public int OversampleV;
        public byte PixelSnapH;
        public Vector2 GlyphExtraSpacing;
        public Vector2 GlyphOffset;
        public ushort* GlyphRanges;
        public float GlyphMinAdvanceX;
        public float GlyphMaxAdvanceX;
        public byte MergeMode;
        public uint FontBuilderFlags;
        public float RasterizerMultiply;
        public ushort EllipsisChar;
        public fixed byte Name[40];
        public ImFont* DstFont;
    }
    public unsafe partial struct ImFontConfigPtr
    {
        public ImFontConfig* NativePtr { get; }
        public ImFontConfigPtr(ImFontConfig* nativePtr) => NativePtr = nativePtr;
        public ImFontConfigPtr(IntPtr nativePtr) => NativePtr = (ImFontConfig*)nativePtr;
        public static implicit operator ImFontConfigPtr(ImFontConfig* nativePtr) => new ImFontConfigPtr(nativePtr);
        public static implicit operator ImFontConfig* (ImFontConfigPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImFontConfigPtr(IntPtr nativePtr) => new ImFontConfigPtr(nativePtr);
        public IntPtr FontData { get => (IntPtr)NativePtr->FontData; set => NativePtr->FontData = (void*)value; }
        public ref int FontDataSize => ref NativePtr->FontDataSize;
        public ref byte FontDataOwnedByAtlas => ref NativePtr->FontDataOwnedByAtlas;
        public ref int FontNo => ref NativePtr->FontNo;
        public ref float SizePixels => ref NativePtr->SizePixels;
        public ref int OversampleH => ref NativePtr->OversampleH;
        public ref int OversampleV => ref NativePtr->OversampleV;
        public ref byte PixelSnapH => ref NativePtr->PixelSnapH;
        public ref Vector2 GlyphExtraSpacing => ref NativePtr->GlyphExtraSpacing;
        public ref Vector2 GlyphOffset => ref NativePtr->GlyphOffset;
        public IntPtr GlyphRanges { get => (IntPtr)NativePtr->GlyphRanges; set => NativePtr->GlyphRanges = (ushort*)value; }
        public ref float GlyphMinAdvanceX => ref NativePtr->GlyphMinAdvanceX;
        public ref float GlyphMaxAdvanceX => ref NativePtr->GlyphMaxAdvanceX;
        public ref byte MergeMode => ref NativePtr->MergeMode;
        public ref uint FontBuilderFlags => ref NativePtr->FontBuilderFlags;
        public ref float RasterizerMultiply => ref NativePtr->RasterizerMultiply;
        public ref ushort EllipsisChar => ref NativePtr->EllipsisChar;
        public RangeAccessor<byte> Name => new RangeAccessor<byte>(NativePtr->Name, 40);
        public ImFontPtr DstFont => new ImFontPtr(NativePtr->DstFont);
        public void Destroy()
        {
            ImGuiNative.ImFontConfig_destroy((ImFontConfig*)(NativePtr));
        }
    }
}
