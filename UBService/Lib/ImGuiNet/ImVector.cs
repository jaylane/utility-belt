using Microsoft.DirectX.Direct3D;
using Newtonsoft.Json;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UBService.Lib.Settings.Serializers;

#pragma warning disable 1591
namespace ImGuiNET {

    [JsonConverter(typeof(ImGuiVectorConverter))]
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector2 {
        public float X, Y;

        public Vector2(float x, float y) {
            this.X = x;
            this.Y = y;
        }

        public override bool Equals(object obj) {
            if (obj is Vector2 v2) {
                return v2.X == X && v2.Y == Y;
            }
            return false;
        }

        public static Vector2 operator -(Vector2 a) => new Vector2(-a.X, -a.Y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator *(Vector2 a, Vector2 b) => new Vector2(a.X * b.X, a.Y * b.Y);
        public static Vector2 operator /(Vector2 a, Vector2 b) => new Vector2(a.X / b.X, a.Y / b.Y);
        public static Vector2 operator +(Vector2 a, float b) => new Vector2(a.X + b, a.Y + b);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.X * b, a.Y * b);
        public static Vector2 operator /(Vector2 a, float b) => new Vector2(a.X / b, a.Y / b);
    }

    [JsonConverter(typeof(ImGuiVectorConverter))]
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3 {
        public float X, Y, Z;

        public Vector3(float x, float y, float z) {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public override bool Equals(object obj) {
            if (obj is Vector3 v3) {
                return v3.X == X && v3.Y == Y && v3.Z == Z;
            }
            return false;
        }
    }

    [JsonConverter(typeof(ImGuiVectorConverter))]
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector4 {
        public float W, X, Y, Z;

        public Vector4(float w, float x, float y, float z) {
            this.W = w;
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public override bool Equals(object obj) {
            if (obj is Vector4 v4) {
                return v4.X == X && v4.Y == Y && v4.Z == Z && v4.W == W;
            }
            return false;
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
