using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Linq;
using System.Net;
using Decal.Adapter;
using Microsoft.Win32;

namespace UtilityBelt
{
	public static class Util {
        public static string GetVersion() {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

            if (assembly != null) {
                var parts = assembly.GetName().Version.ToString().Split('.');
                var version = "0.0.0";

                if (parts.Length > 3) {
                    version = string.Join(".", parts.Take(3).ToArray());
                }

                return version;
            }

            return "0.0.0";
        }

        public static string GetPluginDirectory() {
            return Path.Combine(GetDecalPluginsDirectory(), Globals.PluginName);
        }

        private static string GetDecalPluginsDirectory() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Decal Plugins");
        }

        public static string GetServerDirectory() {
            return Path.Combine(GetPluginDirectory(), Globals.Core.CharacterFilter.Server);
        }

        public static string GetCharacterDirectory() {
            String path = Path.Combine(GetPluginDirectory(), Globals.Core.CharacterFilter.Server);
            path = Path.Combine(path, Globals.Core.CharacterFilter.Name);
            return path;
        }

        private static string GetLogDirectory() {
            return Path.Combine(GetCharacterDirectory(), "logs");
        }

        public static void CreateDataDirectories() {
            System.IO.Directory.CreateDirectory(GetPluginDirectory());
            System.IO.Directory.CreateDirectory(GetCharacterDirectory());
            System.IO.Directory.CreateDirectory(GetLogDirectory());
        }

        public static void WriteToDebugLog(string message) {
            WriteToLogFile("debug", message, true);
        }

        public static void WriteToLogFile(string logName, string message, bool addTimestamp = false) {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var logFileName = String.Format("{0}.{1}.txt", logName, today);

            if (addTimestamp) {
                message = String.Format("{0} {1}", DateTime.Now.ToString("yy/MM/dd H:mm:ss"), message);
            }

            File.AppendAllText(Path.Combine(Util.GetLogDirectory(), logFileName), message + Environment.NewLine);
        }

        internal static double GetDistance(CoordsObject c1, CoordsObject c2) {
            return Math.Abs(Math.Sqrt(Math.Pow(c1.NorthSouth - c2.NorthSouth, 2) + Math.Pow(c1.EastWest - c2.EastWest, 2))) * 240;
        }

        internal static double GetDistance(Vector3Object v1, Vector3Object v2) {
            return Math.Abs(Math.Sqrt(Math.Pow(v1.X - v2.X, 2) + Math.Pow(v1.Y - v2.Y, 2) + Math.Pow(v1.Z - v2.Z, 2))) * 240;
        }

        public static Object GetPropValue(this Object obj, String name) {
            foreach (String part in name.Split('.')) {
                if (obj == null) { return null; }

                Type type = obj.GetType();
                PropertyInfo info = type.GetProperty(part);
                if (info == null) { return null; }

                obj = info.GetValue(obj, null);
            }
            return obj;
        }

        public static T GetFieldValue<T>(this Object obj, String name) {
            Object retval = GetFieldValue(obj, name);
            //if (retval == null) { return default(T); }

            // throws InvalidCastException if types are incompatible
            return (T)retval;
        }

        public static Object GetFieldValue(this Object obj, String name) {
            foreach (String part in name.Split('.')) {
                if (obj == null) { return null; }

                Type type = obj.GetType();
                FieldInfo info = type.GetField(part);
                if (info == null) { return null; }

                obj = info.GetValue(obj);
            }
            return obj;
        }

        public static T GetPropValue<T>(this Object obj, String name) {
            Object retval = GetPropValue(obj, name);
            //if (retval == null) { return default(T); }

            // throws InvalidCastException if types are incompatible
            return (T)retval;
        }

        public static void WriteToChat(string message)
		{
			try
			{
				Globals.Host.Actions.AddChatText("[" + Globals.PluginName + "] " + message, 5);
                WriteToDebugLog(message);
			}
			catch (Exception ex) { Logger.LogException(ex); }
		}

        public static int GetFreeMainPackSpace() {
            WorldObject mainPack = Globals.Core.WorldFilter[Globals.Core.CharacterFilter.Id];

            return GetFreePackSpace(mainPack);
        }

