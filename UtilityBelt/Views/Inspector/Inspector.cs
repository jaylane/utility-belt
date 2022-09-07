using ImGuiNET;
using Microsoft.DirectX.Direct3D;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters;
using System.Security.Cryptography;
using UBService;
using static UtilityBelt.Views.DictionaryEditor;
using static UtilityBelt.Views.Inspector.Inspector;

namespace UtilityBelt.Views.Inspector {
    public class Inspector : IDisposable {
        private static uint _id = 0;
        private static int _pushId = 0;

        private Hud hud;
        private Vector2 minWindowSize = new Vector2(500, 250);
        private Vector2 maxWindowSize = new Vector2(99999, 99999);
        private Vector4 iconTint = new Vector4(1, 1, 1, 1);
        private Vector2 iconSize = new Vector2(12, 12);

        private bool isDisposed = false;
        private int _ptr = 0;

        private string selectedId { get; set; }
        private InspectedObject _selected;
        private Texture EyeTexture = null;
        private List<EventMonitor> eventMonitors = new List<EventMonitor>();
        private List<Inspector> inspectors = new List<Inspector>();
        private List<MethodInspector> methodInspectors = new List<MethodInspector>();

        public static object DRAG_SOURCE_OBJECT = null;

        /// <summary>
        /// An inspected object is an object wrapped with MemberInfo/Parent,
        /// so we can reflect against the parent to get the current value of
        /// primitives.
        /// </summary>
        public class InspectedObject {
            internal Dictionary<string, DynamicEventHandler> _subscriptions = new Dictionary<string, DynamicEventHandler>();

            /// <summary>
            /// MemberInfo of an inspected object
            /// </summary>
            public MemberInfo MemberInfo = null;

            /// <summary>
            /// Parent object
            /// </summary>
            public object Parent = null;

            /// <summary>
            /// The type
            /// </summary>
            public Type Type {
                get {
                    switch (MemberInfo.MemberType) {
                        case MemberTypes.Property:
                            return (MemberInfo as PropertyInfo).PropertyType;
                        case MemberTypes.Field:
                            return (MemberInfo as FieldInfo).FieldType;
                        default:
                            return null;
                    }
                }
            }

            /// <summary>
            /// Value
            /// </summary>
            public object Value => GetMemberValue(Parent, MemberInfo);

            public InspectedObject(MemberInfo memberInfo, object parent) {
                MemberInfo = memberInfo;
                Parent = parent;
            }

