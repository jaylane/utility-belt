using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Interop.Input;
using Microsoft.DirectX;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Dungeon;
using UtilityBelt.Lib.Maps.Markers;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using static UtilityBelt.Tools.VTankControl;
using UBService.Lib.Settings;
using Microsoft.DirectX.Direct3D;
using System.Collections.Generic;
using ACE.DatLoader.FileTypes;
using ACE.DatLoader.Entity;

namespace UtilityBelt.Tools {
    [Name("OverlayMap")]
    [Summary("Provides ingame 3d map overlay.")]
    [FullDescription(@"Todo...")]
    public class OverlayMap : ToolBase {
        #region Config
        [Summary("Enabled")]
        [Hotkey("OverlayMapToggle", "Toggle overlay map display")]
        public Setting<bool> Enabled = new Setting<bool>(false);

        [Summary("Only draw the map overlay when video patched")]
        public Setting<bool> OnlyDrawWhenVideoPatched = new Setting<bool>(true);

        [Summary("Environment Walls / Ceilings display options")]
        public readonly ColorToggleRenderOption DrawEnvironmentWallsCeilings = new ColorToggleRenderOption(true, 0x41FFFFFF, true, true);

        [Summary("Floor display options")]
        public readonly ColorToggleRenderOption DrawFloors = new ColorToggleRenderOption(true, 0x64F5F5F5, true, true);

        [Summary("Static Objects display options")]
        public readonly ColorToggleRenderOption DrawStaticObjects = new ColorToggleRenderOption(true, 0x1EF500F5, true, true);

        [Summary("Monster display options")]
        public readonly ColorToggleRenderOption DrawMonsters = new ColorToggleRenderOption(true, 0x30BB0000, true, true);

        [Summary("Other Character display options")]
        public readonly ColorToggleRenderOption DrawPlayers = new ColorToggleRenderOption(true, 0x3000FE99, true, true);

        [Summary("Your Character display options")]
        public readonly ColorToggleRenderOption DrawCharacter = new ColorToggleRenderOption(true, 0x3000FF00, true, true);

        [Summary("NPC display options")]
        public readonly ColorToggleRenderOption DrawNpcs = new ColorToggleRenderOption(true, 0x30404090, true, true);

        [Summary("Portal display options")]
        public readonly ColorToggleRenderOption DrawPortals = new ColorToggleRenderOption(true, 0x60AA00FE, true, true);
        #endregion // Config

        private Device D3Ddevice { get => UtilityBeltPlugin.Instance.D3Ddevice; }
        private bool isRunning = false;
        private bool forceReload = false;

        public int[] OtherTriIndexes { get; private set; }
        public int[] StaticTriIndexes { get; private set; }

        private IndexBuffer staticTriIndexBuffer;
        private IndexBuffer otherTriIndexBuffer;
        private IndexBuffer walkableTriIndexBuffer;
        private VertexBuffer vertexBuffer;
        private CustomVertex.PositionColored[] vertices;
        private IndexBuffer playerIndexBuffer;
        private VertexBuffer playerVertexBuffer;
        private int[] WalkableTriIndexes;

        private uint LoadedLandblock;

        private struct TrackedObject {
            public int SetupId;
            public ObjectClass ObjectClass;
        }

        private Dictionary<long, IndexBuffer> _objectGeometryIndexBuffers = new Dictionary<long, IndexBuffer>();
        private Dictionary<long, VertexBuffer> _objectGeometryVertexBuffers = new Dictionary<long, VertexBuffer>();
        private Dictionary<int, TrackedObject> _trackedObjects = new Dictionary<int, TrackedObject>();
        private List<int> _objectsToTryTrack = new List<int>();

        public LandblockGeometry LoadedLandblockGeometry { get; private set; }

        public OverlayMap(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            if (UBHelper.Core.GameState == UBHelper.GameState.In_Game) {
                TryStart();
            }
            else {
                UBHelper.Core.GameStateChanged += Core_GameStateChanged;
            }
            Changed += OverlayMapSettings_Changed;
        }

