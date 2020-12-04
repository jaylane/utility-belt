using System;
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

        public uint LandBlock { get => Geometry.GetLandblockFromCoordinates(EW, NS); }

        public static Regex CoordinateRegex = new Regex(@"(?<NSval>\d+.?\d*)\s*(?<NSchr>[ns]),?\s*(?<EWval>\d+.?\d*)(?<EWchr>[ew])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
                coords.NS *= m.Groups["NSchr"].Value.ToLower().Equals("n") ? 1 : -1;
                coords.EW = double.Parse(m.Groups["EWval"].Value, (IFormatProvider)CultureInfo.InvariantCulture.NumberFormat);
                coords.EW *= m.Groups["EWchr"].Value.ToLower().Equals("e") ? 1 : -1;
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
            return $"{Math.Abs(NS).ToString("F2")}{(NS >= 0 ? "N" : "S")}, {Math.Abs(EW).ToString("F2")}{(EW >= 0 ? "E" : "W")} (Z {Z.ToString("F2")})";
        }
    }
}
