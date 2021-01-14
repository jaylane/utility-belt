using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace UBLoader.Lib.Settings {
    public class SerializationBinder : ISerializationBinder {
        public Type BindToType(string assemblyName, string typeName) {
            return FilterCore.CurrentAssembly.GetTypes().First(t => t.Name == typeName);
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName) {
            assemblyName = null;
            typeName = serializedType.Name;
        }
    }
}
