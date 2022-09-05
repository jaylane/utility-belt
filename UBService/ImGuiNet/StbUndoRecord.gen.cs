using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe partial struct StbUndoRecord
    {
        public int where;
        public int insert_length;
        public int delete_length;
        public int char_storage;
    }
    public unsafe partial struct StbUndoRecordPtr
    {
        public StbUndoRecord* NativePtr { get; }
        public StbUndoRecordPtr(StbUndoRecord* nativePtr) => NativePtr = nativePtr;
        public StbUndoRecordPtr(IntPtr nativePtr) => NativePtr = (StbUndoRecord*)nativePtr;
        public static implicit operator StbUndoRecordPtr(StbUndoRecord* nativePtr) => new StbUndoRecordPtr(nativePtr);
        public static implicit operator StbUndoRecord* (StbUndoRecordPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator StbUndoRecordPtr(IntPtr nativePtr) => new StbUndoRecordPtr(nativePtr);
        public ref int where => ref NativePtr->where;
        public ref int insert_length => ref NativePtr->insert_length;
        public ref int delete_length => ref NativePtr->delete_length;
        public ref int char_storage => ref NativePtr->char_storage;
    }
}
