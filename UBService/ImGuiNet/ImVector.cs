using Microsoft.DirectX.Direct3D;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable 1591
namespace ImGuiNET {
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector2 {
        public float X, Y;

        public Vector2(float x, float y) {
            this.X = x;
            this.Y = y;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3 {
        public float X, Y, Z;

        public Vector3(float x, float y, float z) {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Vector4 {
        public float W, X, Y, Z;

        public Vector4(float w, float x, float y, float z) {
            this.W = w;
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
    }

    public unsafe struct ImVector
    {
        public readonly int Size;
        public readonly int Capacity;
        public readonly IntPtr Data;
        /*
        public ref T Ref<T>(int index)
        {
            return ref Unsafe.AsRef<T>((byte*)Data + index * Unsafe.SizeOf<T>());
        }

        public IntPtr Address<T>(int index)
        {
            return (IntPtr)((byte*)Data + index * Unsafe.SizeOf<T>());
        }
        */
    }

    public unsafe struct ImVector<T>
    {
        public readonly int Size;
        public readonly int Capacity;
        public readonly IntPtr Data;

        public ImVector(ImVector vector)
        {
            Size = vector.Size;
            Capacity = vector.Capacity;
            Data = vector.Data;
        }

        public ImVector(int size, int capacity, IntPtr data)
        {
            Size = size;
            Capacity = capacity;
            Data = data;
        }

        //public ref T this[int index] => ref Unsafe.AsRef<T>((byte*)Data + index * Unsafe.SizeOf<T>());
    }

    public unsafe struct ImPtrVector<T>
    {
        public readonly int Size;
        public readonly int Capacity;
        public readonly IntPtr Data;
        private readonly int _stride;

        public ImPtrVector(ImVector vector, int stride)
            : this(vector.Size, vector.Capacity, vector.Data, stride)
        { }

        public ImPtrVector(int size, int capacity, IntPtr data, int stride)
        {
            Size = size;
            Capacity = capacity;
            Data = data;
            _stride = stride;
        }
        
        public T this[int index]
        {
            get
            {
                IntPtr address = (IntPtr)((int)Data + index * _stride);
                T ret = (T)Marshal.PtrToStructure(address, typeof(T));
                return ret;
            }
        }
    }
}
