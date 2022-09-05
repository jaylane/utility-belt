using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe partial struct ImDrawData
    {
        public byte Valid;
        public int CmdListsCount;
        public int TotalIdxCount;
        public int TotalVtxCount;
        public ImDrawList** CmdLists;
        public Vector2 DisplayPos;
        public Vector2 DisplaySize;
        public Vector2 FramebufferScale;
        public ImGuiViewport* OwnerViewport;
    }
    public unsafe partial struct ImDrawDataPtr
    {
        public ImDrawData* NativePtr { get; }
        public ImDrawDataPtr(ImDrawData* nativePtr) => NativePtr = nativePtr;
        public ImDrawDataPtr(IntPtr nativePtr) => NativePtr = (ImDrawData*)nativePtr;
        public static implicit operator ImDrawDataPtr(ImDrawData* nativePtr) => new ImDrawDataPtr(nativePtr);
        public static implicit operator ImDrawData* (ImDrawDataPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImDrawDataPtr(IntPtr nativePtr) => new ImDrawDataPtr(nativePtr);
        public ref byte Valid => ref NativePtr->Valid;
        public ref int CmdListsCount => ref NativePtr->CmdListsCount;
        public ref int TotalIdxCount => ref NativePtr->TotalIdxCount;
        public ref int TotalVtxCount => ref NativePtr->TotalVtxCount;
        public IntPtr CmdLists { get => (IntPtr)NativePtr->CmdLists; set => NativePtr->CmdLists = (ImDrawList**)value; }
        public ref Vector2 DisplayPos => ref NativePtr->DisplayPos;
        public ref Vector2 DisplaySize => ref NativePtr->DisplaySize;
        public ref Vector2 FramebufferScale => ref NativePtr->FramebufferScale;
        public ImGuiViewportPtr OwnerViewport => new ImGuiViewportPtr(NativePtr->OwnerViewport);
        public void Clear()
        {
            ImGuiNative.ImDrawData_Clear((ImDrawData*)(NativePtr));
        }
        public void DeIndexAllBuffers()
        {
            ImGuiNative.ImDrawData_DeIndexAllBuffers((ImDrawData*)(NativePtr));
        }
        public void Destroy()
        {
            ImGuiNative.ImDrawData_destroy((ImDrawData*)(NativePtr));
        }
        public void ScaleClipRects(Vector2 fb_scale)
        {
            ImGuiNative.ImDrawData_ScaleClipRects((ImDrawData*)(NativePtr), fb_scale);
        }
    }
}
