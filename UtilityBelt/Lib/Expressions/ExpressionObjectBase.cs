using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.Reflection;
using System.Collections.Concurrent;

namespace UtilityBelt.Lib.Expressions {
    public class LoadedTypesBinder : Newtonsoft.Json.Serialization.ISerializationBinder {
        public Type BindToType(string assemblyName, string typeName) {
            if (typeName.StartsWith("System.Collections.Concurrent.ObservableConcurrentDictionary"))
                return typeof(ObservableConcurrentDictionary<string, object>);
            return Type.GetType(typeName);
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName) {
            assemblyName = null;
            typeName = serializedType.FullName;
        }
    }


    public class ExpressionObjectBase {
        [JsonIgnore]
        public bool IsSerializable { get; protected set; } = false;

        [JsonIgnore]
        public string Name { get; set; }

        public EventHandler Changed;

        [JsonIgnore]
        public bool HasChanges { get; set; }

        [JsonIgnore]
        public static bool ShouldFireEvents { get; set; } = true;

        public void InvokeChange() {
            if (ShouldFireEvents) {
                HasChanges = true;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings() {
            PreserveReferencesHandling = PreserveReferencesHandling.All,
            TypeNameHandling = TypeNameHandling.Objects,
            SerializationBinder = new LoadedTypesBinder()
        };

        public static object DeserializeRecord(string data, Type type) {
            ShouldFireEvents = false;
            var obj = JsonConvert.DeserializeObject(data, type, SerializerSettings);
            ShouldFireEvents = true;
            return obj;
        }

        public static string SerializeRecord(object record) {
            return JsonConvert.SerializeObject(record, Formatting.Indented, SerializerSettings);
        }
    }
}
