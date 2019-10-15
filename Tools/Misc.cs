using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using UtilityBelt.Lib.VendorCache;
using UtilityBelt.Views;
using VirindiViewService.Controls;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Reflection;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Constants;
using Decal.Filters;
using UtilityBelt.Lib.Settings;

namespace UtilityBelt.Tools {
    public class OptionResult {
        public object Object;
        public object Parent;
        public PropertyInfo Property;

        public OptionResult(object obj, PropertyInfo propertyInfo, object parent) {
            Object = obj;
            Parent = parent;
            Property = propertyInfo;
        }
    }

    public class Misc : IDisposable {
        private DateTime vendorTimestamp = DateTime.MinValue;
        private int vendorOpening = 0;
        private static WorldObject vendor = null;

        private DateTime portalTimestamp = DateTime.MinValue;
        private int portalAttempts = 0;
        private static WorldObject portal = null;
        //yes, I know I'm repeating myself. each objectclass has different catches; and combining them would just be a maze of if's

        private const int VENDOR_OPEN_TIMEOUT = 5000;

        private bool disposed = false;

        public Misc() {
            try {
                Globals.Core.CommandLineText += Current_CommandLineText;
                Globals.Core.WorldFilter.ApproachVendor += WorldFilter_ApproachVendor;
                Globals.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private static readonly Regex regex = new Regex(@"^\/ub (?<command>\w+)( )?(?<params>.*)?");
        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.ToLower().StartsWith("/ub ")) {
                    Match match = regex.Match(e.Text);
                    if (!match.Success) {
                        return;
                    }
                    e.Eat = true;
                    switch (match.Groups["command"].Value.ToLower()) {
                        case "help":
                            UB_help();
                            break;
                        case "testblock":
                            UB_testBlock(match.Groups["params"].Value);
                            break;
                        case "vendor":
                            UB_vendor(match.Groups["params"].Value);
                            break;
                        case "portal":
                            UB_portal(match.Groups["params"].Value, false);
                            break;
                        case "portalp":
                            UB_portal(match.Groups["params"].Value, true);
                            break;
                        case "closestportal":
                            UB_portal(null, false);
                            break;
                        case "opt":
                            UB_opt(match.Groups["params"].Value);
                            break;
                        case "pos":
                            UB_pos(match.Groups["params"].Value);
                            break;
                        case "door":
                            UB_door(match.Groups["params"].Value);
                            break;
                        case "useflags":
                            UB_useflags(match.Groups["params"].Value);
                            break;
                        case "propertydump":
                            UB_propertydump(match.Groups["params"].Value);
                            break;
                    }
                    // Util.WriteToChat("UB called with command <" + match.Groups["command"].Value + ">, params <" + match.Groups["params"].Value+">");

                    return;
                }
                else if (e.Text.ToLower().Equals("/ub")) {
                    e.Eat = true;
                    UB();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void UB() {
            Util.WriteToChat("UtilityBelt v"+Util.GetVersion()+" by trevis type /ub help for a list of commands");
        }
        public void UB_help() {
            Util.WriteToChat("UtilityBelt Commands: \n" +
                "   /ub - Lists the version number\n" +
                "   /ub help - you are here.\n" +
                "   /ub opt {get,set,list} [option_name] [value] - get/set config options\n" +
                "   /ub testblock <int> <duration> - test Decision_Lock parameters (potentially dangerous)\n" +
                "   /ub portal[p] <portalname> - use the named portal\n" +
                "   /ub closestportal - use the closest portal\n" +
                "   /ub vendor {buyall,sellall,clearbuy,clearsell}\n" +
                "   /ub vendor open[p] {vendorname,vendorid,vendorhex}\n" +
                "   /ub vendor openclosest - attempts to open the closest vendor\n" +
                "   /ub vendor opencancel - quietly cancels the last /ub vendor open* command\n" +
                "TODO: Add rest of commands");
        }
        public void UB_testBlock(string theRest) {
            string[] rest = theRest.Split(' ');
            if (theRest.Length == 0
                || rest.Length != 2
                || !int.TryParse(rest[0], out int num)
                || num < 0
                || num > 18
                || !int.TryParse(rest[1], out int durat)
                || durat < 1
                || durat > 300000) {
                Util.WriteToChat("Usage: /ub testblock <int> <duration>");
                return;
            }
            Util.WriteToChat("Attempting: VTankControl.Decision_Lock((uTank2.ActionLockType)" + num + ", TimeSpan.FromMilliseconds(" + durat + "));");
            VTankControl.Decision_Lock((uTank2.ActionLockType)num, TimeSpan.FromMilliseconds(durat));
        }
        public void UB_vendor(string parameters) {
            char[] stringSplit = { ' ' };
            string[] parameter = parameters.Split(stringSplit, 2);
            if (parameter.Length == 0) {
                Util.WriteToChat("Usage: /ub vendor {open[p] {vendorname,vendorid,vendorhex},openclosest,opencancel,buyall,sellall,clearbuy,clearsell}");
                return;
            }
            switch (parameter[0])
            {
                case "buy":
                case "buyall":
                    CoreManager.Current.Actions.VendorBuyAll();
                    break;

                case "sell":
                case "sellall":
                    CoreManager.Current.Actions.VendorSellAll();
                    break;

                case "clearbuy":
                    CoreManager.Current.Actions.VendorClearBuyList();
                    break;

                case "clearsell":
                    CoreManager.Current.Actions.VendorClearSellList();
                    break;

                case "open":
                    if (parameter.Length != 2) {
                        Util.WriteToChat("Usage: /ub vendor open[p] {vendorname,vendorid}");
                        return;
                    }
                    UB_vendor_open(parameter[1], false);
                    break;

                case "openp":
                    if (parameter.Length != 2) {
                        Util.WriteToChat("Usage: /ub vendor open[p] {vendorname,vendorid}");
                        return;
                    }
                    UB_vendor_open(parameter[1], false);
                    break;
                case "openclosest":
                    UB_vendor_open(null, false);
                    break;
                case "opencancel":
                    Globals.Core.Actions.FaceHeading(Globals.Core.Actions.Heading, true);
                    vendor = null;
                    vendorOpening = 0;
                    VTankControl.Nav_UnBlock();
                    break;
            }
        }
        public void UB_portal(string portalName, bool partial) {
            if (portalName == null) {
                double lastDistance = double.MaxValue;
                double thisDistance;
                foreach (WorldObject thisOne in Globals.Core.WorldFilter.GetByObjectClass(ObjectClass.Portal)) {
                    thisDistance = Globals.Core.WorldFilter.Distance(CoreManager.Current.CharacterFilter.Id, thisOne.Id);
                    if (portal == null || lastDistance > thisDistance) {
                        portal = thisOne;
                        lastDistance = thisDistance;
                    }
                }
            } else {
                portal = FindName(portalName, partial);
            }
            if (portal != null) {
                UsePortal();
                return;
            }
            Util.ThinkOrWrite("Could not find a portal", Globals.Settings.Plugin.portalThink);

        }


        private void UB_vendor_open(string vendorname, bool partial) {
            if (vendorname == null) {
                double lastDistance = double.MaxValue;
                double thisDistance;
                foreach (WorldObject thisOne in Globals.Core.WorldFilter.GetByObjectClass(ObjectClass.Vendor)) {
                    thisDistance = Globals.Core.WorldFilter.Distance(CoreManager.Current.CharacterFilter.Id, thisOne.Id);
                    if (vendor == null || lastDistance > thisDistance) {
                        vendor = thisOne;
                        lastDistance = thisDistance;
                    }
                }
            } else {
                vendor = FindName(vendorname, partial);
            }
            if (vendor != null) {
                OpenVendor();
                return;
            }
            Util.ThinkOrWrite("AutoVendor failed to open vendor", Globals.Settings.AutoVendor.Think);
        }

        private void UB_pos(string value) {
            var selected = Globals.Core.Actions.CurrentSelection;

            if (selected == 0 || !Globals.Core.Actions.IsValidObject(selected)) {
                Util.WriteToChat("pos: No object selected");
                return;
            }

            var wo = Globals.Core.WorldFilter[selected];

            if (wo == null) {
                Util.WriteToChat("pos: null object selected");
                return;
            }

            var phys = PhysicsObject.FromId(selected);

            Util.WriteToChat($"Offset: {wo.Offset()}");
            Util.WriteToChat($"Coords: {wo.Coordinates()}");
            Util.WriteToChat($"RawCoords: {wo.RawCoordinates()}"); //same as offset?
            Util.WriteToChat($"Phys lb: {phys.Landblock.ToString("X8")}");
            Util.WriteToChat($"Phys pos: x:{phys.Position.X} y:{phys.Position.Y} z:{phys.Position.Z}");
            Util.WriteToChat($"Phys heading: x:{phys.Heading.X} y:{phys.Position.Y} z:{phys.Position.Z}");
        }

        private void UB_door(string value) {
            var selected = Globals.Core.Actions.CurrentSelection;

            if (selected == 0 || !Globals.Core.Actions.IsValidObject(selected)) {
                Util.WriteToChat("door: No object selected");
                return;
            }

            var wo = Globals.Core.WorldFilter[selected];

            if (wo == null) {
                Util.WriteToChat("door: null object selected");
                return;
            }

            Util.WriteToChat($"Door is {(wo.Values(BoolValueKey.Open, false) ? "open" : "closed")}");
        }

        private void UB_useflags(string value) {
            var selected = Globals.Core.Actions.CurrentSelection;

            if (selected == 0 || !Globals.Core.Actions.IsValidObject(selected)) {
                Util.WriteToChat("useflags: No object selected");
                return;
            }

            var wo = Globals.Core.WorldFilter[selected];

            if (wo == null) {
                Util.WriteToChat("useflags: null object selected");
                return;
            }

            var itemUseabilityFlags = wo.Values(LongValueKey.Unknown10, 0);

            Util.WriteToChat($"UseFlags for {wo.Name} ({itemUseabilityFlags})");

            foreach (UseFlag v in Enum.GetValues(typeof(UseFlag))) {
                if ((itemUseabilityFlags & (int)v) != 0) {
                    Util.WriteToChat($"Has UseFlag: {v.ToString()}");
                }
            }
        }

        private void UB_propertydump(string value) {
            var selected = Globals.Core.Actions.CurrentSelection;

            if (selected == 0 || !Globals.Core.Actions.IsValidObject(selected)) {
                Util.WriteToChat("propertydump: No object selected");
                return;
            }

            var wo = Globals.Core.WorldFilter[selected];

            if (wo == null) {
                Util.WriteToChat("propertydump: null object selected");
                return;
            }

            Util.WriteToChat($"Property Dump for {wo.Name}");

            Util.WriteToChat($"Id = {wo.Id} (0x{wo.Id.ToString("X8")})");
            Util.WriteToChat($"Name = {wo.Name}");
            Util.WriteToChat($"ActiveSpellCount = {wo.ActiveSpellCount}");
            Util.WriteToChat($"Category = {wo.Category}");
            Util.WriteToChat($"Coordinates = {wo.Coordinates()}");
            Util.WriteToChat($"GameDataFlags1 = {wo.GameDataFlags1}");
            Util.WriteToChat($"HasIdData = {wo.HasIdData}");
            Util.WriteToChat($"LastIdTime = {wo.LastIdTime}");
            Util.WriteToChat($"ObjectClass = {wo.ObjectClass} ({(int)wo.ObjectClass})");
            Util.WriteToChat($"Offset = {wo.Offset()}");
            Util.WriteToChat($"Orientation = {wo.Orientation()}");
            Util.WriteToChat($"RawCoordinates = {wo.RawCoordinates()}");
            Util.WriteToChat($"SpellCount = {wo.SpellCount}");

            Util.WriteToChat("String Values:");
            foreach (var sk in wo.StringKeys) {
                Util.WriteToChat($"  {(StringValueKey)sk}({sk}) = {wo.Values((StringValueKey)sk)}");
            }

            Util.WriteToChat("Long Values:");
            foreach (var sk in wo.LongKeys) {
                switch ((LongValueKey)sk) {
                    case LongValueKey.Behavior:
                        Util.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)}");
                        foreach (BehaviorFlag v in Enum.GetValues(typeof(BehaviorFlag))) {
                            if ((wo.Values(LongValueKey.DescriptionFormat) & (int)v) != 0) {
                                Util.WriteToChat($"    Has Flag: {v.ToString()}");
                            }
                        }
                        break;

                    case LongValueKey.Unknown10:
                        Util.WriteToChat($"  UseablityFlags({sk}) = {wo.Values((LongValueKey)sk)}");
                        foreach (UseFlag v in Enum.GetValues(typeof(UseFlag))) {
                            if ((wo.Values(LongValueKey.Flags) & (int)v) != 0) {
                                Util.WriteToChat($"    Has Flag: {v.ToString()}");
                            }
                        }
                        break;

                    case LongValueKey.PhysicsDataFlags:
                        foreach (PhysicsState v in Enum.GetValues(typeof(PhysicsState))) {
                            if ((wo.PhysicsDataFlags & (int)v) != 0) {
                                Util.WriteToChat($"    Has Flag: {v.ToString()}");
                            }
                        }
                        break;

                    case LongValueKey.Landblock:
                        Util.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)} ({wo.Values((LongValueKey)sk).ToString("X8")})");
                        break;

                    case LongValueKey.Icon:
                        Util.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)} (0x{(0x06000000 + wo.Values((LongValueKey)sk)).ToString("X8")})");
                        break;

