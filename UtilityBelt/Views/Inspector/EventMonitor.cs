using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using static UtilityBelt.Views.Inspector.Inspector;
using UBService;
using ImGuiNET;
using Microsoft.DirectX.Direct3D;
using System.Runtime.InteropServices;

namespace UtilityBelt.Views.Inspector {
    /// <summary>
    /// A hud window that allows for monitoring an event.
    /// Dispose or close this window to get rid of it.
    /// </summary>
    public class EventMonitor : IDisposable {
        private static uint _id = 0;
        private Hud hud;
        private Vector2 minWindowSize = new Vector2(300, 200);
        private Vector2 maxWindowSize = new Vector2(99999, 99999);
        private DynamicEventHandler dynamicHandler;
        private List<DynamicEventArgs> events = new List<DynamicEventArgs>();
        private bool _subscribed;
        private bool _autoScroll = true;
        private Texture InspectorIcon;
        private List<Inspector> inspectors = new List<Inspector>();

        /// <summary>
        /// Name of this Event Monitor instance
        /// </summary>
        public string Name { get;}

        /// <summary>
        /// The event being monitored
        /// </summary>
        public EventInfo EventToMonitor { get; }

        /// <summary>
        /// The parent object instance where this event lives
        /// </summary>
        public object Parent { get; }

        /// <summary>
        /// Create a new Event Monitor window
        /// </summary>
        /// <param name="eventToMonitor">The event to monitor</param>
        public EventMonitor(string name, object parent, EventInfo eventToMonitor) {
            Name = name;
            Parent = parent;
            EventToMonitor = eventToMonitor;

            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream("UtilityBelt.Resources.icons.eye.png")) {
                hud = HudManager.CreateHud($"EventMonitor: {Name}##EventMonitor{_id++}", new Bitmap(manifestResourceStream));
            }

            hud.Render += Hud_Render;
            hud.PreRender += Hud_PreRender;
            hud.ShouldHide += Hud_ShouldHide;
            hud.CreateTextures += Hud_CreateTextures;
            hud.DestroyTextures += Hud_DestroyTextures;
            CreateTextures();
            SubscribeToEvent();
        }

        internal void SubscribeToEvent() {
            if (dynamicHandler == null) {
                Type eventHandlerType = EventToMonitor.EventHandlerType;
                var eventArgsType = eventHandlerType.GetMethod("Invoke").GetParameters()[1].ParameterType;
                var dynamicHandlerType = typeof(DynamicEventHandler<>).MakeGenericType(eventArgsType);
                dynamicHandler = (DynamicEventHandler)Activator.CreateInstance(dynamicHandlerType);

                dynamicHandler.Delegate = Delegate.CreateDelegate(eventHandlerType, dynamicHandler, "HandleHelper");
                dynamicHandler.EventInfo = EventToMonitor;
                dynamicHandler.OnEvent += DynamicHandler_OnEvent;

                EventToMonitor.AddEventHandler(Parent, dynamicHandler.Delegate);
                _subscribed = true;
            }
        }

        private void DynamicHandler_OnEvent(object sender, DynamicEventArgs e) {
            events.Add(e);
        }

        internal void UnsubscribeFromEvent() {
            if (dynamicHandler != null) {
                dynamicHandler.OnEvent -= DynamicHandler_OnEvent;
                dynamicHandler.EventInfo?.RemoveEventHandler(Parent, dynamicHandler.Delegate);
                dynamicHandler = null;
                _subscribed = false;
            }
        }

        private void Hud_ShouldHide(object sender, EventArgs e) {
            Dispose();
        }

        private void Hud_PreRender(object sender, EventArgs e) {
            ImGui.SetNextWindowSizeConstraints(minWindowSize, maxWindowSize);

            if (_subscribed && dynamicHandler == null) {
                SubscribeToEvent();
            }
            else if (!_subscribed && dynamicHandler != null) {
                UnsubscribeFromEvent();
            }
        }

        private unsafe void Hud_Render(object sender, EventArgs e) {
            ImGui.Checkbox("Auto scroll", ref _autoScroll); 
            ImGui.SameLine(0, 10);
            ImGui.Checkbox("Subscribed", ref _subscribed);

            ImGui.BeginChild("events", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y));
            var i = 0;
            foreach (var ev in events) {
                var tint = new Vector4(1, 1, 1, 1);
                var size = new Vector2(12, 12);
                ImGui.PushID(i);
                if (ImGui.ImageButton((IntPtr)InspectorIcon.UnmanagedComPointer, size, new Vector2(0, 0), new Vector2(1, 1), 1, new Vector4(), tint)) {
                    inspectors.Add(new Inspector($"{EventToMonitor.Name}#{i}", ev) {
                        DisposeOnClose = true
                    });
                }
                ImGui.PopID();
                ImGui.SameLine();
                ImGui.Text($"Event#{i}: {ev}");
                if ((_autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY()))
                    ImGui.SetScrollHereY(1.0f);
                i++;
            }
            ImGui.EndChild();
        }

        private void Hud_CreateTextures(object sender, EventArgs e) {
            CreateTextures();
        }

        private void Hud_DestroyTextures(object sender, EventArgs e) {
            DestroyTextures();
        }

        private void CreateTextures() {
            try {
                CreateTextureFromResource(ref InspectorIcon, "UtilityBelt.Resources.icons.inspector.png");
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private void CreateTextureFromResource(ref Microsoft.DirectX.Direct3D.Texture texture, string resourcePath) {
            if (texture == null)
                texture = LoadTextureFromResouce(resourcePath);
        }

        private Microsoft.DirectX.Direct3D.Texture LoadTextureFromResouce(string resourcePath) {
            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream(resourcePath)) {
                using (Bitmap bmp = new Bitmap(manifestResourceStream)) {
                    return new Microsoft.DirectX.Direct3D.Texture(UtilityBeltPlugin.Instance.D3Ddevice, bmp, Usage.Dynamic, Pool.Default);
                }
            }
        }

        private void DestroyTextures() {
            try {
                DestroyTexture(ref InspectorIcon);
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private void DestroyTexture(ref Microsoft.DirectX.Direct3D.Texture texture) {
            texture?.Dispose();
            texture = null;
        }

        private bool isDisposed = false;
        public void Dispose() {
            if (!isDisposed) {
                UBLoader.FilterCore.LogError($"Dispose: EventMonitor {Name}");
                foreach (var inspector in inspectors) {
                    inspector.Dispose();
                }
                inspectors.Clear();
                UnsubscribeFromEvent();
                DestroyTextures();
                hud.Visible = false;
                hud.Render -= Hud_Render;
                hud.PreRender -= Hud_PreRender;
                hud.ShouldHide -= Hud_ShouldHide;
                hud.CreateTextures -= Hud_CreateTextures;
                hud.DestroyTextures -= Hud_DestroyTextures;
                hud.Dispose();
                isDisposed = true;
            }
        }
    }
}
