using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace UtilityBelt.Lib.Settings {
    public class SettingsJsonConverter : JsonConverter {
        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            if (typeof(ISetting).IsAssignableFrom(value.GetType())) {
                var iSettingValue = ((ISetting)value).GetValue();
                var fields = iSettingValue.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType));

                if (fields.Count() > 0) {
                        writer.WriteStartObject();
                        foreach (var field in fields) {
                            var fieldValue = ((ISetting)(field.GetValue(iSettingValue))).GetValue();
                            var fieldDefaultValue = ((ISetting)(field.GetValue(iSettingValue))).GetDefaultValue();
                            if (typeof(ISetting).IsAssignableFrom(fieldValue.GetType())) {
                                writer.WritePropertyName(field.Name);
                                WriteJson(writer, fieldValue, serializer);
                            }
                            else  {
                                writer.WritePropertyName(field.Name);
                                writer.WriteValue(fieldValue);
                            }
                        }
                        writer.WriteEndObject();
                }
                else {
                    if (iSettingValue.GetType() != typeof(string) && typeof(IEnumerable).IsAssignableFrom(iSettingValue.GetType())) {
                        writer.WriteStartArray();
                        foreach (var item in (iSettingValue as IEnumerable)) {
                            writer.WriteValue(item);
                        }
                        writer.WriteEnd();
                    }
                    else {
                        writer.WriteValue(((ISetting)value).GetValue());
                    }
                }
            }
        }

        private void ReadObject(JToken obj, ISetting existingValue) {
            var eValue = existingValue.GetValue();
            if (obj.Children().Count() == 0) {
                existingValue.SetValue(obj.ToObject<object>());
            }
            else {
                var jobj = obj as JObject;
                foreach (var fobj in jobj.Properties()) {
                    var childField = eValue.GetType().GetField(fobj.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (typeof(ISetting).IsAssignableFrom(childField.GetValue(eValue).GetType())) {
                        if (fobj.Children().Count() > 0) {
                            foreach (var child in fobj.Children()) {
                                if (child.Children().Count() > 0) {
                                    ReadObject(child, ((ISetting)(childField.GetValue((existingValue).GetValue()))));
                                }
                                else {
                                    ((ISetting)(childField.GetValue((existingValue).GetValue()))).SetValue(fobj.Value.ToString());
                                }
                            }
                        }
                        else {
                            ((ISetting)(childField.GetValue((existingValue).GetValue()))).SetValue(fobj.Value.ToString());
                        }
                    }
                }
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            if (typeof(ISetting).IsAssignableFrom(existingValue.GetType())) {
                if (reader.TokenType == JsonToken.StartObject) {
                    JObject obj = (JObject)serializer.Deserialize(reader);
                    if (typeof(ISetting).IsAssignableFrom(objectType)) {
                        ReadObject(obj, (ISetting)existingValue);
                    }
                }
                else if (reader.TokenType == JsonToken.StartArray) {
                    var list = new List<string>();
                    while (reader.Read() && reader.Value != null) {
                        list.Add(reader.Value.ToString());
                    }
                    ((ISetting)existingValue).SetValue(list);
                }
                else {
                    ((ISetting)existingValue).SetValue(reader.Value);
                }
            }

            return existingValue;
        }

        public override bool CanConvert(Type objectType) {
            return typeof(ISetting).IsAssignableFrom(objectType);
        }
    }
}
