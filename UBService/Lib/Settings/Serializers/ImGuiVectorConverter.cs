using ImGuiNET;
using Microsoft.DirectX.Direct3D;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace UBService.Lib.Settings.Serializers {
    public class ImGuiVectorConverter : JsonConverter {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            float[] values = null;
            if (value is Vector2 v2) {
                values = new float[] { v2.X, v2.Y };
            }
            else if (value is Vector3 v3) {
                values = new float[] { v3.X, v3.Y, v3.Z };
            }
            else if (value is Vector4 v4) {
                values = new float[] { v4.W, v4.X, v4.Y, v4.Z };
            }

            if (values != null) {
                writer.WriteRawValue($"[{string.Join(", ", values.Select(f => f.ToString()).ToArray())}]");
            }
            else {
                writer.WriteValue(value);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            object newValue = null;
            
            IEnumerable<float> readValue = JArray.Load(reader).Values<float>();

            if (objectType == typeof(Vector2)) {
                newValue = new Vector2(readValue.ElementAt(0), readValue.ElementAt(1));
            }
            else if (objectType == typeof(Vector3)) {
                newValue = new Vector3(readValue.ElementAt(0), readValue.ElementAt(1), readValue.ElementAt(2));
            }
            else if (objectType == typeof(Vector4)) {
                newValue = new Vector4(readValue.ElementAt(0), readValue.ElementAt(1), readValue.ElementAt(2), readValue.ElementAt(3));
            }

            return newValue ?? reader.Value;
        }

        public override bool CanConvert(Type objectType) {
            return objectType == typeof(Vector2) || objectType == typeof(Vector3) || objectType == typeof(Vector4);
        }
    }
}
