using Microsoft.DirectX.Direct3D;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace UBService.Views {
    /// <summary>
    /// Holds a texture (from a bitmap), maybe other things later...
    /// Using this class will automatically recreate the texture as needed.
    /// </summary>
    public class ManagedTexture : IDisposable {
        /// <summary>
        /// The bitmap this texture is using.
        /// </summary>
        public Bitmap Bitmap { get; set; } = null;

        /// <summary>
        /// The DirectX texture
        /// </summary>
        public Texture Texture { get; set; } = null;

        /// <summary>
        /// Pointer to the unmanaged texture
        /// </summary>
        public unsafe IntPtr TexturePtr => (Texture == null) ? IntPtr.Zero : (IntPtr)Texture.UnmanagedComPointer;

        /// <summary>
        /// Create a new managed texture from a bitmap. This copies your bitmap data immediately
        /// so you can dispose the passed bitmap immediately.
        /// </summary>
        /// <param name="bitmap">The bitmap source for the texture.</param>
        public ManagedTexture(Bitmap bitmap) : base() {
            Bitmap = new Bitmap(bitmap);
            CreateTexture();
            HudManager.AddManagedTexture(this);
        }

        /// <summary>
        /// Create a new managed texture from a bitmap stream
        /// </summary>
        /// <param name="stream">The bitmap stream source</param>
        public ManagedTexture(Stream stream) : base() {
            Bitmap = new Bitmap(stream);
            CreateTexture();
            HudManager.AddManagedTexture(this);
        }


        internal void CreateTexture() {
            // avoid creating a new texture and losing reference to the old one,
            // if lost and not released, DX will crash later when resetting the device
            if (Texture != null)
                return;

            if (Bitmap != null)
                Texture = new Texture(HudManager.D3Ddevice, Bitmap, Usage.Dynamic, Pool.Default);
        }

        internal void ReleaseTexture() {
            Texture?.Dispose();
            Texture = null;
        }

        /// <summary>
        /// Release this texture
        /// </summary>
        public void Dispose() {
            HudManager.RemoveManagedTexture(this);
            Bitmap?.Dispose();
            Bitmap = null;
        }
    }
}
