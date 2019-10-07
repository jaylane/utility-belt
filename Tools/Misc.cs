using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Mag.Shared.Settings;
using UtilityBelt.Lib.VendorCache;
using UtilityBelt.Views;
using VirindiViewService.Controls;
using System.Text.RegularExpressions;

namespace UtilityBelt.Tools {
    public class Misc : IDisposable {
        private DateTime vendorTimestamp = DateTime.MinValue;
        private int vendorOpening = 0;
        private static WorldObject vendor = null;

        private const int VENDOR_OPEN_TIMEOUT = 5000;

        private bool disposed = false;

        public Misc() {
            try {
                Globals.Core.CommandLineText += Current_CommandLineText;
                Globals.Core.WorldFilter.ApproachVendor += WorldFilter_ApproachVendor;
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
                "   /ub testblock <int> <duration> - test Decision_Lock parameters (potentially dangerous)\n" +
                "   /ub vendor {buyall,sellall,clearbuy,clearsell}\n" +
                "   /ub vendor open[p] {vendorname,vendorid,vendorhex}\n" +
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
                Util.WriteToChat("Usage: /ub vendor {open[p] {vendorname,vendorid,vendorhex},buyall,sellall,clearbuy,clearsell}");
                return;
            }
            switch (parameter[0])
            {
                case "buyall":
                    CoreManager.Current.Actions.VendorBuyAll();
                    break;

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
                        Util.WriteToChat("Usage: /ub vendor open {vendorname,vendorid}");
                        return;
                    }
                    vendor = FindName(parameter[1], false);
                    if (vendor != null) {
                        OpenVendor();
                        break;
                    }
                    Util.WriteToChat("Pretty sure " + parameter[1] + " is not near me");
                    break;

                case "openp":
                    if (parameter.Length != 2) {
                        Util.WriteToChat("Usage: /ub vendor open {vendorname,vendorid}");
                        return;
                    }
                    vendor = FindName(parameter[1], true);
                    if (vendor != null) {
                        OpenVendor();
                        break;
                    }
                    Util.WriteToChat("Pretty sure " + parameter[1] + " is not near me");
                    break;
            }
        }
        private void OpenVendor() {
            VTankControl.Nav_Block(500 + VENDOR_OPEN_TIMEOUT, false);
            vendorOpening = 1;

            vendorTimestamp = DateTime.UtcNow - TimeSpan.FromMilliseconds(VENDOR_OPEN_TIMEOUT - 250); // fudge timestamp so next think hits in 500ms
            Globals.Core.Actions.SetAutorun(false);
            // Util.WriteToChat("Attempting to open vendor " + vendor.Name);

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
                if (vendorOpening > 0 && DateTime.UtcNow - vendorTimestamp > TimeSpan.FromMilliseconds(VENDOR_OPEN_TIMEOUT)) {
                    if (vendorOpening > 1)
                        Util.WriteToChat("Vendor Open Timed out, trying again");
                    if (vendorOpening < 4) {
                        VTankControl.Nav_Block(500 + VENDOR_OPEN_TIMEOUT, false);
                        vendorOpening++;
                        vendorTimestamp = DateTime.UtcNow;
                        CoreManager.Current.Actions.UseItem(vendor.Id, 0);
                    } else {
                        Util.WriteToChat("Unable to open vendor "+vendor.Name);
                        vendor = null;
                        vendorOpening = 0;
                        VTankControl.Nav_UnBlock();
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private void WorldFilter_ApproachVendor(object sender, ApproachVendorEventArgs e) {
            if (vendorOpening > 0 && e.Vendor.MerchantId == vendor.Id)
            {
                // Util.WriteToChat("vendor " + vendor.Name + " opened successfully");
                vendor = null;
                vendorOpening = 0;
                // VTankControl.Nav_UnBlock(); Let it bleed over into AutoVendor; odds are there's a reason this vendor was opened, and letting vtank run off prolly isn't it.
            }
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
                }
                disposed = true;
            }
        }
    }
}