        private void Core_GameStateChanged(UBHelper.GameState previous, UBHelper.GameState new_state) {
            UBHelper.Core.GameStateChanged -= Core_GameStateChanged;
            TryStart();
        }

        private void TryStart() {
            if (Enabled && !isRunning) {
                UB.Core.RenderFrame += Core_RenderFrame;
                UB.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
                UB.Core.WorldFilter.ReleaseObject += WorldFilter_ReleaseObject;
                isRunning = true;
            }
            else if (!Enabled && isRunning) {
                UB.Core.RenderFrame -= Core_RenderFrame;
                UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                UB.Core.WorldFilter.ReleaseObject -= WorldFilter_ReleaseObject;
                isRunning = false;
            }
        }

        private void TrackExistingObjects() {
            _trackedObjects.Clear();
            using (var wos = CoreManager.Current.WorldFilter.GetLandscape()) {
                foreach (var wo in wos) {
                    TryTrackObject(wo);
                }
            }
        }

        private void TryTrackObject(WorldObject wo) {
            if (ShouldTrack(wo) && !_trackedObjects.ContainsKey(wo.Id)) {
                _trackedObjects.Add(wo.Id, new TrackedObject() {
                    ObjectClass = wo.ObjectClass,
                    SetupId = wo.Values(LongValueKey.Model)
                });
            }
        }

