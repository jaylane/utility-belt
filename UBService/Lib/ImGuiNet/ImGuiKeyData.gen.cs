using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe partial struct ImGuiKeyData
    {
        public byte Down;
        public float DownDuration;
        public float DownDurationPrev;
        public float AnalogValue;
    }
    public unsafe partial struct ImGuiKeyDataPtr
    {
        public ImGuiKeyData* NativePtr { get; }
        public ImGuiKeyDataPtr(ImGuiKeyData* nativePtr) => NativePtr = nativePtr;
        public ImGuiKeyDataPtr(IntPtr nativePtr) => NativePtr = (ImGuiKeyData*)nativePtr;
        public static implicit operator ImGuiKeyDataPtr(ImGuiKeyData* nativePtr) => new ImGuiKeyDataPtr(nativePtr);
        public static implicit operator ImGuiKeyData* (ImGuiKeyDataPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImGuiKeyDataPtr(IntPtr nativePtr) => new ImGuiKeyDataPtr(nativePtr);
        public ref byte Down => ref NativePtr->Down;
        public ref float DownDuration => ref NativePtr->DownDuration;
        public ref float DownDurationPrev => ref NativePtr->DownDurationPrev;
        public ref float AnalogValue => ref NativePtr->AnalogValue;
    }
}