            internal void SubscribeToEvents() {
                // todo, this breaks a little so disabled for now...
                return;

                var events = GetAllEventInfos(Value);
                foreach (var ev in events) {
                    try {
                        var eventInfo = ev as EventInfo;
                        Type eventHandlerType = eventInfo.EventHandlerType;
                        var invokeParams = eventHandlerType.GetMethod("Invoke").GetParameters();
                        if (invokeParams.Length <= 9) {
                            if (invokeParams.Any(p => p.IsOut)) {
                                Logger.Error($"{MemberInfo.Name}.{eventInfo.Name} Event: events with ref params are not supported");
                                continue;
                            }
                            var typeParams = eventHandlerType.GetMethod("Invoke").GetParameters().Select(t => t.ParameterType).ToArray();
                            Type dynamicHandlerType = null;
                            switch (typeParams.Length) {
                                case 0: dynamicHandlerType = typeof(DynamicEventHandler); break;
                                case 1: dynamicHandlerType = typeof(DynamicEventHandler<>).MakeGenericType(typeParams); break;
                                case 2: dynamicHandlerType = typeof(DynamicEventHandler<,>).MakeGenericType(typeParams); break;
                                case 3: dynamicHandlerType = typeof(DynamicEventHandler<,,>).MakeGenericType(typeParams); break;
                                case 4: dynamicHandlerType = typeof(DynamicEventHandler<,,,>).MakeGenericType(typeParams); break;
                                case 5: dynamicHandlerType = typeof(DynamicEventHandler<,,,,>).MakeGenericType(typeParams); break;
                                case 6: dynamicHandlerType = typeof(DynamicEventHandler<,,,,,>).MakeGenericType(typeParams); break;
                                case 7: dynamicHandlerType = typeof(DynamicEventHandler<,,,,,,>).MakeGenericType(typeParams); break;
                                case 8: dynamicHandlerType = typeof(DynamicEventHandler<,,,,,,,>).MakeGenericType(typeParams); break;
                                case 9: dynamicHandlerType = typeof(DynamicEventHandler<,,,,,,,,>).MakeGenericType(typeParams); break;
                            }

                            var dynamicHandler = (DynamicEventHandler)Activator.CreateInstance(dynamicHandlerType);

                            dynamicHandler.Delegate = Delegate.CreateDelegate(eventHandlerType, dynamicHandler, "HandleHelper");
                            dynamicHandler.EventInfo = eventInfo;

                            eventInfo.AddEventHandler(Value, dynamicHandler.Delegate);
                            _subscriptions.Add($"{ev.Name}", dynamicHandler);
                            Logger.WriteToChat($"added sub for {ev.Name}");
                        }
                        else {
                            Logger.Error($"{MemberInfo.Name}.{eventInfo.Name} Event: Only events with 1-9 arguments are supported. This had {invokeParams.Length}.");
                        }
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                }
            }

            internal void UnsubscribeFromEvents() {
                foreach (var sub in _subscriptions.Values) {
                    try {
                        sub.EventInfo.RemoveEventHandler(Value, sub.Delegate);
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                }
                _subscriptions.Clear();
            }
        }

        public string Name { get; }

        /// <summary>
        /// The object being inspected.
        /// </summary>
        public object ToInspect { get; private set; }

        /// <summary>
        /// The object currently selected in the treeview
        /// </summary>
        public InspectedObject Selected {
            get => _selected;
            set {
                _selected?.UnsubscribeFromEvents();
                _selected = value;
                _selected.SubscribeToEvents();
            }
        }

        /// <summary>
        /// Wether to Dispose itself when the window is closed
        /// </summary>
        public bool DisposeOnClose { get; set; }

        /// <summary>
        /// Create a new Inspector window
        /// </summary>
        /// <param name="toInspect">The object to inspect</param>
        public Inspector(string name, object toInspect) {
            Name = name;
            ToInspect = toInspect;
            Selected = new InspectedObject(GetType().GetProperty("ToInspect", BindingFlags.Public | BindingFlags.Instance), this);
            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream("UtilityBelt.Resources.icons.inspector.png")) {
                hud = HudManager.CreateHud($"Inspector: {Name}##Inspector{_id++}", new Bitmap(manifestResourceStream));
            }

            CreateTextures();

            hud.Render += Hud_Render;
            hud.PreRender += Hud_PreRender;
            hud.CreateTextures += Hud_CreateTextures;
            hud.DestroyTextures += Hud_DestroyTextures;
            hud.ShouldHide += Hud_ShouldHide;
        }

        private void Hud_ShouldHide(object sender, EventArgs e) {
            if (DisposeOnClose) Dispose();
        }

        private void Hud_PreRender(object sender, EventArgs e) {
            ImGui.SetNextWindowSizeConstraints(minWindowSize, maxWindowSize);
        }

        private void Hud_Render(object sender, EventArgs e) {
            _pushId = 0;
            var pad = 15;
            ImGui.BeginTable("InspectorTable", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoSavedSettings);
            {
                ImGui.TableSetupColumn("ObjectTree", ImGuiTableColumnFlags.WidthFixed, ImGui.GetContentRegionAvail().X * 0.3f);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.BeginChild("Object Tree", new Vector2(-1, ImGui.GetContentRegionAvail().Y - 4)); // object tree
                try {
                    RenderObjectTree(Name, GetType().GetProperty("ToInspect", BindingFlags.Public | BindingFlags.Instance), this);
                }
                catch (Exception ex) { Logger.LogException(ex); }
                ImGui.EndChild(); // object tree
                ImGui.TableSetColumnIndex(1);
                ImGui.BeginChild("Object Info", new Vector2(-1, ImGui.GetContentRegionAvail().Y - 4)); // object info
                try {
                    ImGui.PushID($"{Selected.GetHashCode()}.RenderDetails.{++_pushId}");
                    RenderDetails(Selected);
                    ImGui.PopID();
                }
                catch (Exception ex) { Logger.LogException(ex); }
                ImGui.EndChild();
            }
            ImGui.EndTable();
        }