        public static int GetFreePackSpace(WorldObject container) {
            int packSlots = container.Values(LongValueKey.ItemSlots, 0);

            // side pack count
            if (container.Id != Globals.Core.CharacterFilter.Id) {
                return packSlots - Globals.Core.WorldFilter.GetByContainer(container.Id).Count;
            }

            // main pack count
            foreach (var wo in Globals.Core.WorldFilter.GetByContainer(container.Id)) {
                if (wo != null) {
                    // skip packs
                    if (wo.ObjectClass == ObjectClass.Container) continue;

                    // skip foci
                    if (wo.ObjectClass == ObjectClass.Foci) continue;

                    // skip equipped
                    if (wo.Values(LongValueKey.EquippedSlots, 0) > 0) continue;

                    // skip wielded
                    if (wo.Values(LongValueKey.Slot, -1) == -1) continue;

                    --packSlots;
                }
            }

            return packSlots;
        }

        internal static string GetVTankProfilesDirectory() {
            var defaultPath = @"C:\Games\VirindiPlugins\VirindiTank\";
            try {
                var key = @"HKEY_CURRENT_USER\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Decal\Plugins\{642F1F48-16BE-48BF-B1D4-286652C4533E}";
                string path = (string)Registry.GetValue(key, "ProfilePath", "");

                if (string.IsNullOrEmpty(path)) {
                    key = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Decal\Plugins\{642F1F48-16BE-48BF-B1D4-286652C4533E}";
                    path = (string)Registry.GetValue(key, "ProfilePath", "");
                }

                if (string.IsNullOrEmpty(path)) {
                    return defaultPath;
                }

                return path;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return defaultPath;
        }

        internal static void StackItem(WorldObject stackThis) {
            // try to stack in side pack
            foreach (var container in Globals.Core.WorldFilter.GetInventory()) {
                if (container.ObjectClass == ObjectClass.Container && container.Values(LongValueKey.Slot, -1) >= 0) {
                    foreach (var wo in Globals.Core.WorldFilter.GetByContainer(container.Id)) {
                        if (wo.Name == stackThis.Name && wo.Id != stackThis.Id) {
                            if (wo.Values(LongValueKey.StackCount, 1) + stackThis.Values(LongValueKey.StackCount, 1) <= wo.Values(LongValueKey.StackMax)) {
                                Globals.Core.Actions.SelectItem(stackThis.Id);
                                Globals.Core.Actions.MoveItem(stackThis.Id, container.Id, container.Values(LongValueKey.Slot), true);
                                return;
                            }
                        }
                    }
                }
            }

            // try to stack in main pack
            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (wo.Container == Globals.Core.CharacterFilter.Id) {
                    if (wo.Name == stackThis.Name && wo.Id != stackThis.Id) {
                        if (wo.Values(LongValueKey.StackCount, 1) + stackThis.Values(LongValueKey.StackCount, 1) <= wo.Values(LongValueKey.StackMax)) {
                            Globals.Core.Actions.SelectItem(stackThis.Id);
                            Globals.Core.Actions.MoveItem(stackThis.Id, Globals.Core.CharacterFilter.Id, 0, true);
                            return;
                        }
                    }
                }
            }
        }

        internal static int GetItemCountInInventoryByName(string name) {
            int count = 0;

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (wo.Name == name) {
                    if (wo.Values(LongValueKey.StackCount, 0) > 0) {
                        count += wo.Values(LongValueKey.StackCount);
                    }
                    else {
                        ++count;
                    }
                }
            }

