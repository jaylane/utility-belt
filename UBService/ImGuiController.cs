using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.DirectX.Direct3D;
using System.Runtime.InteropServices;
using Microsoft.DirectX;
using Microsoft.DirectX.PrivateImplementationDetails;
using System.Security.Cryptography;
using ImGuiNET;
using Decal.Adapter.Wrappers;
using System.Drawing;

namespace UBService {
    /// <summary>
    /// ported from https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_dx9.cpp
    /// </summary>
    internal class ImGuiNETDx9Implementation {
        internal static Guid IID_IDirect3DDevice9 = new Guid("{D0223B96-BF7A-43fd-92BD-A43B0D82B9EB}");
        internal Device D3Ddevice;
        internal IntPtr unmanagedD3dPtr;
        internal int VertexBufferSize = 5000;
        internal int IndexBufferSize = 10000;

        private VertexBuffer _vertexBuffer = null;
        private IndexBuffer _indexBuffer = null;

        private Dictionary<IntPtr, Texture> _textureCache = new Dictionary<IntPtr, Texture>();
        private IntPtr _fontAtlasID = (IntPtr)1;
        private Texture _fontTexture;

        internal ImGuiNETDx9Implementation() {
            object d3dDevice = UBService.iDecal.GetD3DDevice(ref IID_IDirect3DDevice9);
            Marshal.QueryInterface(Marshal.GetIUnknownForObject(d3dDevice), ref IID_IDirect3DDevice9, out unmanagedD3dPtr);
            D3Ddevice = new Device(unmanagedD3dPtr);
        }

        // Backend data stored in io.BackendRendererUserData to allow support for multiple Dear ImGui contexts
        // It is STRONGLY preferred that you use docking branch with multi-viewports (== single Dear ImGui context + multiple windows) instead of multiple Dear ImGui contexts.
        //internal Backend ImGui_ImplDX9_GetBackendData() {
        //    return (int)ImGui.GetCurrentContext() != 0 ? ImGui.GetIO().BackendRendererUserData : (IntPtr)0;
        //}

        // Functions
        internal unsafe void ImGui_ImplDX9_SetupRenderState(ImDrawDataPtr draw_data) {
            // Setup render state: fixed-pipeline, alpha-blending, no face culling, no depth testing, shade mode (for gradient), bilinear sampling.
            D3Ddevice.PixelShader = null;
            D3Ddevice.VertexShader = null;
            D3Ddevice.RenderState.FillMode = FillMode.Solid;
            D3Ddevice.RenderState.ShadeMode = ShadeMode.Gouraud;
            D3Ddevice.RenderState.ZBufferWriteEnable = false;
            D3Ddevice.RenderState.AlphaTestEnable = false;
            D3Ddevice.RenderState.CullMode = Cull.None;
            D3Ddevice.RenderState.ZBufferEnable = false;
            D3Ddevice.RenderState.AlphaBlendEnable = false;
            D3Ddevice.RenderState.BlendOperation = BlendOperation.Add;
            D3Ddevice.RenderState.AlphaSourceBlend = Blend.SourceAlpha;
            D3Ddevice.RenderState.AlphaDestinationBlend = Blend.InvSourceAlpha;
            D3Ddevice.RenderState.ScissorTestEnable = true;
            D3Ddevice.RenderState.FogEnable = false;
            D3Ddevice.RenderState.RangeFogEnable = false;
            D3Ddevice.RenderState.SpecularEnable = false;
            D3Ddevice.RenderState.StencilEnable = false;
            D3Ddevice.RenderState.Clipping = false;
            D3Ddevice.RenderState.Lighting = false;
            D3Ddevice.SetTextureStageState(0, TextureStageStates.ColorOperation, (int)TextureOperation.Modulate);
            D3Ddevice.SetTextureStageState(0, TextureStageStates.ColorArgument1, (int)TextureArgument.TextureColor);
            D3Ddevice.SetTextureStageState(0, TextureStageStates.ColorArgument2, (int)TextureArgument.Diffuse);
            D3Ddevice.SetTextureStageState(0, TextureStageStates.AlphaOperation, (int)TextureOperation.Modulate);
            D3Ddevice.SetTextureStageState(0, TextureStageStates.AlphaArgument1, (int)TextureArgument.TextureColor);
            D3Ddevice.SetTextureStageState(0, TextureStageStates.AlphaArgument2, (int)TextureArgument.Diffuse);
            D3Ddevice.SetTextureStageState(1, TextureStageStates.ColorOperation, (int)TextureOperation.Disable);
            D3Ddevice.SetTextureStageState(1, TextureStageStates.AlphaOperation, (int)TextureOperation.Disable);
            D3Ddevice.SetSamplerState(0, SamplerStageStates.MinFilter, (int)TextureFilter.Linear);
            D3Ddevice.SetSamplerState(0, SamplerStageStates.MagFilter, (int)TextureFilter.Linear);

            float L = draw_data.DisplayPos.X + 0.5f;
            float R = draw_data.DisplayPos.X + draw_data.DisplaySize.Y + 0.5f;
            float T = draw_data.DisplayPos.Y + 0.5f;
            float B = draw_data.DisplayPos.Y + draw_data.DisplaySize.Y + 0.5f;

            Matrix mIdentity = MakeMatrix(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);

            Matrix mProjection = MakeMatrix(
                2.0f / (R - L), 0.0f, 0.0f, 0.0f,
                0.0f, 2.0f / (T - B), 0.0f, 0.0f,
                0.0f, 0.0f, 0.5f, 0.0f,
                (L + R) / (L - R), (T + B) / (B - T), 0.5f, 1.0f
            );

            D3Ddevice.SetTransform(TransformType.World, mIdentity);
            D3Ddevice.SetTransform(TransformType.View, mIdentity);
            D3Ddevice.SetTransform(TransformType.Projection, mProjection);
        }