        private unsafe void RenderDetails(InspectedObject inspectedObject) {
            if (Selected.MemberInfo.MemberType == MemberTypes.Event) {
                var eventInfo = inspectedObject.MemberInfo as EventInfo;
                var eventField = inspectedObject.Parent.GetType().GetField(eventInfo.Name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);

                if (eventField == null) {
                    ImGui.Text("null");
                    return;
                }

                Delegate[] delegates = GetEventDelegates(inspectedObject);
                if (ImGui.ImageButton((IntPtr)EyeTexture.UnmanagedComPointer, iconSize, new Vector2(0, 0), new Vector2(1, 1), 1, new Vector4(), iconTint)) {
                    eventMonitors.Add(new EventMonitor(eventInfo.Name, inspectedObject.Parent, eventInfo));
                }
                ImGui.SameLine();
                ImGui.Text($"Event {TypeDisplayString(eventField.FieldType)} {eventField.Name}");
                ImGui.Spacing();
                ImGui.Spacing();

                ImGui.Text($"Subscribed Event Delegates:");
                if (ImGui.BeginTable("EventDelegates", 1, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable)) {
                    foreach (var del in delegates) {
                        ImGui.TableNextColumn();
                        ImGui.Text($"{TypeDisplayString(del.Target.GetType())}.{del.Method.Name}");
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip($"{TypeDisplayString(del.Target.GetType(), false)}.{del.Method.Name}");
                        }
                    }
                    ImGui.EndTable();
                }
            }
            else {
                var type = Selected.Value == null ? Selected.Type : Selected.Value.GetType();
                ImGui.Text($"Type: {TypeDisplayString(type)}");
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip(TypeDisplayString(type, false));
                }

                if (inspectedObject.Value == null) {
                    ImGui.TextWrapped($"Value: null");
                }
                else if (inspectedObject.Type.IsPrimitive || inspectedObject.Type.IsEnum || inspectedObject.Type == typeof(string)) {
                    ImGui.TextWrapped($"Value: {DetailsDisplayString(inspectedObject.Value)}");
                }
                else {
                    ImGui.TextWrapped($"Value: {inspectedObject.Value}");

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    RenderEventsDetails(inspectedObject);
                    RenderPropertiesDetails(inspectedObject);
                    RenderFieldsDetails(inspectedObject);
                    RenderMethodsDetails(inspectedObject);

                    if (IsEnumerable(inspectedObject.Value)) {
                        RenderEnumerableChildrenDetails(inspectedObject);
                    }
                }
            }
        }

        private unsafe void RenderEnumerableChildrenDetails(InspectedObject inspectedObject) {
            var id = "EnumerableChildren" + Selected.GetHashCode().ToString();
            ImGui.Text($"Enumerable Contents:");
            if (ImGui.BeginTable(id, 2, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable)) {
                ImGui.TableSetupColumn("Key");
                ImGui.TableSetupColumn("Value");
                ImGui.TableHeadersRow();

                var enumerable = inspectedObject.Value as IEnumerable;

                var methodInfos = GetAllMethodInfos(inspectedObject.Value);
                var i = 0;
                foreach (var e in enumerable) {
                    ImGui.PushID($"enumerableItem.{++_pushId}");
                    if (e.GetType().IsGenericType && e.GetType().GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) {
                        object kvpKey = e.GetType().GetProperty("Key").GetValue(e, null);
                        object kvpValue = e.GetType().GetProperty("Value").GetValue(e, null);
                        ImGui.TableNextColumn();
                        ImGui.Text(DetailsDisplayString(kvpKey));
                        ImGui.TableNextColumn();
                        RenderSimpleObjectDetails(kvpValue);
                    }
                    else {
                        ImGui.TableNextColumn();
                        ImGui.Text(i.ToString());
                        ImGui.TableNextColumn();
                        RenderSimpleObjectDetails(e);
                    }
                    ImGui.PopID();
                    i++;
                }

                ImGui.EndTable();
            }
            ImGui.Spacing();
            ImGui.Spacing();
        }

