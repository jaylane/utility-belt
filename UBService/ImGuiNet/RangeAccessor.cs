using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable 1591
namespace ImGuiNET
{
    public unsafe struct RangeAccessor<T> where T : struct
    {
        private static readonly int s_sizeOfT = Marshal.SizeOf(typeof(T));

        public readonly void* Data;
        public readonly int Count;

        public RangeAccessor(IntPtr data, int count) : this(data.ToPointer(), count) { }
        public RangeAccessor(void* data, int count)
        {
            Data = data;
            Count = count;
        }

        public T this[int index]
        {
            get {
                if (index < 0 || index >= Count) {
                    throw new IndexOutOfRangeException();
                }

                IntPtr address = (IntPtr)((int)Data + s_sizeOfT * index);
                T ret = (T)Marshal.PtrToStructure(address, typeof(T));
                return ret;
            }
        }
    }

    public unsafe struct RangePtrAccessor<T> where T : struct
    {
        public readonly void* Data;
        public readonly int Count;

        public RangePtrAccessor(IntPtr data, int count) : this(data.ToPointer(), count) { }
        public RangePtrAccessor(void* data, int count)
        {
            Data = data;
            Count = count;
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException();
                }
                IntPtr address = (IntPtr)((int)Data + sizeof(void*) * index);
                T ret = (T)Marshal.PtrToStructure(address, typeof(T));
                return ret;
            }
        }
    }
    /*
    public static class RangeAccessorExtensions
    {
        public static unsafe string GetStringASCII(this RangeAccessor<byte> stringAccessor)
        {
            return Encoding.ASCII.GetString((byte*)stringAccessor.Data, stringAccessor.Count);
        }
    }
    */
}
