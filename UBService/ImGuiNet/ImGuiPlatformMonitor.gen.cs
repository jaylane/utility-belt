using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe partial struct ImGuiPlatformMonitor
    {
        public Vector2 MainPos;
        public Vector2 MainSize;
        public Vector2 WorkPos;
        public Vector2 WorkSize;
        public float DpiScale;
    }
    public unsafe partial struct ImGuiPlatformMonitorPtr
    {
        public ImGuiPlatformMonitor* NativePtr { get; }
        public ImGuiPlatformMonitorPtr(ImGuiPlatformMonitor* nativePtr) => NativePtr = nativePtr;
        public ImGuiPlatformMonitorPtr(IntPtr nativePtr) => NativePtr = (ImGuiPlatformMonitor*)nativePtr;
        public static implicit operator ImGuiPlatformMonitorPtr(ImGuiPlatformMonitor* nativePtr) => new ImGuiPlatformMonitorPtr(nativePtr);
        public static implicit operator ImGuiPlatformMonitor* (ImGuiPlatformMonitorPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImGuiPlatformMonitorPtr(IntPtr nativePtr) => new ImGuiPlatformMonitorPtr(nativePtr);
        public ref Vector2 MainPos => ref NativePtr->MainPos;
        public ref Vector2 MainSize => ref NativePtr->MainSize;
        public ref Vector2 WorkPos => ref NativePtr->WorkPos;
        public ref Vector2 WorkSize => ref NativePtr->WorkSize;
        public ref float DpiScale => ref NativePtr->DpiScale;
        public void Destroy()
        {
            ImGuiNative.ImGuiPlatformMonitor_destroy((ImGuiPlatformMonitor*)(NativePtr));
        }
    }
}
