using System.Runtime.InteropServices;
using System.Text;
using System;
using System.Drawing;

namespace CppSharp.Runtime {
    // HACK: .NET Standard 2.0 which we use in auto-building to support .NET Framework, lacks UnmanagedType.LPUTF8Str
    public class UTF8Marshaller : ICustomMarshaler {
        public void CleanUpManagedData(object ManagedObj) {
        }

        public void CleanUpNativeData(IntPtr pNativeData)
            => Marshal.FreeHGlobal(pNativeData);

        public int GetNativeDataSize() => -1;

        public IntPtr MarshalManagedToNative(object managedObj) {
            if (managedObj == null)
                return IntPtr.Zero;
            if (!(managedObj is string))
                throw new MarshalDirectiveException(
                    "UTF8Marshaler must be used on a string.");

            // not null terminated
            byte[] strbuf = Encoding.UTF8.GetBytes((string)managedObj);
            IntPtr buffer = Marshal.AllocHGlobal(strbuf.Length + 1);
            Marshal.Copy(strbuf, 0, buffer, strbuf.Length);

            // write the terminating null
            Marshal.WriteByte((IntPtr)((int)buffer + strbuf.Length), 0);
            return buffer;
        }

        public unsafe object MarshalNativeToManaged(IntPtr str) {
            if (str == IntPtr.Zero)
                return null;

            int byteCount = 0;
            var str8 = (byte*)str;
            while (*(str8++) != 0) byteCount += sizeof(byte);

            byte[] arr = new byte[byteCount];
            Marshal.Copy(str, arr, 0, byteCount);

            return Encoding.UTF8.GetString(arr, 0, byteCount);
        }

        public static ICustomMarshaler GetInstance(string pstrCookie) {
            if (marshaler == null)
                marshaler = new UTF8Marshaller();
            return marshaler;
        }

        private static UTF8Marshaller marshaler;
    }

    public unsafe static class MarshalUtil {
        public static string GetString(Encoding encoding, IntPtr str) {
            if (str == IntPtr.Zero)
                return null;

            int byteCount = 0;

            if (encoding == Encoding.UTF32) {
                var str32 = (int*)str;
                while (*(str32++) != 0) byteCount += sizeof(int);
            }
            else if (encoding == Encoding.Unicode || encoding == Encoding.BigEndianUnicode) {
                var str16 = (short*)str;
                while (*(str16++) != 0) byteCount += sizeof(short);
            }
            else {
                var str8 = (byte*)str;
                while (*(str8++) != 0) byteCount += sizeof(byte);
            }

            byte[] arr = new byte[byteCount];
            Marshal.Copy(str, arr, 0, byteCount);

            return encoding.GetString(arr, 0, byteCount);
        }

        [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr memcpy(IntPtr dest, IntPtr src, UInt32 count);

        public static T[] GetArray<T>(void* array, int size) where T : unmanaged {
            if (array == null)
                return null;
            var result = new T[size];
            fixed (void* fixedResult = result) {
                memcpy((IntPtr)fixedResult, (IntPtr)array, (uint)(sizeof(T) * size));
                //Buffer.MemoryCopy(array, fixedResult, sizeof(T) * size, sizeof(T) * size);
            }
            return result;
        }

        public static char[] GetCharArray(sbyte* array, int size) {
            if (array == null)
                return null;
            var result = new char[size];
            for (var i = 0; i < size; ++i)
                result[i] = Convert.ToChar(array[i]);
            return result;
        }

        public static IntPtr[] GetIntPtrArray(IntPtr* array, int size) {
            return GetArray<IntPtr>(array, size);
        }

        //public static T GetDelegate<T>(IntPtr[] vtables, short table, int i) where T : class {
        //    var slot = *(IntPtr*)(vtables[table] + i * sizeof(IntPtr));
        //    return Marshal.GetDelegateForFunctionPointer<T>(slot);
       // }
    }
}