        private Matrix MakeMatrix(float v1, float v2, float v3, float v4, float v5, float v6, float v7, float v8, float v9, float v10, float v11, float v12, float v13, float v14, float v15, float v16) {
            return new Matrix() {
                M11 = v1,
                M12 = v2,
                M13 = v3,
                M14 = v4,
                M21 = v5,
                M22 = v6,
                M23 = v7,
                M24 = v8,
                M31 = v9,
                M32 = v10,
                M33 = v11,
                M34 = v12,
                M41 = v13,
                M42 = v14,
                M43 = v15,
                M44 = v16
            };
        }

        // Render function.
        internal unsafe void ImGui_ImplDX9_RenderDrawData(ImDrawDataPtr draw_data) {
            // Avoid rendering when minimized
            if (draw_data.DisplaySize.X <= 0.0f || draw_data.DisplaySize.Y <= 0.0f || draw_data.CmdListsCount == 0)
                return;

            // Create and grow buffers if needed
            if (_vertexBuffer == null || VertexBufferSize < draw_data.TotalVtxCount) {
                if (_vertexBuffer != null) {
                    _vertexBuffer.Dispose();
                    _vertexBuffer = null;
                }
                VertexBufferSize = draw_data.TotalVtxCount + 5000;
                new VertexBuffer(typeof(CustomVertex.PositionColoredTextured), VertexBufferSize, D3Ddevice, Usage.Dynamic | Usage.WriteOnly, CustomVertex.PositionColoredTextured.Format, Pool.Default);
            }

            if (_indexBuffer == null || IndexBufferSize < draw_data.TotalIdxCount) {
                if (_indexBuffer != null) {
                    _indexBuffer.Dispose();
                    _indexBuffer = null;
                }
                IndexBufferSize = draw_data.TotalVtxCount + 10000;
                _indexBuffer = new IndexBuffer(typeof(int), VertexBufferSize, D3Ddevice, Usage.Dynamic | Usage.WriteOnly, Pool.Default);
            }

            if (_vertexBuffer == null || _indexBuffer == null)
                return;

            using (var stateBlock = new StateBlock(D3Ddevice, StateBlockType.All)) {
                // Backup the DX9 transform (DX9 documentation suggests that it is included in the StateBlock but it doesn't appear to)
                var lastWorldMatrix = D3Ddevice.GetTransform(TransformType.World);
                var lastViewMatrix = D3Ddevice.GetTransform(TransformType.View);
                var lastProjectionMatrix = D3Ddevice.GetTransform(TransformType.Projection);

                var vertices = new List<CustomVertex.PositionColoredTextured>();
                var indices = new List<int>();

                // Copy and convert all vertices into a single contiguous buffer, convert colors to DX9 default format.
                // FIXME-OPT: This is a minor waste of resource, the ideal is to use imconfig.h and
                //  1) to avoid repacking colors:   #define IMGUI_USE_BGRA_PACKED_COLOR
                //  2) to avoid repacking vertices: #define IMGUI_OVERRIDE_DRAWVERT_STRUCT_LAYOUT struct ImDrawVert { ImVec2 pos; float z; ImU32 col; ImVec2 uv; }
                ImDrawVert[] imVerts = new ImDrawVert[draw_data.CmdListsCount];
                for (int n = 0; n < draw_data.CmdListsCount; n++) {
                    ImDrawListPtr cmd_list = draw_data.CmdListsRange[1];
                    for (int i = 0; i < cmd_list.VtxBuffer.Size; i++) {
                        var imVertPtr = ((ImDrawVertPtr)cmd_list.VtxBuffer.Data);
                        vertices.Add(new CustomVertex.PositionColoredTextured(new Microsoft.DirectX.Vector3(imVertPtr.pos.X, imVertPtr.pos.Y, 0), IMGUI_COL_TO_DX9_ARGB(imVertPtr.col), imVertPtr.uv.X, imVertPtr.uv.Y));
                        indices.Add((ushort)cmd_list.IdxBuffer.Data);
                    }
                }
                _vertexBuffer.SetData(vertices.ToArray(), 0, LockFlags.Discard);
                _indexBuffer.SetData(indices.ToArray(), 0, LockFlags.Discard);

                D3Ddevice.SetStreamSource(0, _vertexBuffer, 0, CustomVertex.PositionColoredTextured.StrideSize);
                D3Ddevice.Indices = _indexBuffer;
                D3Ddevice.VertexFormat = CustomVertex.PositionColoredTextured.Format;

                // Setup desired DX state
                ImGui_ImplDX9_SetupRenderState(draw_data);

                // Render command lists
                // (Because we merged all buffers into a single one, we maintain our own offset into them)
                int global_vtx_offset = 0;
                int global_idx_offset = 0;
                ImGuiNET.Vector2 clip_off = draw_data.DisplayPos;
                for (int n = 0; n < draw_data.CmdListsCount; n++) {
                    ImDrawListPtr cmd_list = draw_data.CmdListsRange[1];
                    for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++) {
                        ImDrawCmdPtr pcmd = cmd_list.CmdBuffer[cmd_i];
                        if (pcmd.UserCallback != IntPtr.Zero) {
                            // not supported...
                            throw new NotImplementedException("ImDrawList::AddCallback() not supported");

                            // User callback, registered via ImDrawList::AddCallback()
                            // (ImDrawCallback_ResetRenderState is a special callback value used by the user to request the renderer to reset render state.)

                            //if (pcmd.UserCallback == ResetRender) {
                            //    ImGui_ImplDX9_SetupRenderState(draw_data);
                            //}
                            //else {
                            //    pcmd.UserCallback(cmd_list, pcmd);
                            //}
                        }
                        else {
                            // Project scissor/clipping rectangles into framebuffer space

                            ImGuiNET.Vector2 clip_min = new ImGuiNET.Vector2(pcmd.ClipRect.X - clip_off.X, pcmd.ClipRect.Y - clip_off.Y);
                            ImGuiNET.Vector2 clip_max = new ImGuiNET.Vector2(pcmd.ClipRect.Z - clip_off.X, pcmd.ClipRect.W - clip_off.Y);
                            if (clip_max.X <= clip_min.X || clip_max.Y <= clip_min.Y)
                                continue;

                            if (_textureCache.TryGetValue(pcmd.GetTexID(), out Texture texture)) {
                                D3Ddevice.SetTexture(0, texture);
                                D3Ddevice.ScissorRectangle = new System.Drawing.Rectangle((int)clip_min.X, (int)clip_min.Y, (int)clip_max.X - (int)clip_min.X, (int)clip_max.Y - (int)clip_min.Y);
                                D3Ddevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, (int)pcmd.VtxOffset + global_vtx_offset, 0, cmd_list.VtxBuffer.Size, (int)pcmd.IdxOffset + global_idx_offset, (int)pcmd.ElemCount / 3);
                            }
                        }
                    }
                }
            }
        }

        private int IMGUI_COL_TO_DX9_ARGB(uint _COL) {
            return (int)(((_COL) & 0xFF00FF00) | (((_COL) & 0xFF0000) >> 16) | (((_COL) & 0xFF) << 16));
        }

        internal bool ImGui_ImplDX9_Init() {
            var io = ImGui.GetIO();
            io.Fonts.AddFontDefault();
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;  // We can honor the ImDrawCmd::VtxOffset field, allowing for large meshes.

            return true;
        }

        internal void ImGui_ImplDX9_Shutdown() {
            ImGui_ImplDX9_InvalidateDeviceObjects();
        }

        internal unsafe bool ImGui_ImplDX9_CreateFontsTexture() {
            _fontTexture = new Texture(D3Ddevice, new Bitmap(@"C:\Games\tim.png"), Usage.Dynamic, Pool.Default);

            // Build texture atlas
            /*
            var io = ImGui.GetIO();
            byte* pixels;
            int width, height, bytesPerPixel;
            io.Fonts.GetTexDataAsRGBA32(out pixels, out width, out height, out bytesPerPixel);
            io.Fonts.SetTexID(_fontAtlasID);

            byte[] cPixels = new byte[width * height * bytesPerPixel];

            // Convert RGBA32 to BGRA32 (because RGBA32 is not well supported by DX9 devices)
            if (io.Fonts.TexPixelsUseColors != 0) {
                byte[] bPixels = new byte[width * height * bytesPerPixel];
                int[] newPixels = new int[width * height];
                Marshal.Copy((IntPtr)pixels, bPixels, 0, width * height * bytesPerPixel);

                for (var i = 0; i < bPixels.Length; i++) {
                    newPixels[i] = IMGUI_COL_TO_DX9_ARGB(bPixels[i]);
                }

                Marshal.Copy(newPixels, 0, (IntPtr)pixels, width * height * bytesPerPixel);
            }

            Marshal.Copy((IntPtr)pixels, cPixels, 0, width * height * bytesPerPixel);
            using (var textureStream = new MemoryStream(cPixels)) {
                _fontTexture = new Texture(D3Ddevice, textureStream, Usage.Dynamic, Pool.Default);
            }
            */
            if (_textureCache.ContainsKey(_fontAtlasID)) {
                _textureCache[_fontAtlasID] = _fontTexture;
            }
            else {
                _textureCache.Add(_fontAtlasID, _fontTexture);
            }

            return true;
        }

        internal bool ImGui_ImplDX9_CreateDeviceObjects() {
            if (!ImGui_ImplDX9_CreateFontsTexture())
                return false;
            return true;
        }

        internal void ImGui_ImplDX9_InvalidateDeviceObjects() {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            if (_fontTexture != null) {
                _fontTexture?.Dispose();
                ImGui.GetIO().Fonts.SetTexID(IntPtr.Zero);
            }
        }

        internal void ImGui_ImplDX9_NewFrame() {
            if (_fontTexture == null)
                ImGui_ImplDX9_CreateDeviceObjects();
        }
    }
}
