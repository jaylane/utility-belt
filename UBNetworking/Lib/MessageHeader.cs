using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UBNetworking.Lib {
    public class MessageHeader {
        public static readonly int SIZE = sizeof(int) * 5; // header is int type, int flags, int sendingClientId, int targetClientId, int length

        public MessageHeaderType Type;
        public MessageHeaderFlags Flags;
        public int SendingClientId;
        public int TargetClientId;
        public int BodySize;

        public static MessageHeader CreateFrom(BinaryReader reader) {
            return new MessageHeader() {
                Type = (MessageHeaderType)reader.ReadInt32(),
                Flags = (MessageHeaderFlags)reader.ReadInt32(),
                SendingClientId = reader.ReadInt32(),
                TargetClientId = reader.ReadInt32(),
                BodySize = reader.ReadInt32(),
            };
        }

        public static void ToBytes(out byte[] bytes, MessageHeader header) {
            bytes = new byte[SIZE];
            using (var stream = new MemoryStream(bytes))
            using (var writer = new BinaryWriter(stream))
                WriteTo(writer, header);
        }

        public static void WriteTo(BinaryWriter writer, MessageHeader header) {
            writer.Write((int)header.Type);
            writer.Write((int)header.Flags);
            writer.Write((int)header.SendingClientId);
            writer.Write((int)header.TargetClientId);
            writer.Write((int)header.BodySize);
        }
    }
}