                    default:
                        Util.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)}");
                        break;
                }
            }

            Util.WriteToChat("Bool Values:");
            foreach (var sk in wo.BoolKeys) {
                Util.WriteToChat($"  {(BoolValueKey)sk}({sk}) = {wo.Values((BoolValueKey)sk)}");
            }

            Util.WriteToChat("Double Values:");
            foreach (var sk in wo.DoubleKeys) {
                Util.WriteToChat($"  {(DoubleValueKey)sk}({sk}) = {wo.Values((DoubleValueKey)sk)}");
            }

            Util.WriteToChat("Spells:");
            FileService service = Globals.Core.Filter<FileService>();
            for (var i = 0; i < wo.SpellCount; i++) {
                var spell = service.SpellTable.GetById(wo.Spell(i));
                Util.WriteToChat($"  {spell.Name} ({wo.Spell(i)})");
            }
        }

        T Cast<T>(object obj, T type) { return (T)obj; }

        private Regex optionRe = new Regex(@"^((get|set) )?(?<option>[^\s]+)\s?(?<value>.*)", RegexOptions.IgnoreCase);
        private void UB_opt(string args) {
            try {
                if (args.ToLower().Trim() == "list") {
                    ListOptions(Globals.Settings, "");
                    return;
                }

                if (!optionRe.IsMatch(args.Trim())) return;

                var match = optionRe.Match(args.Trim());
                var option = GetOptionProperty(match.Groups["option"].Value);
                string name = match.Groups["option"].Value;
                string newValue = match.Groups["value"].Value;

                if (option == null || option.Object == null) {
                    Util.WriteToChat("Invalid option: " + name);
                    return;
                }

                if (string.IsNullOrEmpty(newValue)) {
                    Util.WriteToChat(name + " = " + option.Object.ToString());
                }
                else {
                    try {
                        option.Property.SetValue(option.Parent, Convert.ChangeType(newValue, option.Property.PropertyType), null);
                        Util.WriteToChat($"Set {name} = {option.Property.GetValue(option.Parent, null)}");
                    }
                    catch (Exception ex) { Util.WriteToChat(ex.Message); }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ListOptions(object obj, string history) {
            obj = obj ?? Globals.Settings;

            var props = obj.GetType().GetProperties();

            foreach (var prop in props) {
                var summaryAttributes = prop.GetCustomAttributes(typeof(SummaryAttribute), true);
                var defaultValueAttributes = prop.GetCustomAttributes(typeof(DefaultValueAttribute), true);

                if (defaultValueAttributes.Length > 0) {
                    Util.WriteToChat($"{history}{prop.Name}");
                }
                else if (summaryAttributes.Length > 0) {
                    ListOptions(prop.GetValue(obj, null), $"{history}{prop.Name}.");
                }
            }
        }

        private OptionResult GetOptionProperty(string key) {
            try {
                var parts = key.Split('.');
                object obj = Globals.Settings;
                PropertyInfo lastProp = null;
                object lastObj = obj;
                for (var i = 0; i < parts.Length; i++) {
                    if (obj == null) return null;

                    var found = false;
                    foreach (var prop in obj.GetType().GetProperties()) {
                        if (prop.Name.ToLower() == parts[i].ToLower()) {
                            lastProp = prop;
                            lastObj = obj;
                            obj = prop.GetValue(obj, null);
                            found = true;
                            break;
                        }
                    }

                    if (!found) return null;
                }

                if (lastProp != null) {
                    var d = lastProp.GetCustomAttributes(typeof(DefaultValueAttribute), true);

                    return d.Length > 0 ? new OptionResult(obj, lastProp, lastObj) : null;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }

        private void OpenVendor() {
            VTankControl.Nav_Block(500 + Globals.Settings.AutoVendor.TriesTime, false);
            vendorOpening = 1;

            vendorTimestamp = DateTime.UtcNow - TimeSpan.FromMilliseconds(Globals.Settings.AutoVendor.TriesTime - 250); // fudge timestamp so next think hits in 500ms
            Globals.Core.Actions.SetAutorun(false);
            Logger.Debug("Attempting to open vendor " + vendor.Name);

        }
        private void UsePortal() {
            VTankControl.Nav_Block(500 + Globals.Settings.Plugin.portalTimeout, false);
            portalAttempts = 1;

            portalTimestamp = DateTime.UtcNow - TimeSpan.FromMilliseconds(Globals.Settings.Plugin.portalTimeout - 250); // fudge timestamp so next think hits in 500ms
            Globals.Core.Actions.SetAutorun(false);
            Logger.Debug("Attempting to use portal " + portal.Name);
        }

        //Do not use this in a loop, it gets an F for eFFiciency.
        private WorldObject FindName(string searchname, bool partial) {
            //try int id first
            if (int.TryParse(searchname, out int id)) {
                if (Globals.Core.WorldFilter[id] != null) {
                    // Util.WriteToChat("Found by id");
                    return Globals.Core.WorldFilter[id];
                }
            }
            //try hex...
            try {
                int intValue = Convert.ToInt32(searchname, 16);
                if (Globals.Core.WorldFilter[intValue] != null) {
                    // Util.WriteToChat("Found vendor by hex");
                    return Globals.Core.WorldFilter[intValue];
                }
            }
            catch { }
            //try exact name...
            WorldObjectCollection temp = Globals.Core.WorldFilter.GetByName(searchname);
            if (temp.Count > 0)
            {
                // Util.WriteToChat("Found vendor by exact name");
                return temp.First();
            }
            if (!partial)
                return null;
            //try slow search...
            foreach (WorldObject thisOne in CoreManager.Current.WorldFilter.GetLandscape()) {
                if (thisOne.Name.Contains(searchname))
                {
                    // Util.WriteToChat("Found by slow search");
                    return thisOne;
                }
            }
            return null;
        }

        public void Think() {
            try {
                if (vendorOpening > 0 && DateTime.UtcNow - vendorTimestamp > TimeSpan.FromMilliseconds(Globals.Settings.AutoVendor.TriesTime)) {

                    if (vendorOpening <= Globals.Settings.AutoVendor.Tries) {
                        if (vendorOpening > 1)
                            Logger.Debug("Vendor Open Timed out, trying again");

                        VTankControl.Nav_Block(500 + Globals.Settings.AutoVendor.TriesTime, false);
                        vendorOpening++;
                        vendorTimestamp = DateTime.UtcNow;
                        CoreManager.Current.Actions.UseItem(vendor.Id, 0);
                    } else {
                        Globals.Core.Actions.FaceHeading(Globals.Core.Actions.Heading, true); // Cancel the previous useitem call (don't ask)
                        Util.ThinkOrWrite("AutoVendor failed to open vendor", Globals.Settings.AutoVendor.Think);
                        vendor = null;
                        vendorOpening = 0;
                        VTankControl.Nav_UnBlock();
                    }
                }
                if (portalAttempts > 0 && DateTime.UtcNow - portalTimestamp > TimeSpan.FromMilliseconds(Globals.Settings.Plugin.portalTimeout)) {

                    if (portalAttempts <= Globals.Settings.Plugin.portalAttempts) {
                        if (portalAttempts > 1)
                            Logger.Debug("Use Portal Timed out, trying again");

                        VTankControl.Nav_Block(500 + Globals.Settings.Plugin.portalTimeout, false);
                        portalAttempts++;
                        portalTimestamp = DateTime.UtcNow;
                        CoreManager.Current.Actions.UseItem(portal.Id, 0);
                    } else {
                        Util.WriteToChat("Unable to use portal " + portal.Name);
                        Globals.Core.Actions.FaceHeading(Globals.Core.Actions.Heading, true); // Cancel the previous useitem call (don't ask)
                        Util.ThinkOrWrite("failed to use portal", Globals.Settings.Plugin.portalThink);
                        portal = null;
                        portalAttempts = 0;
                        VTankControl.Nav_UnBlock();
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private void WorldFilter_ApproachVendor(object sender, ApproachVendorEventArgs e) {
            if (vendorOpening > 0 && e.Vendor.MerchantId == vendor.Id) {
                Logger.Debug("vendor " + vendor.Name + " opened successfully");
                vendor = null;
                vendorOpening = 0;
                // VTankControl.Nav_UnBlock(); Let it bleed over into AutoVendor; odds are there's a reason this vendor was opened, and letting vtank run off prolly isn't it.
            }
        }

        private void EchoFilter_ServerDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                if (portalAttempts > 0 && e.Message.Type == 0xF74B && (int)e.Message["object"] == CoreManager.Current.CharacterFilter.Id && (short)e.Message["portalType"] == 17424) { //17424 is the magic sauce for entering a portal. 1032 is the magic sauce for exiting a portal.
                    Logger.Debug("portal used successfully");
                    portal = null;
                    portalAttempts = 0;
                    VTankControl.Nav_UnBlock();
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.CommandLineText -= Current_CommandLineText;
                    Globals.Core.WorldFilter.ApproachVendor -= WorldFilter_ApproachVendor;
                    Globals.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                }
                disposed = true;
            }
        }
    }
}
