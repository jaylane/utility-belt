using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

namespace UBNetServer {
    class Program {
        static bool shouldExit = false;
        static DateTime lastclientAction = DateTime.UtcNow;
        static TimeSpan programExitTimeout = TimeSpan.FromSeconds(30);

        static void Main(string[] args) {
            using (var mutex = new Mutex(false, "com.UBNetServer.Instance")) {
                bool isAnotherInstanceOpen = !mutex.WaitOne(TimeSpan.Zero);
                if (isAnotherInstanceOpen) {
                    Log("Only one instance of this app is allowed.");
                    return;
                }

                string host = "127.0.0.1";
                int port = 42163;

                if (args.Length == 2) {
                    host = args[0];
                    if (!Int32.TryParse(args[1], out port)) {
                        Log($"Unable to parse port! {args[1]}");
                        return;
                    }
                }

                var server = new UBNetworking.UBServer(host, port, Log);
                server.OnClientConnected += Server_OnClientConnected;
                server.OnClientDisconnected += Server_OnClientDisconnected;
                while (!shouldExit) {
                    if (server.Clients.Count == 0 && DateTime.UtcNow - lastclientAction > programExitTimeout) {
                        Log("Exiting due to inactivity.");
                        break;
                    }
                    Thread.Sleep(1000);
                }
                server?.Dispose();
            }
        }

        private static void Server_OnClientDisconnected(object sender, EventArgs e) {
            lastclientAction = DateTime.UtcNow;
        }

        private static void Server_OnClientConnected(object sender, EventArgs e) {
            lastclientAction = DateTime.UtcNow;
        }

        static void Log(string message) {
            Console.WriteLine(message);
        }
    }
}
