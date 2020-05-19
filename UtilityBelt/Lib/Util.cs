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
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.DirectX;
using UtilityBelt.Lib;

namespace UtilityBelt
{
    public static class Util {
        private static string pluginDirectory;
        private static UtilityBeltPlugin UB;
        public static void Init(UtilityBeltPlugin ub, string assemblyLocation, string storagePath) {
            UB = ub;
            AssemblyLocation = assemblyLocation;
            pluginDirectory = storagePath;
        }

        public static string AssemblyLocation = "";
        public static string AssemblyDirectory { get { return System.IO.Path.GetDirectoryName(AssemblyLocation); } }

        private static FileService fileService = null;
        public static FileService FileService {
            get {
                if (fileService == null) {
                    fileService = CoreManager.Current.Filter<FileService>();
                }

                return fileService;
            }
        }

        private static readonly Regex releaseBranchVersion = new Regex(@"^\d+\.\d+.\d+\.release");
        public static string GetVersion(bool includeGitExtras=false) {
            try {
                var productVersion = FileVersionInfo.GetVersionInfo(AssemblyLocation).ProductVersion;

                // show the short version for release branch builds
                if (releaseBranchVersion.IsMatch(productVersion) && !includeGitExtras) {
                    return FileVersionInfo.GetVersionInfo(AssemblyLocation).FileVersion;
                }
                else {
                    return productVersion;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return "0.0.0";
        }

        public static bool IsReleaseVersion() {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetAssembly(typeof(UtilityBeltPlugin));

            if (assembly != null) {
                var productVersion = FileVersionInfo.GetVersionInfo(AssemblyLocation).ProductVersion;
                return releaseBranchVersion.IsMatch(productVersion);
            }

            return false;
        }

        public static string GetPluginDirectory() {
            return pluginDirectory;
        }

        public static string GetServerDirectory() {
            return Path.Combine(GetPluginDirectory(), UB.ServerName);
        }

        public static string GetCharacterDirectory() {
            String path = Path.Combine(GetPluginDirectory(), UB.ServerName);
            path = Path.Combine(path, UB.CharacterName);
            return path;
        }

        private static string GetLogDirectory() {
            return Path.Combine(GetCharacterDirectory(), "logs");
        }

        internal static string GetResourcesDirectory() {
            return Path.Combine(AssemblyDirectory, "Resources");
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

        internal static int UnixTimeStamp() {
            return (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        internal static double GetDistance(CoordsObject c1, CoordsObject c2) {
            return Math.Abs(Math.Sqrt(Math.Pow(c1.NorthSouth - c2.NorthSouth, 2) + Math.Pow(c1.EastWest - c2.EastWest, 2))) * 240;
        }

        internal static double GetDistance(Vector3Object v1, Vector3Object v2) {
            return Math.Abs(Math.Sqrt(Math.Pow(v1.X - v2.X, 2) + Math.Pow(v1.Y - v2.Y, 2) + Math.Pow(v1.Z - v2.Z, 2))) * 240;
        }

        internal static double GetDistance(Vector3 v1, Vector3 v2) {
            return Math.Abs(Math.Sqrt(Math.Pow(v1.X - v2.X, 2) + Math.Pow(v1.Y - v2.Y, 2) + Math.Pow(v1.Z - v2.Z, 2))) * 240;
        }

        internal static int GetChatId() {
            return UB.PluginName.GetHashCode() & int.MaxValue;
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

        public static int GetOverallSlot(WorldObject wo) { //Just for sorting for now. TODO: math real numbers, based on actual pack slots
            if (wo.Container == UB.Core.CharacterFilter.Id)
                return wo.Values(LongValueKey.Slot, 0);
            if (wo.Container == 0 || UB.Core.WorldFilter[wo.Container] == null)
                return int.MaxValue;
            return wo.Values(LongValueKey.Slot, 0) + 1000 + (100 * UB.Core.WorldFilter[wo.Container].Values(LongValueKey.Slot, 0));
        }
        public static string GetItemLocation(int id) {
            if (id == 0 || UB.Core.WorldFilter[id] == null)
                return "Does Not Exist";
            WorldObject wo = UB.Core.WorldFilter[id];
            if (wo.Container == 0)
                return wo.Coordinates().ToString();
            string location = "";
            if (wo.Container == UB.Core.CharacterFilter.Id)
                return $"Main Pack";
            wo = UB.Core.WorldFilter[wo.Container];
            if (wo != null && wo.Container != 0) {
                location = $"{wo.Name} #{1 + wo.Values(LongValueKey.Slot, 0)} => {location}";
                wo = UB.Core.WorldFilter[wo.Container];
            }
            if (wo != null && wo.Id != UB.Core.CharacterFilter.Id)
                location = $"{wo.Name}[{wo.Coordinates().ToString()}] => {location}";
            return location.Substring(0, location.Length - 4);
        }
        internal static string GetVTankProfilesDirectory() {
            var defaultPath = @"C:\Games\VirindiPlugins\VirindiTank\";
            try {
                var key = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Decal\Plugins\{642F1F48-16BE-48BF-B1D4-286652C4533E}";
                var path = (string)Registry.GetValue(key, "ProfilePath", "");

                if (string.IsNullOrEmpty(path)) {
                    key = @"HKEY_CURRENT_USER\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Decal\Plugins\{642F1F48-16BE-48BF-B1D4-286652C4533E}";
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

        internal static int GetItemCountInInventoryByName(string name) {
            int count = 0;

            using (var inv = UB.Core.WorldFilter.GetInventory()) {
                foreach (var wo in inv) {
                    if (wo.Name == name) {
                        if (wo.Values(LongValueKey.StackCount, 0) > 0) {
                            count += wo.Values(LongValueKey.StackCount);
                        }
                        else {
                            ++count;
                        }
                    }
                }
            }

            return count;
        }

        internal static int GetItemCountInInventoryByObjectClass(ObjectClass objectClass) {
            int count = 0;

            using (var inv = UB.Core.WorldFilter.GetInventory()) {
                foreach (var wo in inv) {
                    if (wo.ObjectClass == objectClass) {
                        if (wo.Values(LongValueKey.StackCount, 0) > 0) {
                            count += wo.Values(LongValueKey.StackCount);
                        }
                        else {
                            ++count;
                        }
                    }
                }
            }

            return count;
        }

        internal static bool IsItemSafeToGetRidOf(int id) {
            return IsItemSafeToGetRidOf(UB.Core.WorldFilter[id]);
        }

        internal static bool IsItemSafeToGetRidOf(WorldObject wo) {
            if (wo == null) return false;

            // skip attuned
            if (wo.Values(LongValueKey.Attuned, 0) > 1) return false;

            // skip retained
            if (wo.Values(BoolValueKey.Retained, false) == true) return false;

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

            using (var inv = UB.Core.WorldFilter.GetInventory()) {
                foreach (var wo in inv) {
                    if (wo.Values(LongValueKey.Type, 0) == 273/* pyreals */) {
                        total += wo.Values(LongValueKey.StackCount, 1);
                    }
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
                CoreManager.Current.Actions.InvokeChatParser(cmd);
        }

        internal static string GetTilePath() {
            return Path.Combine(Path.Combine(AssemblyDirectory, "Resources"), "tiles");
        }

        internal static void Think(string message) {
            try {
                DispatchChatToBoxWithPluginIntercept(string.Format("/tell {0}, {1}", UB.Core.CharacterFilter.Name, message));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal static void ThinkOrWrite(string message, bool think=false) {
            try {
                if (think) {
                    DispatchChatToBoxWithPluginIntercept(string.Format("/tell {0}, {1}", UB.Core.CharacterFilter.Name, message));
                }
                else {
                    Logger.WriteToChat(string.Format("{0}", message));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public static string GetObjectName(int id) {
            if (!UB.Core.Actions.IsValidObject(id)) {
                return string.Format("<{0}>", id);
            }
            var wo = CoreManager.Current.WorldFilter[id];

            if (wo == null) return string.Format("<{0}>", id);

            if (wo.Values(LongValueKey.Material, 0) > 0) {
                FileService service = UB.Core.Filter<FileService>();
                var material = service.MaterialTable.GetById(wo.Values(LongValueKey.Material, 0));

                // this accounts for gems, where the material is the same as the name.  That way we don't
                // get names like Jet Jet or White Sapphire White Sapphire
                if (material.Name == wo.Name || wo.Name.Contains(material.Name)) {
                    return wo.Name.Trim();
                }
                else {
                    return string.Format("{0} {1}", material.Name.Trim(), wo.Name.Trim());
                }
            }
            else {
                return string.Format("{0}", wo.Name.Trim());
            }
        }

        //Do not use this in a loop, it gets an F for eFFiciency.
        public static WorldObject FindName(string searchname, bool partial, ObjectClass[] oc) {

            //try int id
            if (int.TryParse(searchname, out int id)) {
                if (UB.Core.WorldFilter[id] != null && CheckObjectClassArray(UB.Core.WorldFilter[id].ObjectClass, oc)) {
                    // Util.WriteToChat("Found by id");
                    return UB.Core.WorldFilter[id];
                }
            }
            //try hex...
            try {
                int intValue = Convert.ToInt32(searchname, 16);
                if (UB.Core.WorldFilter[intValue] != null && CheckObjectClassArray(UB.Core.WorldFilter[intValue].ObjectClass, oc)) {
                    // Util.WriteToChat("Found vendor by hex");
                    return UB.Core.WorldFilter[intValue];
                }
            }
            catch { }

            searchname = searchname.ToLower();

            //try "selected"
            if (searchname.Equals("selected") && UB.Core.Actions.CurrentSelection != 0 && UB.Core.WorldFilter[UB.Core.Actions.CurrentSelection] != null && CheckObjectClassArray(UB.Core.WorldFilter[UB.Core.Actions.CurrentSelection].ObjectClass, oc)) {
                return UB.Core.WorldFilter[UB.Core.Actions.CurrentSelection];
            }
            //try slow search...
            WorldObject found = null;

            double lastDistance = double.MaxValue;
            double thisDistance;
            using (var woc = CoreManager.Current.WorldFilter.GetLandscape()) {
                foreach (WorldObject thisOne in woc) {
                    if (!CheckObjectClassArray(thisOne.ObjectClass, oc)) continue;
                    thisDistance = UB.Core.WorldFilter.Distance(CoreManager.Current.CharacterFilter.Id, thisOne.Id);
                    if (thisOne.Id != UB.Core.CharacterFilter.Id && (found == null || lastDistance > thisDistance)) {
                        string thisLowerName = thisOne.Name.ToLower();
                        if (partial && thisLowerName.Contains(searchname) && CheckObjectClassArray(thisOne.ObjectClass, oc)) {
                            found = thisOne;
                            lastDistance = thisDistance;
                        }
                        else if (thisLowerName.Equals(searchname) && CheckObjectClassArray(thisOne.ObjectClass, oc)) {
                            found = thisOne;
                            lastDistance = thisDistance;
                        }
                    }
                }
            }
            return found;
        }

        public static WorldObject FindClosestByObjectClass(ObjectClass objectclass) {
            WorldObject closest = null;
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetByObjectClass(objectclass);
            var closestDistance = float.MaxValue;
            foreach (var wo in wos) {
                if (PhysicsObject.GetDistance(wo.Id) < closestDistance) {
                    closest = wo;
                    closestDistance = PhysicsObject.GetDistance(wo.Id);
                }
            }
            wos.Dispose();

            return closest;
        }

        private static bool CheckObjectClassArray(ObjectClass needle, ObjectClass[] haystack) {
            if (haystack.Length == 0) return true;
            foreach (ObjectClass o in haystack)
                if (needle == o) return true;
            return false;
        }


        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp) {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp);
            return dtDateTime;
        }

        public static string GetFriendlyTimeDifference(TimeSpan difference) {
            string output = "";

            if (difference.Days > 0) output += difference.Days.ToString() + "d ";
            if (difference.Hours > 0) output += difference.Hours.ToString() + "h ";
            if (difference.Minutes > 0) output += difference.Minutes.ToString() + "m ";
            if (difference.Seconds > 0) output += difference.Seconds.ToString() + "s ";

            if (output.Length == 0)
                return "0s";
            return output.Trim();
        }

        public static string GetFriendlyTimeDifference(long difference) {
            return GetFriendlyTimeDifference(TimeSpan.FromSeconds(difference));
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

        public static PointF RotatePoint(PointF pointToRotate, PointF centerPoint, double angleInDegrees) {
            double angleInRadians = angleInDegrees * (Math.PI / 180f);
            double cosTheta = Math.Cos(angleInRadians);
            double sinTheta = Math.Sin(angleInRadians);
            return new PointF {
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

        public static PointF MovePoint(PointF point, double degrees, double distance) {
            var rad = degrees * Math.PI / 180;

            return new PointF(
                (float)(point.X + (distance * Math.Cos(rad))),
                (float)(point.Y + (distance * Math.Sin(rad)))
            );
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

        public static int GetDamerauLevenshteinDistance(string one, string two) {
            var bounds = new { Height = one.Length + 1, Width = two.Length + 1 };

            int[,] matrix = new int[bounds.Height, bounds.Width];

            for (int height = 0; height < bounds.Height; height++) { matrix[height, 0] = height; };
            for (int width = 0; width < bounds.Width; width++) { matrix[0, width] = width; };

            for (int height = 1; height < bounds.Height; height++) {
                for (int width = 1; width < bounds.Width; width++) {
                    int cost = (one[height - 1] == two[width - 1]) ? 0 : 1;
                    int insertion = matrix[height, width - 1] + 1;
                    int deletion = matrix[height - 1, width] + 1;
                    int substitution = matrix[height - 1, width - 1] + cost;

                    int distance = Math.Min(insertion, Math.Min(deletion, substitution));

                    if (height > 1 && width > 1 && one[height - 1] == two[width - 2] && one[height - 2] == two[width - 1]) {
                        distance = Math.Min(distance, matrix[height - 2, width - 2] + cost);
                    }

                    matrix[height, width] = distance;
                }
            }

            return matrix[bounds.Height - 1, bounds.Width - 1];
        }
        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int width, int height, int wFlags);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

        public struct Rect {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        #region IsClientActive()
        public static bool IsClientActive() {
            return GetForegroundWindow() == UB.Core.Decal.Hwnd;
        }
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        #endregion

    }
}
