using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace UtilityBelt.Lib.Expressions {
    public class ExpressionCoordinates : ExpressionObjectBase {
        private double _ns = 0;
        private double _ew = 0;
        private double _z = 0;

        public double NS {
            get => _ns;
            set {
                _ns = value;
                Coordinates.NS = value;
            }
        }
        public double EW {
            get => _ew;
            set {
                _ew = value;
                Coordinates.EW = value;
            }
        }
        public double Z {
            get => _z;
            set {
                _z = value;
                Coordinates.Z = value;
            }
        }

        [JsonIgnore]
        public Coordinates Coordinates = new Coordinates();

        public ExpressionCoordinates() {
            IsSerializable = true;
        }

        public ExpressionCoordinates(double ew, double ns, double z=0) {
            IsSerializable = true;
            ShouldFireEvents = false;
            NS = ns;
            EW = ew;
            Z = z;
            ShouldFireEvents = true;
        }

        public override string ToString() {
            return Coordinates.ToString();
        }
    }
}
