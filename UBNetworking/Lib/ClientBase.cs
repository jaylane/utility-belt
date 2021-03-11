using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace UBNetworking.Lib {
    public class ClientBase : IDisposable {
        public int ClientId { get; protected set; }
        public string ConnectionId { get; protected set; }
        public int KeepAliveSeconds { get; }
        public bool Disconnected { get; internal set; }
        public bool IsDisposed { get; private set; }

        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;
        public event EventHandler<OnMessageEventArgs> OnMessageReceived;

        internal DateTime lastKeepAliveSent = DateTime.UtcNow;
        internal DateTime lastKeepAliveRecv = DateTime.UtcNow;
        internal TcpClient TcpClient { get; private set; } = null;
        internal bool IsRemote { get; set; }

        internal byte[] recvBuffer = new byte[UBServer.MAX_MESSAGE_SIZE];

        protected Action<string> LogAction = null;
        protected Action<Action> RunOnMainThread;

        public SerializationBinder Binder { get; }

        private Queue SendQueue = Queue.Synchronized(new Queue());
        private int recvBufferOffset = 0;
        private bool isReadingMessageBody = false;
        private MessageHeader incomingMessageHeader;

        private Dictionary<string, List<object>> handlers = new Dictionary<string, List<object>>();
        private Dictionary<object, object> _handlerCache = new Dictionary<object, object>();

        public ClientBase(int clientId, string connectionId, Action<string> log, Action<Action> runOnMainThread, SerializationBinder binder) {
            ClientId = clientId;
            ConnectionId = connectionId;
            LogAction = log;
            RunOnMainThread = runOnMainThread;
            Binder = binder;
        }

        public void SetClient(TcpClient client) {
            TcpClient = client;
            RunOnMainThread(() => {
                OnConnected?.Invoke(this, EventArgs.Empty);
            });
        }

        /// <summary>
        /// Adds a message to the send queue
        /// </summary>
        /// <param name="header"></param>
        /// <param name="body"></param>
        public void SendMessageBytes(MessageHeader header, byte[] body) {
            MessageHeader.ToBytes(out byte[] headerBytes, header);
            byte[] message;
            if (body != null) {
                message = new byte[headerBytes.Length + body.Length];
                headerBytes.CopyTo(message, 0);
                body.CopyTo(message, MessageHeader.SIZE);
            }
            else {
                message = headerBytes;
            }
            SendQueue.Enqueue(message);
        }

        /// <summary>
        /// Adds a message to the send queue
        /// </summary>
        /// <param name="header"></param>
        /// <param name="body"></param>
        public void SendObject(MessageHeader header, object obj) {
            // todo; less buffer copies
            //LogAction?.Invoke($"Client {ClientId} SendObject {header.Type} {obj.GetType()} SendingId:{header.SendingClientId}");
            byte[] typeBytes;
            byte[] bodyBytes;
            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream))
                    writer.Write(obj.GetType().FullName);
                typeBytes = stream.ToArray();
            }
            using (var stream = new MemoryStream()) {
                new BinaryFormatter() {
                    Binder = Binder
                }.Serialize(stream, obj);
                bodyBytes = stream.ToArray();
            }
            header.BodySize = typeBytes.Length + bodyBytes.Length;
            MessageHeader.ToBytes(out byte[] headerBytes, header);
            var message = new byte[headerBytes.Length + typeBytes.Length + bodyBytes.Length];
            headerBytes.CopyTo(message, 0);
            typeBytes.CopyTo(message, headerBytes.Length);
            bodyBytes.CopyTo(message, headerBytes.Length + typeBytes.Length);
            SendRaw(message);
        }

        /// <summary>
        /// Adds raw bytes to the send queue, probably dont use this
        /// </summary>
        /// <param name="header"></param>
        /// <param name="body"></param>
        public void SendRaw(byte[] body) {
            SendQueue.Enqueue(body);
        }

        /// <summary>
        /// Reads tcp stream and parses into messages, firing off an event for each one
        /// </summary>
        internal void ReadIncoming() {
            try {
                // read stream until empty
                while (TcpClient != null && TcpClient.Connected && (
                    (isReadingMessageBody && TcpClient.Available > 0) ||
                    (!isReadingMessageBody && TcpClient.Available >= MessageHeader.SIZE)
                )) {
                    // read new message header
                    if (!isReadingMessageBody) {
                        var headerBytes = new byte[MessageHeader.SIZE];
                        TcpClient.GetStream().Read(headerBytes, 0, MessageHeader.SIZE);
                        using (var stream = new MemoryStream(headerBytes))
                        using (var reader = new BinaryReader(stream)) {
                            incomingMessageHeader = MessageHeader.CreateFrom(reader);
                            if (incomingMessageHeader.BodySize > 0)
                                isReadingMessageBody = true;

                            switch (incomingMessageHeader.Type) {
                                case MessageHeaderType.ClientInit:
                                    if (!IsRemote)
                                        ClientId = incomingMessageHeader.SendingClientId;
                                    RunOnMainThread(() => {
                                        OnMessageReceived?.Invoke(this, new OnMessageEventArgs(incomingMessageHeader, null));
                                    });
                                    continue;
                                case MessageHeaderType.KeepAlive:
                                    lastKeepAliveRecv = DateTime.UtcNow;
                                    RunOnMainThread(() => {
                                        OnMessageReceived?.Invoke(this, new OnMessageEventArgs(incomingMessageHeader, null));
                                    });
                                    continue;
                                case MessageHeaderType.RemoteClientConnected:
                                case MessageHeaderType.RemoteClientDisconnected:
                                    RunOnMainThread(() => {
                                        OnMessageReceived?.Invoke(this, new OnMessageEventArgs(incomingMessageHeader, null));
                                    });
                                    continue;
                            }
                        }
                    }

                    // read message body in from buffer and flush when done
                    if (isReadingMessageBody && TcpClient.Available > 0) {
                        // we only read up to currentMessageSize - bufferOffset. We don't want to read into the next
                        // message header
                        recvBufferOffset += TcpClient.GetStream().Read(recvBuffer, recvBufferOffset, incomingMessageHeader.BodySize - recvBufferOffset);

                        // flush if we read the entire message
                        if (recvBufferOffset >= incomingMessageHeader.BodySize) {
                            ReadMessage(incomingMessageHeader);
                            isReadingMessageBody = false;
                        }
                    }
                }
            }
            catch (IOException) { DoDisconnected(); }
        }

        /// <summary>
        /// writes send queue to socket
        /// </summary>
        internal void WriteOutgoing() {
            try {
                while (TcpClient != null && TcpClient.Connected && SendQueue.Count > 0) {
                    byte[] bytesToSend = (byte[])SendQueue.Dequeue();
                    TcpClient.GetStream().Write(bytesToSend, 0, bytesToSend.Length);
                }
            }
            catch (IOException) { DoDisconnected(); }
        }

        /// <summary>
        /// called when this client is detected as disconnected
        /// </summary>
        private void DoDisconnected() {
            try {
                TcpClient?.Close();
            }
            catch { }
            TcpClient = null;
            Disconnected = true;
            RunOnMainThread(() => {
                OnDisconnected?.Invoke(this, EventArgs.Empty);
            });
        }

        /// <summary>
        /// reads a full message body from the buffer, and sends appropriate events
        /// </summary>
        private void ReadMessage(MessageHeader incomingMessageHeader) {
            // use pooling?
            var messageBytes = new byte[incomingMessageHeader.BodySize];
            Buffer.BlockCopy(recvBuffer, 0, messageBytes, 0, incomingMessageHeader.BodySize);
            recvBufferOffset -= incomingMessageHeader.BodySize;

            if (recvBufferOffset != 0)
                LogAction?.Invoke($"Error: client {ConnectionId} read message and ended up with bad offset: {recvBufferOffset}. Should be 0.");
            RunOnMainThread(() => {
                OnMessageReceived?.Invoke(this, new OnMessageEventArgs(incomingMessageHeader, messageBytes));
            });
        }

        /// <summary>
        /// Deserialize a message body, and run any handlers subscribed to the deserialized type
        /// </summary>
        /// <param name="header"></param>
        /// <param name="body"></param>
        internal void DeserializeMessage(MessageHeader header, byte[] body) {
            try {
                if (body == null || body.Length == 0)
                    return;
                using (var stream = new MemoryStream(body)) {
                    using (var reader = new BinaryReader(stream)) {
                        var typeStr = reader.ReadString();
                        var type = Binder.BindToType("", typeStr);
                        if (type == null) {
                            LogAction?.Invoke($"Unable to deserialize message type, unknown: {typeStr}");
                            return;
                        }
                        var obj = new BinaryFormatter() {
                            Binder = Binder
                        }.Deserialize(stream);
                        RunHandlers(header, obj, type);
                    }
                }
            }
            catch (Exception ex) { LogAction?.Invoke(ex.ToString()); }
        }

        #region Network Message Handlers
        /// <summary>
        /// Adds a message handler for the specified type T
        /// </summary>
        /// <typeparam name="T">the message type to subscribe to</typeparam>
        /// <param name="handler">handler method</param>
        public void AddMessageHandler<T>(Action<MessageHeader, T> handler) {
            if (!handlers.ContainsKey(typeof(T).ToString()))
                handlers.Add(typeof(T).ToString(), new List<object>());
            var _handler = (Action<MessageHeader, object>)((h, o) => {
                handler?.Invoke(h, (T)o);
            });
            _handlerCache.Add(handler, _handler);
            handlers[typeof(T).ToString()].Add(_handler);
        }

        /// <summary>
        /// Removes a message handler for the specified type T
        /// </summary>
        /// <typeparam name="T">the message type to subscribe to</typeparam>
        /// <param name="handler">handler method</param>
        public void RemoveMessageHandler<T>(Action<MessageHeader, T> handler) {
            if (!handlers.ContainsKey(typeof(T).ToString()) || !_handlerCache.ContainsKey(handler))
                return;
            handlers[typeof(T).ToString()].Remove(_handlerCache[handler]);
            _handlerCache.Remove(handler);
        }

        private void RunHandlers(MessageHeader header, object obj, Type type) {
            if (!handlers.ContainsKey(type.ToString()))
                return;

            var _handlers = handlers[type.ToString()].ToArray();
            RunOnMainThread(() => {
                foreach (var handler in _handlers) {
                    try {
                        ((Action<MessageHeader, object>)handler)?.Invoke(header, obj);
                    }
                    catch (Exception ex) { LogAction?.Invoke(ex.ToString()); }
                }
            });
        }
        #endregion Network Message Handlers

        protected virtual void Dispose(bool isDisposing) {
            handlers.Clear();
            _handlerCache.Clear();
            TcpClient?.Close();
            recvBuffer = null;
        }

        public void Dispose() {
            if (IsDisposed)
                return;
            IsDisposed = true;
            Dispose(true);
        }
    }
}