        private unsafe void RenderSimpleObjectDetails(object obj) {
            ImGui.PushID($"{Selected.GetHashCode()}.RenderSimpleObjectDetails.{++_pushId}");
            if (obj != null && !obj.GetType().IsEnum && !obj.GetType().IsPrimitive) {
                if (ImGui.ImageButton((IntPtr)EyeTexture.UnmanagedComPointer, iconSize, new Vector2(0, 0), new Vector2(1, 1), 1, new Vector4(), iconTint)) {
                    Logger.WriteToChat($"Show new inspector");
                    inspectors.Add(new Inspector(DetailsDisplayString(obj), obj) {
                        DisposeOnClose = true
                    });
                }
                ImGui.SameLine();
            }
            ImGui.Selectable(DetailsDisplayString(obj));
            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None)) {
                DRAG_SOURCE_OBJECT = obj; 
                int _ptr = 0;
                ImGui.SetDragDropPayload("OBJECT_INSTANCE", (IntPtr)(&_ptr), sizeof(int));
                ImGui.Text(DetailsDisplayString(obj));
                ImGui.EndDragDropSource();
            }
            if (obj != null && ImGui.IsItemHovered()) {
                ImGui.SetTooltip(TypeDisplayString(obj.GetType(), false));
            }
            ImGui.PopID();
        }

        private unsafe void RenderMethodsDetails(InspectedObject inspectedObject) {
            var id = "Methods" + inspectedObject.Value.GetHashCode().ToString();
            ImGui.Text($"Methods:");
            if (ImGui.BeginTable(id, 3, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable)) {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Returns", ImGuiTableColumnFlags.WidthFixed, 140);
                ImGui.TableSetupColumn("Info", ImGuiTableColumnFlags.WidthFixed, 40);
                ImGui.TableHeadersRow();

                var methodInfos = GetAllMethodInfos(inspectedObject.Value);
                var i = 0;
                foreach (var method in methodInfos) {
                    var methodInfo = method as MethodInfo;
                    ImGui.PushID(i);
                    ImGui.TableNextColumn();
                    ImGui.Text(DetailsDisplayString(methodInfo));
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip(GetMethodDisplayString(methodInfo, false));
                    }
                    ImGui.TableNextColumn();
                    ImGui.Text(TypeDisplayString(methodInfo.ReturnType));
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip(TypeDisplayString(methodInfo.ReturnType, false));
                    }
                    ImGui.TableNextColumn();
                    if (ImGui.ImageButton((IntPtr)EyeTexture.UnmanagedComPointer, iconSize, new Vector2(0, 0), new Vector2(1, 1), 1, new Vector4(), iconTint)) {
                        methodInspectors.Add(new MethodInspector($"{TypeDisplayString(inspectedObject.Value.GetType())}.{GetMethodDisplayString(methodInfo)}", methodInfo, inspectedObject.Value));
                    }
                    ImGui.PopID();
                    i++;
                }

                ImGui.EndTable();
            }
            ImGui.Spacing();
            ImGui.Spacing();
        }

        private unsafe void RenderEventsDetails(InspectedObject inspectedObject) {
            var id = "Events" + inspectedObject.Value.GetHashCode().ToString();
            ImGui.Text($"Events:");
            if (ImGui.BeginTable(id, 4, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable)) {
                ImGui.TableSetupColumn("Event Name");
                ImGui.TableSetupColumn("EventArgs Type");
                ImGui.TableSetupColumn("Subscribers");
                ImGui.TableSetupColumn("Info");
                ImGui.TableHeadersRow();

                List<MemberInfo> members = GetAllEventInfos(inspectedObject.Value);

                foreach (var member in members) {
                    var eventInfo = member as EventInfo;
                    var eventInspectedObject = new InspectedObject(member, inspectedObject.Value);
                    Delegate[] delegates = GetEventDelegates(eventInspectedObject);
                    ImGui.TableNextColumn();
                    ImGui.Text($"{member.Name}");
                    ImGui.TableNextColumn();
                    ImGui.Text(TypeDisplayString(eventInfo.EventHandlerType.GetGenericArguments().FirstOrDefault()));
                    if (ImGui.IsItemHovered() && eventInfo.EventHandlerType.GetGenericArguments().Length > 0) {
                        ImGui.SetTooltip(TypeDisplayString(eventInfo.EventHandlerType.GetGenericArguments().First(), false));
                    }
                    ImGui.TableNextColumn();
                    ImGui.Text(delegates.Length.ToString());
                    if (delegates.Length > 0 && ImGui.IsItemHovered()) {
                        ImGui.BeginTooltip();
                        for (var i = 0; i < delegates.Length; i++) {
                            ImGui.Text($"#{i} {TypeDisplayString(delegates[i].Target.GetType(), false)}.{delegates[i].Method.Name}");
                        }
                        ImGui.EndTooltip();
                    }
                    ImGui.TableNextColumn();
                    if (inspectedObject._subscriptions.ContainsKey(eventInfo.Name)) {
                        ImGui.PushID(eventInfo.Name);
                        if (ImGui.ImageButton((IntPtr)EyeTexture.UnmanagedComPointer, iconSize, new Vector2(0, 0), new Vector2(1, 1), 1, new Vector4(), iconTint)) {
                            eventMonitors.Add(new EventMonitor(eventInfo.Name, inspectedObject.Value, eventInfo));
                        }
                        ImGui.SameLine();
                        ImGui.Text(DetailsDisplayString($"Called {inspectedObject._subscriptions[eventInfo.Name].CalledCount} times"));
                        ImGui.PopID();
                    }
                    else {
                        ImGui.PushID(eventInfo.Name);
                        if (ImGui.ImageButton((IntPtr)EyeTexture.UnmanagedComPointer, iconSize, new Vector2(0, 0), new Vector2(1, 1), 1, new Vector4(), iconTint)) {
                            eventMonitors.Add(new EventMonitor(eventInfo.Name, inspectedObject.Value, eventInfo));
                        }
                        ImGui.PopID();
                    }
                }

                ImGui.EndTable();
            }
            ImGui.Spacing();
            ImGui.Spacing();
        }

        private void RenderFieldsDetails(InspectedObject inspectedObject) {
            RenderMemberInfoDetailTable("Fields", "Field Name", inspectedObject, GetAllFieldInfos(inspectedObject.Value));
        }

        private unsafe void RenderPropertiesDetails(InspectedObject inspectedObject) {
            RenderMemberInfoDetailTable("Properties", "Property Name", inspectedObject, GetAllPropertyInfos(inspectedObject.Value));
        }

        private void RenderMemberInfoDetailTable(string headerText, string keyHeaderText, InspectedObject inspectedObject, List<MemberInfo> members) {
            var id = $"MemberInfoDetailTable.{headerText}";

            ImGui.Text(headerText);
            if (ImGui.BeginTable(id, 2, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable)) {
                ImGui.TableSetupColumn(keyHeaderText);
                ImGui.TableSetupColumn("Value");
                ImGui.TableHeadersRow();

                RenderMemberInfosDetailRows(inspectedObject, members);

                ImGui.EndTable();
            }
            ImGui.Spacing();
            ImGui.Spacing();
        }

        private void RenderMemberInfosDetailRows(InspectedObject inspectedObject, List<MemberInfo> members) {
            foreach (var member in members) {
                var inspectedMember = new InspectedObject(member, inspectedObject.Value);
                if (member is PropertyInfo propInfo && propInfo.GetIndexParameters().Any())
                    continue;
                ImGui.TableNextColumn();
                ImGui.Text($"{member.Name}");
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip(TypeDisplayString(inspectedMember.Type, false));
                }
                ImGui.TableNextColumn();
                RenderSimpleObjectDetails(inspectedMember.Value);
            }
        }

        private void RenderObjectTree(string name, MemberInfo memberInfo, object parent, string history = "", uint depth = 0) {
            var toInspect = GetMemberValue(parent, memberInfo);
            var id = string.IsNullOrEmpty(history) ? name : $"{history}.{name}";
            List<MemberInfo> members = null;

            var showChildren = ShouldRenderTree(toInspect?.GetType());
            if (showChildren) {
                members = GetAllMemberInfos(toInspect);
                showChildren = showChildren && members.Count > 0;
            }

            var flags = ImGuiTreeNodeFlags.None | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.AllowItemOverlap;
            var isEnumerable = IsEnumerable(toInspect);
            if (toInspect != null && toInspect.GetType() == typeof(string))
                isEnumerable = false;
            var hasItems = isEnumerable && DoesEnumerableHaveChildren(toInspect);
            if (!showChildren || isEnumerable)
                flags |= ImGuiTreeNodeFlags.Leaf;
            if (id == selectedId)
                flags |= ImGuiTreeNodeFlags.Selected;

            var isExpanded = ImGui.TreeNodeEx(id, flags, name);
            if (ImGui.IsItemClicked()) {
                Selected = new InspectedObject(memberInfo, parent);
                selectedId = id;
                Logger.WriteToChat($"Selected: {TreeDisplayString(toInspect)} {isEnumerable} {isExpanded}");
            }

            if (isExpanded) {
                if (isEnumerable) {
                    /*
                    var enumerable = toInspect as IEnumerable;
                    var i = 0;
                    foreach (var x in enumerable) {
                        var eId = $"{id}.{i}";
                        ImGui.TreeNodeEx(id, ImGuiTreeNodeFlags.Leaf, $"#{i} {TypeDisplayString(x.GetType())}");
                        ImGui.TreePop();
                        i++;
                    }
                    */
                }
                else if (showChildren) {
                    foreach (var member in members) {
                        if (member is PropertyInfo propInfo && propInfo.GetIndexParameters().Any())
                            continue;
                        RenderObjectTree(member.Name, member, toInspect, id, depth + 1);
                    }
                }
                ImGui.TreePop();
            }
        }

        #region Static Helpers
        internal static bool DoesEnumerableHaveChildren(object toInspect) {
            if (toInspect.GetType().GetInterfaces().Any(x => x == typeof(IEnumerable))) {
                var i = (toInspect as IEnumerable);
                if (i == null)
                    return false;
                var e = i.GetEnumerator();
                if (e == null)
                    return false;

                try {
                    return e.MoveNext();
                }
                catch {}
                return false;
            }

            return false;
        }

        internal static bool IsEnumerable(object toInspect) {
            if (toInspect == null) {
                return false;
            }
            if (toInspect.GetType().GetInterfaces().Any(x => x == typeof(IEnumerable))) {
                return true;
            }
            return false;
        }

        internal static object GetMemberValue(object toInspect, MemberInfo member) {
            try {
                if (member is PropertyInfo prop) {
                    if (prop.GetGetMethod(true) == null)
                        return null;
                    return prop.GetValue(toInspect, null);
                }
                else if (member is FieldInfo field) {
                    return field.GetValue(toInspect);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }

        internal static List<MemberInfo> GetAllMemberInfos(object toInspect) {
            if (toInspect == null)
                return new List<MemberInfo>();
            var members = new List<MemberInfo>();

            members.AddRange(GetAllPropertyInfos(toInspect));
            members.AddRange(GetAllFieldInfos(toInspect));
            //members.AddRange(GetAllEventInfos(toInspect));

            members.Sort((a, b) => a.Name.CompareTo(b.Name));

            return members;
        }

        internal static List<MemberInfo> GetAllEventInfos(object toInspect) {
            if (toInspect == null)
                return new List<MemberInfo>();
            var items = toInspect.GetType().GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => p as MemberInfo)
                .ToList();

            items.Sort((a, b) => a.Name.CompareTo(b.Name));
            return items;
        }

        internal static List<MemberInfo> GetAllFieldInfos(object toInspect) {
            if (toInspect == null)
                return new List<MemberInfo>();
            var items = toInspect.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                //.Where(p => p.FieldType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() != typeof(EventHandler<>)) && ShouldRenderProp(p))
                .Select(p => p as MemberInfo)
                .ToList();

            items.Sort((a, b) => a.Name.CompareTo(b.Name));
            return items;
        }

        internal static List<MemberInfo> GetAllPropertyInfos(object toInspect) {
            if (toInspect == null)
                return new List<MemberInfo>();
            var items = toInspect.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => ShouldRenderProp(p)).Select(p => p as MemberInfo)
                .ToList();

            items.Sort((a, b) => a.Name.CompareTo(b.Name));
            return items;
        }

        internal static List<MemberInfo> GetAllMethodInfos(object toInspect) {
            if (toInspect == null)
                return new List<MemberInfo>();
            var items = toInspect.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => !p.IsSpecialName && p.DeclaringType == toInspect.GetType() && ShouldRenderProp(p)).Select(p => p as MemberInfo)
                .ToList();

            items.Sort((a, b) => a.Name.CompareTo(b.Name));
            return items;
        }

        internal static Delegate[] GetEventDelegates(InspectedObject inspectedObject) {
            Delegate[] delegates = new Delegate[] { };
            if (inspectedObject.MemberInfo is EventInfo eventInfo) {
                var eventField = inspectedObject.Parent.GetType().GetField(eventInfo.Name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);
                if (eventField == null) eventField = inspectedObject.Parent.GetType().GetField(eventInfo.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField);
                if (eventField == null) eventField = inspectedObject.Parent.GetType().GetField(eventInfo.Name, BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField);
                if (eventField == null) eventField = inspectedObject.Parent.GetType().GetField(eventInfo.Name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetField);
                if (eventField == null) eventField = inspectedObject.Parent.GetType().GetField(eventInfo.Name, BindingFlags.GetField);

                if (eventField != null) {
                    var eDel = ((Delegate)eventField.GetValue(inspectedObject.Parent));
                    if (eDel != null) {
                        delegates = eDel.GetInvocationList();
                    }
                }
            }
            return delegates;
        }

        internal static string DetailsDisplayString(object x) {
            if (x == null)
                return "null";
            else if (x.GetType() == typeof(ulong) || x.GetType() == typeof(long))
                return $"{x} (0x{x:X16})";
            else if (x.GetType() == typeof(uint) || x.GetType() == typeof(int))
                return $"{x} (0x{x:X8})";
            else if (x.GetType() == typeof(ushort) || x.GetType() == typeof(short))
                return $"{x} (0x{x:X4})";
            else if (x.GetType() == typeof(byte))
                return $"{x} (0x{x:X2})";
            else if (x.GetType().IsPrimitive)
                return x.ToString();
            else if (x.GetType() == typeof(string))
                return x.ToString();
            else if (x.GetType().IsEnum)
                return x.ToString();
            else if (x is MethodInfo methodInfo) {
                return GetMethodDisplayString(methodInfo);
            }
            else if (x is DateTime) {
                return x.ToString();
            }
            else if (x.GetType().GetMethod("ToString", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) != null) {
                return x.ToString();
            }
            return TypeDisplayString(x.GetType());
        }

        internal static string GetMethodDisplayString(MethodInfo methodInfo, bool friendlyDisplay = true) {
            return $"{methodInfo.Name}{GetMethodInfoGenericTypeArgsDisplayString(methodInfo, friendlyDisplay)}({GetMethodArgumentsDisplayString(methodInfo, friendlyDisplay)})";
        }

        internal static object GetMethodArgumentsDisplayString(MethodInfo methodInfo, bool friendlyDisplay = true) {
            return string.Join(", ", methodInfo.GetParameters().Select(p => $"{TypeDisplayString(p.ParameterType, friendlyDisplay)} {p.Name}").ToArray());
        }

        internal static string GetMethodInfoGenericTypeArgsDisplayString(MethodInfo methodInfo, bool friendlyDisplay = true) {
            var genericArgs = methodInfo.GetGenericArguments();

            if (genericArgs.Length > 0) {
                return $"<{string.Join(", ", genericArgs.Select(a => TypeDisplayString(a, friendlyDisplay)).ToArray())}>";
            }
            return "";
        }

        internal static string TypeDisplayString(Type type, bool friendlyDisplay = true) {
            if (type == null)
                return "null";
            var name = $"{(friendlyDisplay ? "" : $"{type.Namespace}.")}{type.Name.Split('`').First()}";
            if (type.IsGenericType) {
                return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(g => TypeDisplayString(g, friendlyDisplay)).ToArray())}>";
            }
            return name;
        }

        internal static string TreeDisplayString(object x) {
            if (x == null)
                return "null";
            if (x.GetType() == typeof(ulong) || x.GetType() == typeof(long))
                return $"{x} (0x{x:X16})";
            if (x.GetType() == typeof(uint) || x.GetType() == typeof(int))
                return $"{x} (0x{x:X8})";
            if (x.GetType() == typeof(ushort) || x.GetType() == typeof(short))
                return $"{x} (0x{x:X4})";
            if (x.GetType() == typeof(byte))
                return $"{x} (0x{x:X2})";
            if (x.GetType().IsPrimitive)
                return x.ToString();
            if (x.GetType() == typeof(string))
                return x.ToString();
            if (x.GetType().IsEnum)
                return $"{x.GetType().Name}.{x}";
            return x.GetType().Name;
        }

        internal static bool ShouldRenderProp(MemberInfo property) {
            return true;
        }

        internal static bool ShouldRenderTree(Type toInspect) {
            return toInspect != null &&
                !toInspect.IsPrimitive &&
                toInspect != typeof(string) &&
                !toInspect.IsEnum;
        }
        #endregion // Static Helpers

        #region Textures
        private void Hud_CreateTextures(object sender, EventArgs e) {
            CreateTextures();
        }

        private void Hud_DestroyTextures(object sender, EventArgs e) {
            DestroyTextures();
        }

        private void CreateTextures() {
            try {
                CreateTextureFromResource(ref EyeTexture, "UtilityBelt.Resources.icons.eye.png");
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
                DestroyTexture(ref EyeTexture);
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private void DestroyTexture(ref Microsoft.DirectX.Direct3D.Texture texture) {
            texture?.Dispose();
            texture = null;
        }
        #endregion // Textures

        public void Dispose() {
            if (!isDisposed) {
                UBLoader.FilterCore.LogError($"Dispose: Inspector {Name}");
                _selected?.UnsubscribeFromEvents();
                foreach (var monitor in eventMonitors) {
                    monitor?.Dispose();
                }
                eventMonitors.Clear();
                foreach (var inspector in methodInspectors) {
                    inspector?.Dispose();
                }
                methodInspectors.Clear();
                foreach (var inspector in inspectors) {
                    inspector?.Dispose();
                }
                inspectors.Clear();
                DestroyTextures();
                hud.Render -= Hud_Render;
                hud.PreRender -= Hud_PreRender;
                hud.CreateTextures -= Hud_CreateTextures;
                hud.DestroyTextures -= Hud_DestroyTextures;
                hud.ShouldHide -= Hud_ShouldHide;
                hud?.Dispose();
                isDisposed = true;
            }
        }
    }
}
