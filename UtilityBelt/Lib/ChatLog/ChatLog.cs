using System;
using UtilityBelt.Service.Lib.Settings;
using UtilityBelt.Lib.Constants;
using UtilityBelt.Lib.Settings;

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
