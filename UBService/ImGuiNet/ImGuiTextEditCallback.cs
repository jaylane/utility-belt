using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate int ImGuiInputTextCallback(ImGuiInputTextCallbackData* data);
}