        private bool ShouldTrack(WorldObject wo) {
            if (!UB.Core.Actions.IsValidObject(wo.Id))
                return false;

            switch (wo.ObjectClass) {
                case ObjectClass.Player:
                    return DrawPlayers.Enabled && wo.Id != UB.Core.CharacterFilter.Id;

                case ObjectClass.Monster:
                    return DrawMonsters.Enabled;

                case ObjectClass.Portal:
                case ObjectClass.Lifestone:
                    return DrawPortals.Enabled;

                case ObjectClass.Npc:
                case ObjectClass.Vendor:
                    return DrawNpcs.Enabled;

                default:
                    return false;
            }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                _objectsToTryTrack.Add(e.New.Id);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_ReleaseObject(object sender, ReleaseObjectEventArgs e) {
            try {
                _trackedObjects.Remove(e.Released.Id);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void OverlayMapSettings_Changed(object sender, SettingChangedEventArgs e) {
            forceReload = true;
            TryStart();
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                CheckCurrentLandblock();
                Render();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal void Render() {
            if (OnlyDrawWhenVideoPatched.Value && !UB.Plugin.VideoPatch)
                return;

            D3Ddevice.Transform.View = Camera.GetD3DViewTransform();

            using (var stateBlock = new StateBlock(D3Ddevice, StateBlockType.All)) {
                stateBlock.Capture();

                D3Ddevice.Transform.World = Matrix.Identity;

                D3Ddevice.RenderState.CullMode = Cull.CounterClockwise;
                D3Ddevice.RenderState.Lighting = false;
                D3Ddevice.RenderState.DepthBias = 1;
                D3Ddevice.RenderState.DestinationBlend = Blend.InvSourceAlpha;
                D3Ddevice.RenderState.DiffuseMaterialSource = ColorSource.Color1;
                D3Ddevice.RenderState.AlphaTestEnable = false;
                D3Ddevice.SetTexture(0, null);

                D3Ddevice.RenderState.ZBufferEnable = true;
                D3Ddevice.RenderState.ZBufferWriteEnable = true;
                D3Ddevice.RenderState.ZBufferFunction = Compare.Always;

                D3Ddevice.RenderState.DestinationBlend = Blend.One;
                D3Ddevice.RenderState.SourceBlend = Blend.SourceAlpha;

                D3Ddevice.RenderState.FogEnable = true;
                D3Ddevice.RenderState.FogStart = 0;
                D3Ddevice.RenderState.FogEnd = 90;
                D3Ddevice.RenderState.FogColor = Color.FromArgb(255, 0, 20, 20);
                D3Ddevice.RenderState.FogDensity = 0.2f;

                RenderCurrentLandblock();
                RenderDynamicObjects();
                RenderPlayer();

                stateBlock.Apply();
            }

        }

        private void RenderPlayer() {
            if (!DrawCharacter.Enabled)
                return;

            RenderDynamicObject(DrawCharacter, UB.Core.CharacterFilter.Id, playerIndexBuffer, playerVertexBuffer, Cull.None);
        }

        private void RenderDynamicObjects() {
            D3Ddevice.VertexFormat = CustomVertex.PositionColored.Format;

            if (_objectsToTryTrack.Count > 0) {
                foreach (var objToTrack in _objectsToTryTrack) {
                    TryTrackObject(UB.Core.WorldFilter[objToTrack]);
                }
                _objectsToTryTrack.Clear();
            }

            foreach (var kv in _trackedObjects) {
                if (!UB.Core.Actions.IsValidObject(kv.Key))
                    continue;

                IndexBuffer woIndexBuffer;
                VertexBuffer woVertexBuffer;
                ColorToggleRenderOption setting = null;
                Cull cullMode = Cull.CounterClockwise;
                switch (kv.Value.ObjectClass) {
                    case ObjectClass.Player:
                        setting = DrawPlayers;
                        break;
                    
                    case ObjectClass.Monster:
                        setting = DrawMonsters;
                        break;

                    case ObjectClass.Portal:
                    case ObjectClass.Lifestone:
                        setting = DrawPortals;
                        cullMode = Cull.None;
                        break;

                    case ObjectClass.Npc:
                    case ObjectClass.Vendor:
                        setting = DrawNpcs;
                        break;
                }

                EnsureTrackedObjectBuffers(kv.Value.SetupId, setting.Color);
                var key = (kv.Value.SetupId << 32) + setting.Color;
                var hasIndexBuffer = _objectGeometryIndexBuffers.TryGetValue(key, out woIndexBuffer);
                var hasVertexBuffer = _objectGeometryVertexBuffers.TryGetValue(key, out woVertexBuffer);

                if (hasIndexBuffer && hasVertexBuffer) {
                    RenderDynamicObject(setting, kv.Key, woIndexBuffer, woVertexBuffer, cullMode);
                }
            }
        }

        private void RenderDynamicObject(ColorToggleRenderOption setting, int id, IndexBuffer woIndexBuffer, VertexBuffer woVertexBuffer, Cull cullMode) {
            if (setting.Enabled.Value) {
                var pos = PhysicsObject.GetPosition(id);
                if (pos == null)
                    return;
                var rot = (float)Geometry.QuaternionToHeading(PhysicsObject.GetRot(id));
                var world = Matrix.Identity;
                var rotWorld = Matrix.Identity;
                rotWorld.RotateY(rot - (float)Math.PI);
                world.Translate(new Vector3(pos.X, pos.Z, pos.Y));

                D3Ddevice.SetTransform(TransformType.World, rotWorld * world);
                RenderWithSettings(setting, woVertexBuffer, woIndexBuffer, cullMode);
            }
        }

        private void EnsureTrackedObjectBuffers(int setupId, int color) {
            var key = (setupId << 32) + color;
            if (!_objectGeometryIndexBuffers.ContainsKey(key)) {
                var geometry = new DynamicObjectGeometry((uint)setupId);
                var geometryVertices = new CustomVertex.PositionColored[geometry.Vertices.Count];
                for (var i = 0; i < geometry.Vertices.Count; i++) {
                    geometryVertices[i] = new CustomVertex.PositionColored(new Vector3( 
                        geometry.Vertices[i].X,
                        geometry.Vertices[i].Z,
                        geometry.Vertices[i].Y
                    ), color);
                }
                var geometryVertexBuffer = new VertexBuffer(geometryVertices[0].GetType(), geometryVertices.Length, D3Ddevice, Usage.WriteOnly, CustomVertex.PositionColored.Format, Pool.Managed);
                geometryVertexBuffer.SetData(geometryVertices, 0, LockFlags.None);

                var triIndexes = new List<int>();
                foreach (var triIndex in geometry.Triangles) {
                    triIndexes.AddRange(triIndex);
                }

                if (triIndexes.Count > 0) {
                    var geometryTriIndexBuffer = new IndexBuffer(triIndexes[0].GetType(), triIndexes.Count, D3Ddevice, Usage.None, Pool.Managed);
                    geometryTriIndexBuffer.SetData(triIndexes.ToArray(), 0, LockFlags.None);
                    _objectGeometryIndexBuffers.Add(key, geometryTriIndexBuffer);
                    _objectGeometryVertexBuffers.Add(key, geometryVertexBuffer);
                }
            }
        }

        private void RenderCurrentLandblock() {
            D3Ddevice.VertexFormat = CustomVertex.PositionColored.Format;

            RenderWithSettings(DrawEnvironmentWallsCeilings, vertexBuffer, otherTriIndexBuffer);
            RenderWithSettings(DrawFloors, vertexBuffer, walkableTriIndexBuffer, Cull.None);
            RenderWithSettings(DrawStaticObjects, vertexBuffer, staticTriIndexBuffer);
        }

        private void RenderWithSettings(ColorToggleRenderOption setting, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, Cull cullMode=Cull.CounterClockwise) {
            if (setting.Enabled.Value && indexBuffer != null && indexBuffer.SizeInBytes / sizeof(int) > 0) {
                D3Ddevice.SetStreamSource(0, vertexBuffer, 0);
                D3Ddevice.RenderState.CullMode = cullMode;
                D3Ddevice.Indices = indexBuffer;
                if (setting.DrawWireFrame) {
                    D3Ddevice.RenderState.FillMode = FillMode.WireFrame;
                    D3Ddevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, indexBuffer.SizeInBytes / sizeof(int), 0, indexBuffer.SizeInBytes / sizeof(int) / 3);
                }
                if (setting.DrawSolid) {
                    D3Ddevice.RenderState.FillMode = FillMode.Solid;
                    D3Ddevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, indexBuffer.SizeInBytes / sizeof(int), 0, indexBuffer.SizeInBytes / sizeof(int) / 3);
                }
            }
        }

        private void CheckCurrentLandblock() {
            var currentLb = (uint)(CoreManager.Current.Actions.Landcell & 0xFFFF0000);
            if (forceReload || currentLb != LoadedLandblock) {
                LoadedLandblockGeometry = new LandblockGeometry(currentLb);
                LoadedLandblock = currentLb;
                TrackExistingObjects();
                MakeBuffers();
            }

            forceReload = false;
        }

        private void MakeBuffers() {
            DestroyBuffers();

            vertices = new CustomVertex.PositionColored[LoadedLandblockGeometry.Vertices.Count];
            int color;
            for (var i = 0; i < LoadedLandblockGeometry.Vertices.Count; i++) {
                switch (LoadedLandblockGeometry.VerticesTypes[i]) {
                    case LandblockGeometry.GeometryType.Floor:
                        color = DrawFloors.Color;
                        break;
                    case LandblockGeometry.GeometryType.StaticObject:
                        color = DrawStaticObjects.Color;
                        break;
                    default:
                        color = DrawEnvironmentWallsCeilings.Color;
                        break;
                }

                vertices[i] = new CustomVertex.PositionColored(new Vector3(
                    LoadedLandblockGeometry.Vertices[i].X,
                    LoadedLandblockGeometry.Vertices[i].Z,
                    LoadedLandblockGeometry.Vertices[i].Y
                ), color);
            } 

            vertexBuffer = new VertexBuffer(vertices[0].GetType(), vertices.Length, D3Ddevice, Usage.WriteOnly, CustomVertex.PositionColored.Format, Pool.Managed);
            vertexBuffer.SetData(vertices, 0, LockFlags.None);

            if (DrawFloors.Enabled) {
                var walkableTriIndexes = new List<int>();
                foreach (var triIndexes in LoadedLandblockGeometry.WalkableTriangles) {
                    walkableTriIndexes.AddRange(triIndexes);
                }

                this.WalkableTriIndexes = walkableTriIndexes.ToArray();
                if (WalkableTriIndexes.Length > 0) {
                    walkableTriIndexBuffer = new IndexBuffer(walkableTriIndexes[0].GetType(), walkableTriIndexes.Count, D3Ddevice, Usage.None, Pool.Managed);
                    walkableTriIndexBuffer.SetData(this.WalkableTriIndexes, 0, LockFlags.None);
                }
            }

            if (DrawEnvironmentWallsCeilings.Enabled) {
                var otherTriIndexes = new List<int>();
                foreach (var triIndexes in LoadedLandblockGeometry.OtherTriangles) {
                    otherTriIndexes.AddRange(triIndexes);
                }

                this.OtherTriIndexes = otherTriIndexes.ToArray();
                if (OtherTriIndexes.Length > 0) {
                    otherTriIndexBuffer = new IndexBuffer(otherTriIndexes[0].GetType(), otherTriIndexes.Count, D3Ddevice, Usage.None, Pool.Managed);
                    otherTriIndexBuffer.SetData(this.OtherTriIndexes, 0, LockFlags.None);
                }
            }

            if (DrawStaticObjects.Enabled) {
                var staticTriIndexes = new List<int>();
                foreach (var triIndexes in LoadedLandblockGeometry.StaticTriangles) {
                    staticTriIndexes.AddRange(triIndexes);
                }

                this.StaticTriIndexes = staticTriIndexes.ToArray();
                if (StaticTriIndexes.Length > 0) {
                    staticTriIndexBuffer = new IndexBuffer(staticTriIndexes[0].GetType(), staticTriIndexes.Count, D3Ddevice, Usage.None, Pool.Managed);
                    staticTriIndexBuffer.SetData(this.StaticTriIndexes, 0, LockFlags.None);
                }
            }

            var setupId = (uint)UB.Core.WorldFilter[UB.Core.CharacterFilter.Id].Values(LongValueKey.Model);
            var playerGeometry = new DynamicObjectGeometry(setupId);

            var playerVertices = new CustomVertex.PositionColored[playerGeometry.Vertices.Count];
            for (var i = 0; i < playerGeometry.Vertices.Count; i++) {
                playerVertices[i] = new CustomVertex.PositionColored(new Vector3(
                    playerGeometry.Vertices[i].X,
                    playerGeometry.Vertices[i].Z,
                    playerGeometry.Vertices[i].Y
                ), DrawCharacter.Color);
            }

            playerVertexBuffer = new VertexBuffer(playerVertices[0].GetType(), playerVertices.Length, D3Ddevice, Usage.WriteOnly, CustomVertex.PositionColored.Format, Pool.Managed);
            playerVertexBuffer.SetData(playerVertices, 0, LockFlags.None);

            var playerTriIndexes = new List<int>();
            foreach (var triIndex in playerGeometry.Triangles) {
                playerTriIndexes.AddRange(triIndex);
            }

            if (playerTriIndexes.Count > 0) {
                playerIndexBuffer = new IndexBuffer(playerTriIndexes[0].GetType(), playerTriIndexes.Count, D3Ddevice, Usage.None, Pool.Managed);
                playerIndexBuffer.SetData(playerTriIndexes.ToArray(), 0, LockFlags.None);
            }
        }

        private void DestroyBuffers() {
            walkableTriIndexBuffer?.Dispose();
            otherTriIndexBuffer?.Dispose();
            staticTriIndexBuffer?.Dispose();
            vertexBuffer?.Dispose();
            playerIndexBuffer?.Dispose();
            playerVertexBuffer?.Dispose();

            foreach (var indexBuffer in _objectGeometryIndexBuffers.Values) {
                indexBuffer.Dispose();
            }

            foreach (var vertexBuffer in _objectGeometryVertexBuffers.Values) {
                vertexBuffer.Dispose();
            }

            _objectGeometryIndexBuffers.Clear();
            _objectGeometryVertexBuffers.Clear();
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UBHelper.Core.GameStateChanged -= Core_GameStateChanged;
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    Changed -= OverlayMapSettings_Changed;
                    UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                    UB.Core.WorldFilter.ReleaseObject -= WorldFilter_ReleaseObject;
                    DestroyBuffers();
                }
                disposedValue = true;
            }
        }
    }
}
