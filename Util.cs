using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace UtilityBelt
{
	public static class Util
	{
        public static string GetPluginDirectory() {
            return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\Decal Plugins\" + Globals.PluginName + @"\";
        }

        public static string GetCharacterDirectory() {
            return GetPluginDirectory() + Globals.Core.CharacterFilter.Server + @"\" + Globals.Core.CharacterFilter.Name + @"\";
        }

        private static string GetLogDirectory() {
            return GetCharacterDirectory() + @"logs\";
        }

        public static void CreateDataDirectories() {
            System.IO.Directory.CreateDirectory(GetPluginDirectory());
            System.IO.Directory.CreateDirectory(GetCharacterDirectory());
            System.IO.Directory.CreateDirectory(GetLogDirectory());
        }

        public static void LogException(Exception ex)
		{
			try
			{
				using (StreamWriter writer = new StreamWriter(GetPluginDirectory() + "exceptions.txt", true))
				{
					writer.WriteLine("============================================================================");
					writer.WriteLine(DateTime.Now.ToString());
					writer.WriteLine("Error: " + ex.Message);
					writer.WriteLine("Source: " + ex.Source);
					writer.WriteLine("Stack: " + ex.StackTrace);
					if (ex.InnerException != null)
					{
						writer.WriteLine("Inner: " + ex.InnerException.Message);
						writer.WriteLine("Inner Stack: " + ex.InnerException.StackTrace);
					}
					writer.WriteLine("============================================================================");
					writer.WriteLine("");
					writer.Close();
				}
			}
			catch
			{
			}
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

            File.AppendAllText(Util.GetLogDirectory() + logFileName, message + Environment.NewLine);
        }

        public static void WriteToChat(string message)
		{
			try
			{
				Globals.Host.Actions.AddChatText("[" + Globals.PluginName + "] " + message, 5);
                WriteToDebugLog(message);
			}
			catch (Exception ex) { LogException(ex); }
		}

        public static int GetFreeMainPackSpace() {
            WorldObject mainPack = Globals.Core.WorldFilter[Globals.Core.CharacterFilter.Id];

            return GetFreePackSpace(mainPack);
        }

        public static int GetFreePackSpace(WorldObject container) {
            int packSlots = container.Values(LongValueKey.ItemSlots, 0);

            using (IEnumerator<WorldObject> enumerator = Globals.Core.WorldFilter.GetInventory().GetEnumerator()) {
                while (enumerator.MoveNext()) {
                    if (enumerator.Current != null) {
                        // skip packs
                        if (enumerator.Current.ObjectClass == ObjectClass.Container) continue;

                        // skip foci
                        if (enumerator.Current.ObjectClass == ObjectClass.Foci) continue;

                        // skip equipped
                        if (enumerator.Current.Values(LongValueKey.EquippedSlots, 0) > 0) continue;

                        // skip wielded
                        if (enumerator.Current.Values(LongValueKey.Slot, -1) == -1) continue;

                        if (enumerator.Current.Container == container.Id) {
                            --packSlots;
                        }
                    }
                }
            }

            return packSlots;
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

        internal static bool ItemIsSafeToGetRidOf(WorldObject wo) {
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

            return true;
        }

        internal static int PyrealCount() {
            int total = 0;

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (wo.Name == "Pyreal") {
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

        internal static void Think(string message) {
            try {
                DispatchChatToBoxWithPluginIntercept(string.Format("/tell {0}, {1}", Globals.Core.CharacterFilter.Name, message));
            }
            catch (Exception ex) { Util.LogException(ex); }
        }
    }
}
