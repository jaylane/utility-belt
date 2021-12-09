using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UBNetworking.Lib;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace UBNetworking {
    public class UBServer : IDisposable {
        public static readonly int MAX_MESSAGE_SIZE = 1024 * 5; // 5k seems like plenty for us right now...
        private static int _id = 0;

        public event EventHandler OnClientConnected;
        public event EventHandler OnClientDisconnected;

        public string Host { get; }
        public int Port { get; }
        public bool IsRunning { get; private set; }
        
        private Action<string> LogAction = null;
        private SerializationBinder Binder = null;
        public Dictionary<int, ServerClient> Clients = new Dictionary<int, ServerClient>();

        TcpListener listener;
        BackgroundWorker worker;

        public UBServer(string host, int port, Action<string>log=null, SerializationBinder binder) {
            Host = host;
            Port = port;
            LogAction = (s) => { log($"Server: {s}"); };
            Binder = binder;

            Start();
        }

        private void Start() {
            if (IsRunning)
                return;

            var addresses = Dns.GetHostEntry(Host).AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (addresses.Count() == 0)
                throw new Exception($"Unable to resolve UBServer Host: {Host}");

            // todo: unhardcode address, it was picking up lan ip above instead of loopback
            listener = new TcpListener(new IPEndPoint(IPAddress.Loopback, Port));
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            listener.Start();
            
            StartBackgroundWorker();
            IsRunning = true;

            LogAction?.Invoke($"Started Listening Server on {listener.LocalEndpoint}");
        }

        private void Stop() {
            if (worker != null) {
                worker.DoWork -= BackgroundWorker_DoWork;
                worker.CancelAsync();
                worker.Dispose();
            }

            listener?.Stop();

            var clients = Clients.Values.ToArray();
            foreach (var client in clients) 
                client.Dispose();

            Clients.Clear();
            IsRunning = false;
        }

        #region BackgroundWorker
        private void StartBackgroundWorker() {
            worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = false;
            worker.DoWork += BackgroundWorker_DoWork;
            worker.RunWorkerAsync();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            BackgroundWorker worker = sender as BackgroundWorker;
            while (worker.CancellationPending != true) {
                try {
                    // accept new connections
                    while (listener.Pending())
                        HandleClientConnected(listener.AcceptTcpClient());

                    // per-client stuff
                    var clients = Clients.Values.ToArray();
                    foreach (var client in clients) {
                        // check timeout
                        if (DateTime.UtcNow - client.lastKeepAliveRecv > TimeSpan.FromSeconds(8)) {
                            LogAction?.Invoke($"Killing client {client.ClientId} because last keepalive was over 3 seconds ago.");
                            client.Disconnected = true;
                        }

                        if (client.Disconnected) {
                            HandleClientDisconnected(client.ClientId);
                            continue;
                        }
                        
                        if (DateTime.UtcNow - client.lastKeepAliveSent > TimeSpan.FromSeconds(3)) {
                            client.SendMessageBytes(new MessageHeader() {
                                SendingClientId = client.ClientId,
                                Type = MessageHeaderType.KeepAlive
                            }, new byte[] { });
                            client.lastKeepAliveSent = DateTime.UtcNow;
                        }

                        client.ReadIncoming();
                        client.WriteOutgoing();
                    }
                }
                catch (Exception ex) { LogAction?.Invoke(ex.ToString()); }
                Thread.Sleep(15);
            }
        }
        #endregion BackgroundWorker

        /// <summary>
        /// called when a new client is connected
        /// </summary>
        /// <param name="tcpClient">thew newly connected tcpClient</param>
        private void HandleClientConnected(TcpClient tcpClient) {
            string connectionId = tcpClient.Client.RemoteEndPoint.ToString();
            var client = new ServerClient(++_id, connectionId, tcpClient, LogAction, (a) => {
                a.Invoke();
            }, Binder);
            client.OnMessageReceived += Client_OnMessageReceived;
            Clients.Add(client.ClientId, client);
            LogAction?.Invoke($"Client {client.ClientId} connected.");
            client.SendMessageBytes(new MessageHeader() {
                SendingClientId = client.ClientId,
                Type = MessageHeaderType.ClientInit
            }, new byte[] { });

            BroadcastClientMessage(client, new MessageHeader() {
                SendingClientId = client.ClientId,
                Type = MessageHeaderType.RemoteClientConnected
            }, new byte[] { });
            OnClientConnected(this, EventArgs.Empty);
        }

        /// <summary>
        /// Called when a client has disconnected, either by itself or from a keepalive timeout
        /// </summary>
        /// <param name="client"></param>
        private void HandleClientDisconnected(int clientId) {
            if (Clients.ContainsKey(clientId)) {
                // we should do cleanup here or something?
                // probably only here if disconnection detection is bugged or something..
                Clients.Remove(clientId);
                Broadcast(new MessageHeader() {
                    SendingClientId = clientId,
                    Type = MessageHeaderType.RemoteClientDisconnected
                }, new byte[] { });
                LogAction?.Invoke($"Client {clientId} disconnected.");
                OnClientDisconnected(this, EventArgs.Empty);
            }
        }


        /// <summary>
        /// called when a message is received from the client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Client_OnMessageReceived(object sender, OnMessageEventArgs e) {
            if (e.Header.Type == MessageHeaderType.KeepAlive || !(sender is ServerClient client))
                return;

            e.Header.SendingClientId = client.ClientId;

            if (e.Header.TargetClientId == 0)
                BroadcastClientMessage(client, e.Header, e.Body);
            else if (Clients.ContainsKey(e.Header.TargetClientId))
                Clients[e.Header.TargetClientId].SendMessageBytes(e.Header, e.Body);
        }

        /// <summary>
        /// send raw bytes to a client
        /// </summary>
        /// <param name="client">client to send to</param>
        /// <param name="bytes">bytes to send</param>
        private void SendRaw(ServerClient client, byte[] bytes) {
            client.SendRaw(bytes);
        }

        /// <summary>
        /// Rebroadcast a message from one client to all the others
        /// </summary>
        /// <param name="client"></param>
        /// <param name="header"></param>
        /// <param name="body"></param>
        private void BroadcastClientMessage(ServerClient sendingClient, MessageHeader header, byte[] body) {
            header.SendingClientId = sendingClient.ClientId;
            var clients = Clients.Values.ToArray();
            foreach (var client in clients) {
                try {
                    client.SendMessageBytes(header, body);
                }
                catch (Exception ex) { LogAction?.Invoke(ex.ToString()); }
            }
        }

        private void Broadcast(MessageHeader header, byte[] body) {
            var clients = Clients.Values.ToArray();
            foreach (var client in clients) {
                try {
                    client.SendMessageBytes(header, body);
                }
                catch (Exception ex) { LogAction?.Invoke(ex.ToString()); }
            }
        }

        public void Dispose() {
            Stop();
        }
    }
}
