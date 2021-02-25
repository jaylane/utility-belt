using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace UtilityBelt.Lib.Expressions {
    public class ExpressionWorldObject : ExpressionObjectBase {
        public int Id { get; set; }

        [JsonIgnore]
        public Decal.Adapter.Wrappers.WorldObject Wo {
            get {
                if (!UtilityBeltPlugin.Instance.Core.Actions.IsValidObject(Id)) {
                    return null;
                }
                return UtilityBeltPlugin.Instance.Core.WorldFilter[Id];
            }
        }

        public ExpressionWorldObject() {
            IsSerializable = true;
        }

        public ExpressionWorldObject(int id) {
            IsSerializable = true;
            ShouldFireEvents = false;
            Id = id;
            ShouldFireEvents = true;
        }

        public override string ToString() {
            if (Wo != null)
                return $"{Id:X8}: {Wo.Name}, {Wo.ObjectClass}";
            else
                return $"{Id:X8} (Invalid)";
        }
    }
}