            return count;
        }
        internal static bool IsItemSafeToGetRidOf(int id) {
            return IsItemSafeToGetRidOf(CoreManager.Current.WorldFilter[id]);
        }

        internal static bool IsItemSafeToGetRidOf(WorldObject wo) {
            if (wo == null) return false;

            // skip packs
            if (wo.ObjectClass == ObjectClass.Container) return false;

            // skip foci
            if (wo.ObjectClass == ObjectClass.Foci) return false;

            // skip equipped
            if (wo.Values(LongValueKey.EquippedSlots, 0) > 0) return false;

            // skip wielded
            if (wo.Values(LongValueKey.Slot, -1) == -1) return false;

            // skip tinkered
            if (wo.Values(LongValueKey.NumberTimesTinkered, 0) > 1) return false;

            // skip imbued
            if (wo.Values(LongValueKey.Imbued, 0) > 1) return false;

            // skip inscribed
            if (!string.IsNullOrEmpty(wo.Values(StringValueKey.Inscription))) return false;

            return true;
        }

        internal static int PyrealCount() {
            int total = 0;

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (wo.Values(LongValueKey.Type, 0) == 273/* pyreals */) {
                    total += wo.Values(LongValueKey.StackCount, 1);
                }
            }

            return total;
        }


        [DllImport("Decal.dll")]
        static extern int DispatchOnChatCommand(ref IntPtr str, [MarshalAs(UnmanagedType.U4)] int target);

        public static bool Decal_DispatchOnChatCommand(string cmd) {
            IntPtr bstr = Marshal.StringToBSTR(cmd);

            try {
                bool eaten = (DispatchOnChatCommand(ref bstr, 1) & 0x1) > 0;

                return eaten;
            }
            finally {
                Marshal.FreeBSTR(bstr);
            }
        }

        /// <summary>
        /// This will first attempt to send the messages to all plugins. If no plugins set e.Eat to true on the message, it will then simply call InvokeChatParser.
        /// </summary>
        /// <param name="cmd"></param>
        public static void DispatchChatToBoxWithPluginIntercept(string cmd) {
            if (!Decal_DispatchOnChatCommand(cmd))
                Globals.Core.Actions.InvokeChatParser(cmd);
        }

        internal static string GetTilePath() {
            string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            return Path.Combine(Path.Combine(assemblyFolder, "Resources"), "tiles");
        }

        internal static void Think(string message) {
            try {
                DispatchChatToBoxWithPluginIntercept(string.Format("/tell {0}, {1}", Globals.Core.CharacterFilter.Name, message));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public static string GetObjectName(int id) {
            if (!Globals.Core.Actions.IsValidObject(id)) {
                return string.Format("<{0}>", id);
            }
            var wo = Globals.Core.WorldFilter[id];

            if (wo == null) return string.Format("<{0}>", id);

            if (wo.Values(LongValueKey.Material, 0) > 0) {
                FileService service = Globals.Core.Filter<FileService>();
                return string.Format("{0} {1}", service.MaterialTable.GetById(wo.Values(LongValueKey.Material, 0)), wo.Name); 
            }
            else {
                return string.Format("{0}", wo.Name);
            }
        }

        public static Point RotatePoint(Point pointToRotate, Point centerPoint, double angleInDegrees) {
            double angleInRadians = angleInDegrees * (Math.PI / 180);
            double cosTheta = Math.Cos(angleInRadians);
            double sinTheta = Math.Sin(angleInRadians);
            return new Point {
                X =
                    (int)
                    (cosTheta * (pointToRotate.X - centerPoint.X) -
                    sinTheta * (pointToRotate.Y - centerPoint.Y) + centerPoint.X),
                Y =
                    (int)
                    (sinTheta * (pointToRotate.X - centerPoint.X) +
                    cosTheta * (pointToRotate.Y - centerPoint.Y) + centerPoint.Y)
            };
        }

        internal static string GetQTXMLPath() {
            string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            return Path.Combine(assemblyFolder, "Resources");
        }

        public static Dictionary<string, string> questKeyLookup = new Dictionary<string, string>();

        public static void LoadQuestLookupXML() {
            try {
                string filePath = Path.Combine(GetQTXMLPath(), "quests.xml");

                if (!File.Exists(filePath)) {
                    Util.WriteToChat("Unable to find lookup file: " + filePath);
                    return;
                }

                using (XmlReader reader = XmlReader.Create(filePath)) {
                    while (reader.Read()) {
                        if (reader.IsStartElement() && reader.Name != "root") {
                            questKeyLookup.Add(reader.Name.ToLower(), reader.ReadElementContentAsString());
                        }
                    }
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        public static string GetFriendlyQuestName(string questKey) {
            if (questKeyLookup.Keys.Contains(questKey)) {
                return questKeyLookup[questKey];
            }

            return questKey;
        }

        public static bool CompareFiles(string path1, string path2) {
            byte[] file1 = File.ReadAllBytes(path1);
            byte[] file2 = File.ReadAllBytes(path2);
            if (file1.Length == file2.Length) {
                for (int i = 0; i < file1.Length; i++) {
                    if (file1[i] != file2[i]) {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }
}
