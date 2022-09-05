using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe partial struct STB_TexteditState
    {
        public int cursor;
        public int select_start;
        public int select_end;
        public byte insert_mode;
        public int row_count_per_page;
        public byte cursor_at_end_of_line;
        public byte initialized;
        public byte has_preferred_x;
        public byte single_line;
        public byte padding1;
        public byte padding2;
        public byte padding3;
        public float preferred_x;
        public StbUndoState undostate;
    }
    public unsafe partial struct STB_TexteditStatePtr
    {
        public STB_TexteditState* NativePtr { get; }
        public STB_TexteditStatePtr(STB_TexteditState* nativePtr) => NativePtr = nativePtr;
        public STB_TexteditStatePtr(IntPtr nativePtr) => NativePtr = (STB_TexteditState*)nativePtr;
        public static implicit operator STB_TexteditStatePtr(STB_TexteditState* nativePtr) => new STB_TexteditStatePtr(nativePtr);
        public static implicit operator STB_TexteditState* (STB_TexteditStatePtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator STB_TexteditStatePtr(IntPtr nativePtr) => new STB_TexteditStatePtr(nativePtr);
        public ref int cursor => ref NativePtr->cursor;
        public ref int select_start => ref NativePtr->select_start;
        public ref int select_end => ref NativePtr->select_end;
        public ref byte insert_mode => ref NativePtr->insert_mode;
        public ref int row_count_per_page => ref NativePtr->row_count_per_page;
        public ref byte cursor_at_end_of_line => ref NativePtr->cursor_at_end_of_line;
        public ref byte initialized => ref NativePtr->initialized;
        public ref byte has_preferred_x => ref NativePtr->has_preferred_x;
        public ref byte single_line => ref NativePtr->single_line;
        public ref byte padding1 => ref NativePtr->padding1;
        public ref byte padding2 => ref NativePtr->padding2;
        public ref byte padding3 => ref NativePtr->padding3;
        public ref float preferred_x => ref NativePtr->preferred_x;
        public ref StbUndoState undostate => ref NativePtr->undostate;
    }
}
