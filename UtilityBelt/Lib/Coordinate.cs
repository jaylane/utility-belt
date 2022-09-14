﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UtilityBelt.Lib {
    public class Coordinates {
        public double NS { get; set; }
        public double EW { get; set; }
        public double Z { get; set; }

        public static Coordinates Me {
            get {
                var me = UtilityBeltPlugin.Instance.Core.CharacterFilter.Id;
                var pos = PhysicsObject.GetPosition(me);
                var ew = Geometry.LandblockToEW((uint)PhysicsObject.GetLandcell(me), pos.X);
                var ns = Geometry.LandblockToNS((uint)PhysicsObject.GetLandcell(me), pos.Y);
                return new Coordinates(ew, ns, pos.Z);
            }
        }

        public uint LandBlock { get => Geometry.GetLandblockFromCoordinates(EW, NS); }

        public static Regex CoordinateRegex = new Regex(@"(?<NSval>[0-9]{1,3}(?:\.[0-9]{1,3})?)(?<NSchr>(?:[ns]))(?:[,\s]+)?(?<EWval>[0-9]{1,3}(?:\.[0-9]{1,3})?)(?<EWchr>(?:[ew]))?(,?\s*(?<Zval>\-?\d+.?\d+)z)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Coordinates() {

        }

        public Coordinates(double ew, double ns, double z = 0) {
            EW = ew;
            NS = ns;
            Z = z;
        }

        public static Coordinates FromString(string coordsToParse) {
            var coords = new Coordinates();

            if (CoordinateRegex.IsMatch(coordsToParse)) {
                var m = CoordinateRegex.Match(coordsToParse);
                coords.NS = double.Parse(m.Groups["NSval"].Value, (IFormatProvider)CultureInfo.InvariantCulture.NumberFormat);
                if (!string.IsNullOrEmpty(m.Groups["NSchr"].Value.ToLower())) {
                    coords.NS *= m.Groups["NSchr"].Value.ToLower().Equals("n") ? 1 : -1;
                }
                coords.EW = double.Parse(m.Groups["EWval"].Value, (IFormatProvider)CultureInfo.InvariantCulture.NumberFormat);
                if (!string.IsNullOrEmpty(m.Groups["EWchr"].Value.ToLower())) {
                    coords.EW *= m.Groups["EWchr"].Value.ToLower().Equals("e") ? 1 : -1;
                }
                if (!string.IsNullOrEmpty(m.Groups["Zval"].Value))
                    coords.Z = double.Parse(m.Groups["Zval"].Value, (IFormatProvider)CultureInfo.InvariantCulture.NumberFormat);
            }

            return coords;
        }

        public double DistanceTo(Coordinates other) {
            var nsdiff = (((NS * 10) + 1019.5) * 24) - (((other.NS * 10) + 1019.5) * 24);
            var ewdiff = (((EW * 10) + 1019.5) * 24) - (((other.EW * 10) + 1019.5) * 24);
            return Math.Abs(Math.Sqrt(Math.Pow(Math.Abs(nsdiff), 2) + Math.Pow(Math.Abs(ewdiff), 2) + Math.Pow(Math.Abs(Z - other.Z), 2)));
        }

        public double DistanceToFlat(Coordinates other) {
            var nsdiff = (((NS * 10) + 1019.5) * 24) - (((other.NS * 10) + 1019.5) * 24);
            var ewdiff = (((EW * 10) + 1019.5) * 24) - (((other.EW * 10) + 1019.5) * 24);
            return Math.Abs(Math.Sqrt(Math.Pow(Math.Abs(nsdiff), 2) + Math.Pow(Math.Abs(ewdiff), 2)));
        }

        public override string ToString() {
            return $"{Math.Abs(NS).ToString("F2")}{(NS >= 0 ? "N" : "S")}, {Math.Abs(EW).ToString("F2")}{(EW >= 0 ? "E" : "W")}, {(Z/240).ToString("F2")}Z";
        }
    }
}
