using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using UBNetworking;

namespace UtilityBelt.Lib.Networking {
    // this is for generic types that contain our types..
    // https://stackoverflow.com/questions/19666511/how-to-create-a-serializationbinder-for-the-binary-formatter-that-handles-the-mo
    public class HotReloadSerializationBinder : SerializationBinder {
        Regex genericRe = new Regex(@"^(?<gen>[^\[]+)\[\[(?<type>[^\]]*)\](,\[(?<type>[^\]]*)\])*\]$", RegexOptions.Compiled);
        Regex subtypeRe = new Regex(@"^(?<tname>.*)(?<aname>(,[^,]+){4})$", RegexOptions.Compiled);

        public override Type BindToType(string assemblyName, string typeName) {
            var m = genericRe.Match(typeName);
            if (m.Success) { // generic type
                var gen = GetFlatTypeMapping(m.Groups["gen"].Value);
                var genArgs = m.Groups["type"]
                    .Captures
                    .Cast<Capture>()
                    .Select(c => {
                        var m2 = subtypeRe.Match(c.Value);
                        return BindToType(m2.Groups["aname"].Value.Substring(1).Trim(), m2.Groups["tname"].Value.Trim());
                    })
                    .ToArray();
                return gen.MakeGenericType(genArgs);
            }
            return GetFlatTypeMapping(typeName);
        }

        private Type GetFlatTypeMapping(string typeName) {
            var res = typeof(UBClient).Assembly.GetType(typeName);
            res = res == null ? GetType().Assembly.GetType(typeName) : res;
            res = res == null ? Type.GetType(typeName) : res;
            return res;
        }
    }
}
