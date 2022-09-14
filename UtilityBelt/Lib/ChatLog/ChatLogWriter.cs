using System.Collections.Generic;
using System.IO;
using System.Threading;
using UBService.Lib.Settings;
using UtilityBelt.Lib.Settings;

namespace UtilityBelt.Lib.ChatLog
{
    public class ChatLogWriter
    {
        private readonly Queue<ChatLog> logsToWrite = new Queue<ChatLog>();
        private readonly Timer flushTimer;
        private bool enabled = false;

        public ChatLogWriter()
        {
            flushTimer = new Timer(new TimerCallback(_ => Flush()));
        }

        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled != value)
                {
                    enabled = value;
                    if (enabled)
                        flushTimer.Change(5000, 5000);
                    else
                    {
                        flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        Flush();
                    }
                }
            }
        }

        public void AddLog(ChatLog log) => logsToWrite.Enqueue(log);

        private void Flush()
        {
            if (logsToWrite.Count > 0)
            {
                var chatLogPath = Path.Combine(Util.GetCharacterDirectory(), "chat.txt");
                using (var fs = File.Open(chatLogPath, FileMode.Append, FileAccess.Write))
                using (var writer = new StreamWriter(fs))
                {
                    while (logsToWrite.Count > 0)
                    {
                        var log = logsToWrite.Dequeue();
                        var type = log.Type.GetParent() ?? log.Type;
                        writer.WriteLine($"{log.Timestamp:O}|{type.GetDescription().ToUpper()}|{log.Message}");
                    }

                    writer.Flush();
                }
            }
        }
    }
}
