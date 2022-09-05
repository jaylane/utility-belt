using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe partial struct StbTexteditRow
    {
        public float x0;
        public float x1;
        public float baseline_y_delta;
        public float ymin;
        public float ymax;
        public int num_chars;
    }
    public unsafe partial struct StbTexteditRowPtr
    {
        public StbTexteditRow* NativePtr { get; }
        public StbTexteditRowPtr(StbTexteditRow* nativePtr) => NativePtr = nativePtr;
        public StbTexteditRowPtr(IntPtr nativePtr) => NativePtr = (StbTexteditRow*)nativePtr;
        public static implicit operator StbTexteditRowPtr(StbTexteditRow* nativePtr) => new StbTexteditRowPtr(nativePtr);
        public static implicit operator StbTexteditRow* (StbTexteditRowPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator StbTexteditRowPtr(IntPtr nativePtr) => new StbTexteditRowPtr(nativePtr);
        public ref float x0 => ref NativePtr->x0;
        public ref float x1 => ref NativePtr->x1;
        public ref float baseline_y_delta => ref NativePtr->baseline_y_delta;
        public ref float ymin => ref NativePtr->ymin;
        public ref float ymax => ref NativePtr->ymax;
        public ref int num_chars => ref NativePtr->num_chars;
    }
}
