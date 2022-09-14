using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace UBService.Lib.Settings {
    internal class SerializationBinder : ISerializationBinder {
        public object BindParent = null;

        public Type BindToType(string assemblyName, string typeName) {
            var genRe = new Regex(@"^(?<gen>[^\[]+)\[(?<type>[^\]]*,?)+\]$");
            var m = genRe.Match(typeName);
            if (m.Success) {
                var gen = GetFlatTypeMapping(assemblyName, m.Groups["gen"].Value);
                var genArgs = m.Groups["type"].Captures
                    .Cast<Capture>()
                    .Where(c => !string.IsNullOrEmpty(c.Value))
                    .Select(c => {
                        if (genRe.IsMatch(c.Value)) {
                            return BindToType(assemblyName, c.Value);
                        }
                        else {
                            return GetFlatTypeMapping(assemblyName, c.Value);
                        }
                    }).ToArray();
                return gen.MakeGenericType(genArgs);
            }
            return GetFlatTypeMapping(assemblyName, typeName);
        }

        private Type GetFlatTypeMapping(string assemblyName, string typeName = "") {
            Type ret = null;

            if (BindParent != null) {
                ret = BindParent.GetType().Assembly.GetTypes().FirstOrDefault(t => t.ToString() == typeName || t.Name == typeName);
            }

            if (ret == null) {
                ret = typeof(System.Collections.Generic.List<>).Assembly.GetTypes().FirstOrDefault(t => t.ToString().Split('[').FirstOrDefault() == typeName);
            }
            if (ret == null) {
                ret = typeof(Hellosam.Net.Collections.TraversalMode).Assembly.GetTypes().FirstOrDefault(t => t.ToString().Split('[').FirstOrDefault() == typeName);
            }

            if (ret == null) {
                UBService.LogError($"Could not get flat type mapping for type: {typeName}");
            }

            return ret;
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName) {
            assemblyName = null;
            typeName = $"{serializedType}";
        }
    }
}
