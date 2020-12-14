using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace UtilityBelt.Lib.Settings {
    public class ShouldSerializeContractResolver : DefaultContractResolver {
        public static readonly ShouldSerializeContractResolver Instance = new ShouldSerializeContractResolver();

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            if (typeof(ISetting).IsAssignableFrom(property.PropertyType)) {
                property.Converter = new SettingsJsonConverter();
                property.ShouldSerialize = instance => {
                    if (typeof(ISetting).IsAssignableFrom(instance.GetType())) {
                        var shouldSerialize = !((ISetting)instance).GetValue().Equals(((ISetting)instance).GetDefaultValue());
                        //Logger.WriteToChat($"Serialize: {((ISetting)instance).GetName()} {shouldSerialize}");
                    }
                    else {
                        //Logger.WriteToChat($"ShouldSerialize: {instance.GetType()}");
                    }
                    return true;
                };
            }
            else if (!(typeof(ToolBase).IsAssignableFrom(property.PropertyType) || property.PropertyType == typeof(UtilityBeltPlugin))) {
                property.ShouldSerialize = p => false;
            }

            return property;
        }

        protected override JsonContract CreateContract(Type objectType) {
            JsonContract contract = base.CreateContract(objectType);

            if (typeof(ISetting).IsAssignableFrom(objectType)) {
                contract.Converter = new SettingsJsonConverter();
            }

            return contract;
        }
    }
}
