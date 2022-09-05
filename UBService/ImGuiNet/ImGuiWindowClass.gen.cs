using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe partial struct ImGuiWindowClass
    {
        public uint ClassId;
        public uint ParentViewportId;
        public ImGuiViewportFlags ViewportFlagsOverrideSet;
        public ImGuiViewportFlags ViewportFlagsOverrideClear;
        public ImGuiTabItemFlags TabItemFlagsOverrideSet;
        public ImGuiDockNodeFlags DockNodeFlagsOverrideSet;
        public byte DockingAlwaysTabBar;
        public byte DockingAllowUnclassed;
    }
    public unsafe partial struct ImGuiWindowClassPtr
    {
        public ImGuiWindowClass* NativePtr { get; }
        public ImGuiWindowClassPtr(ImGuiWindowClass* nativePtr) => NativePtr = nativePtr;
        public ImGuiWindowClassPtr(IntPtr nativePtr) => NativePtr = (ImGuiWindowClass*)nativePtr;
        public static implicit operator ImGuiWindowClassPtr(ImGuiWindowClass* nativePtr) => new ImGuiWindowClassPtr(nativePtr);
        public static implicit operator ImGuiWindowClass* (ImGuiWindowClassPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImGuiWindowClassPtr(IntPtr nativePtr) => new ImGuiWindowClassPtr(nativePtr);
        public ref uint ClassId => ref NativePtr->ClassId;
        public ref uint ParentViewportId => ref NativePtr->ParentViewportId;
        public ref ImGuiViewportFlags ViewportFlagsOverrideSet => ref NativePtr->ViewportFlagsOverrideSet;
        public ref ImGuiViewportFlags ViewportFlagsOverrideClear => ref NativePtr->ViewportFlagsOverrideClear;
        public ref ImGuiTabItemFlags TabItemFlagsOverrideSet => ref NativePtr->TabItemFlagsOverrideSet;
        public ref ImGuiDockNodeFlags DockNodeFlagsOverrideSet => ref NativePtr->DockNodeFlagsOverrideSet;
        public ref byte DockingAlwaysTabBar => ref NativePtr->DockingAlwaysTabBar;
        public ref byte DockingAllowUnclassed => ref NativePtr->DockingAllowUnclassed;
        public void Destroy()
        {
            ImGuiNative.ImGuiWindowClass_destroy((ImGuiWindowClass*)(NativePtr));
        }
    }
}
