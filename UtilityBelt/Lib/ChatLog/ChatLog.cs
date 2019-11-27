using System;
using UtilityBelt.Lib.Constants;

namespace UtilityBelt.Lib.ChatLog
{
    public class ChatLog
    {
        public DateTimeOffset Timestamp { get; }

        public ChatMessageType Type { get; }

        public string Message { get; }

        public ChatLog(ChatMessageType type, string message)
        {
            Timestamp = DateTimeOffset.UtcNow;
            Type = type;
            Message = message;
        }
    }
}
