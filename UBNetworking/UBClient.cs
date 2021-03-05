using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using UBNetworking.Lib;
using System.IO;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace UBNetworking {
    public class UBClient : ClientBase {
        public event EventHandler<RemoteClientConnectionEventArgs> OnRemoteClientConnected;
        public event EventHandler<RemoteClientConnectionEventArgs> OnRemoteClientDisconnected;

        public string Host { get; }
        public int Port { get; }
        public DateTime WorkerStartedAt { get; private set; }

        private BackgroundWorker worker;

        private double retrySeconds = 1;
        private DateTime lastRetry = DateTime.MinValue;

        public UBClient(string host, int port, Action<string> log, Action<Action> runOnMainThread, SerializationBinder binder)
            : base(0, "local", log, runOnMainThread, binder) {
            Host = host;
            Port = port;

            OnMessageReceived += BaseClient_OnMessageReceived;
            StartBackgroundWorker();
        }

        private void BaseClient_OnMessageReceived(object sender, OnMessageEventArgs e) {
            switch (e.Header.Type) {
                case MessageHeaderType.RemoteClientConnected:
                    RunOnMainThread(() => {
                        OnRemoteClientConnected?.Invoke(this, new RemoteClientConnectionEventArgs(e.Header.SendingClientId, RemoteClientConnectionEventArgs.ConnectionType.Connected));
                    });
                    break;
                case MessageHeaderType.RemoteClientDisconnected:
                    RunOnMainThread(() => {
                        OnRemoteClientDisconnected?.Invoke(this, new RemoteClientConnectionEventArgs(e.Header.SendingClientId, RemoteClientConnectionEventArgs.ConnectionType.Disconnected));
                    });
                    break;
                case MessageHeaderType.ClientInit:
                    ClientId = e.Header.SendingClientId;
                    break;
                case MessageHeaderType.Serialized:
                    DeserializeMessage(e.Header, e.Body);
                    break;
            }
        }

        #region BackgroundWorker
        private void StartBackgroundWorker() {
            worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = false;
            worker.DoWork += BackgroundWorker_DoWork;
            worker.RunWorkerAsync();
            WorkerStartedAt = DateTime.UtcNow;
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            BackgroundWorker worker = sender as BackgroundWorker;
            while (worker.CancellationPending != true) {
                try {
                    if (TcpClient == null && DateTime.UtcNow - lastRetry > TimeSpan.FromSeconds(retrySeconds)) {
                        retrySeconds *= 2;
                        LogAction?.Invoke($"Attempting to connect to {Host}:{Port}");
                        lastKeepAliveRecv = DateTime.UtcNow;
                        lastKeepAliveSent = DateTime.UtcNow;
                        var client = new TcpClient(Host, Port);
                        ConnectionId = client.Client.LocalEndPoint.ToString();
                        SetClient(client);
                        retrySeconds = 1; // reset reconnection delay
                    }
                    if (TcpClient != null && TcpClient.Connected && DateTime.UtcNow - WorkerStartedAt > TimeSpan.FromSeconds(1)) {
                        if (DateTime.UtcNow - lastKeepAliveSent > TimeSpan.FromMilliseconds(3000)) {
                            //LogAction?.Invoke($"Sending keepalive from {Id}");
                            SendMessageBytes(new MessageHeader() {
                                SendingClientId = ClientId,
                                Type = MessageHeaderType.KeepAlive
                            }, new byte[] { });
                            lastKeepAliveSent = DateTime.UtcNow;
                        }
                        ReadIncoming();
                        WriteOutgoing();
                    }
                }
                catch (SocketException ex) {
                    //LogAction?.Invoke(ex.ToString());
                    TryLaunchServer();
                }
                catch (Exception ex) { LogAction?.Invoke(ex.ToString()); }
                Thread.Sleep(15);
            }
        }
        #endregion BackgroundWorker

        private void TryLaunchServer() {
            using (var mutex = new Mutex(false, "com.UBNetServer.Instance")) {
                bool isAnotherInstanceOpen = !mutex.WaitOne(TimeSpan.Zero);
                if (!isAnotherInstanceOpen) {
                    try {
                        string assemblyPath = System.Reflection.Assembly.GetAssembly(typeof(UBClient)).Location;
                        string assemblyDirectory = Path.GetDirectoryName(assemblyPath);
                        Process p = new Process();
                        p.StartInfo.UseShellExecute = true;
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        p.StartInfo.FileName = Path.Combine(assemblyDirectory, "UBNetServer.exe");
                        p.Start();
                    }
                    catch (Exception launchEx) { LogAction?.Invoke(launchEx.ToString()); }
                }
            }
        }

        protected override void Dispose(bool isDisposing) {
            base.Dispose(isDisposing);
            OnMessageReceived -= BaseClient_OnMessageReceived;
            if (worker != null) {
                worker.DoWork -= BackgroundWorker_DoWork;
                worker.CancelAsync();
                worker.Dispose();
            }
        }
    }
}
