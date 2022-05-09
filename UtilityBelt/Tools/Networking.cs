using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Lib;
using System.ComponentModel;
using System.Windows.Forms;
using UBLoader.Lib.Settings;
using System.Collections.Concurrent;
using UtilityBelt.Lib.Networking.Messages;
using System.Text.RegularExpressions;
using System.Threading;
using UtilityBelt.Views;
using System.Collections.ObjectModel;
using UtilityBelt.Lib.Networking;
using UBNetworking;
using UBNetworking.Lib;
using UBNetworking.Messages;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Reflection;

namespace UtilityBelt.Tools {
    [Name("Networking")]
    [Summary("Provides client communication via a local tcp server.")]
    [FullDescription(@"Allows clients to communicate with each other via a local tcp server.  Clients can  have tags added to them using the setting `Networking.Tags`.  Tags can be used in combination with the `/ub bct` command to broadcast a command to all characters of a specific tag.  See below for examples.")]
    public class Networking : ToolBase {
        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;
        public event EventHandler<RemoteClientConnectionEventArgs> OnRemoteClientConnected;
        public event EventHandler<RemoteClientConnectionEventArgs> OnRemoteClientDisconnected;

        public bool Connected { get; internal set; }
        public bool IsRunning { get; private set; }
        public int ClientId { get => ubNet != null ? ubNet.ClientId : 0; }
        public readonly ObservableConcurrentDictionary<int, ClientInfo> Clients = new ObservableConcurrentDictionary<int, ClientInfo>();
        

        private UBClient ubNet;

        private ConcurrentQueue<Action> GameThreadActionQueue = new ConcurrentQueue<Action>();
        private DateTime lastClientCleanup = DateTime.MinValue;

        #region Config
        [Summary("Networking server host")]
        public readonly Setting<string> ServerHost = new Setting<string>("127.0.0.1");

        [Summary("Networking server port")]
        public readonly Setting<int> ServerPort = new Setting<int>(42163);

        [Summary("Character identifier tags. You can use these with the /ub bct command to limit which characters are broadcast to.")]
        public readonly CharacterState<ObservableCollection<string>> Tags = new CharacterState<ObservableCollection<string>>(new ObservableCollection<string>());
        #endregion Config

        #region Commands
        #region /ub bc <millisecondDelay> <command>
        [Summary("Broadcasts a command to all open clients, with optional `millisecondDelay` inbetween each")]
        [Usage("/ub bc [millisecondDelay] <command>")]
        [Example("/ub bc 5000 /say hello", "Runs \"/say hello\" on every client, with a 5000ms delay between each")]
        [Example("/ub bc /say hello", "Runs \"/say hello\" on every client, with no delay")]
        [CommandPattern("bc", @"^(?<delay>\d*) ?(?<command>.*)$")]
        public void DoBroadcast(string _, Match args) {
            var command = args.Groups["command"].Value;
            int delay = 0;

            if (!string.IsNullOrEmpty(args.Groups["delay"].Value) && !int.TryParse(args.Groups["delay"].Value, out delay)) {
                Logger.Error($"Unable to broadcast command, invalid delay: {args.Groups["delay"].Value}");
                return;
            }
            if (delay < 0) {
                Logger.Error($"Delay must be greater than zero: {delay}");
                return;
            }

            Logger.WriteToChat($"Broadcasting command to all clients: \"{command}\" with delay inbetween of {delay}ms");
            DoBroadcast(Clients.Select(c => c.Value), command, delay);
            if (UB.Plugin.Debug)
                Logger.Debug($"Sent to clients: {string.Join(", ", Clients.Select(c => c.Value.Name).ToArray())}");
        }

        public void DoBroadcast(IEnumerable<ClientInfo> clients, string command, int delay) {
            var i = 1;
            foreach (var client in clients)
                SendObject(new CommandBroadcastMessage(command, i++ * delay), client.ClientId);
        }
        #endregion /ub bc <millisecondDelay> <command>
        #region /ub bct <teamslist> <millisecondDelay> <command>
        [Summary("Broadcasts a command to all clients with the specified comma-separated tags (no spaces!), with optional `millisecondDelay` inbetween each. Tags are managed with the Networking.Tags setting.")]
        [Usage("/ub bct <teamslist> [millisecondDelay] <command>")]
        [Example("/ub bct one,two 5000 /say hello", "Runs \"/say hello\" on every client tagged `one` or `two`, with a 5000ms delay between each")]
        [Example("/ub bct three /say hello", "Runs \"/say hello\" on every client tagged `three`, with no delay")]
        [Example("/ub bct \"some tag\",\"another tag\" /say hello", "Runs \"/say hello\" on every client tagged `some tag` or `another tag`, with no delay")]
        [CommandPattern("bct", @"^((?<tags>([^""\s,]+|""[^""]+"")),?)+ (?<delay>\d*) ?(?<command>.*)$")]
        public void DoTaggedBroadcast(string _, Match args) {
            var command = args.Groups["command"].Value;
            var tags = new List<string>();

            for (var i = 0; i < args.Groups["tags"].Captures.Count; i++) {
                tags.Add(args.Groups["tags"].Captures[i].Value.Trim('"'));
            }

            int delay = 0;

            if (!string.IsNullOrEmpty(args.Groups["delay"].Value) && !int.TryParse(args.Groups["delay"].Value, out delay)) {
                Logger.Error($"Unable to broadcast command, invalid delay: {args.Groups["delay"].Value}");
                return;
            }
            if (delay < 0) {
                Logger.Error($"Delay must be greater than zero: {delay}");
                return;
            }
            if (tags.Count() == 0) {
                Logger.Error($"You must specify at least one tag to send the command to.");
                return;
            }

            var clients = Clients.Select(c => c.Value).Where(c => {
                foreach (var tag in tags)
                    if (c.Tags.Contains(tag))
                        return true;
                return false;
            });

            Logger.WriteToChat($"Broadcasting command to clients with tags ({String.Join(",", tags.ToArray())}): \"{command}\" with delay inbetween of {delay}ms");
            DoBroadcast(clients, command, delay);
            if (UB.Plugin.Debug)
                Logger.Debug($"Sent to clients: {string.Join(", ", clients.Select(c => c.Name).ToArray())}");
        }
        #endregion /ub bc <millisecondDelay> <command>
        #region /ub netclients
        [Summary("Lists all available clients on the network, optionally limited to the specified tag.")]
        [Usage("/ub netclients <tag>")]
        [Example("/ub netclients", "Show all clients on the ub network")]
        [Example("/ub netclients one", "Show all clients on the ub network with tag `one`")]
        [CommandPattern("netclients", @"^(?<tags>[a-z,]*)$")]
        public void DoNetClients(string _, Match args) {
            bool showedClients = false;
            var tags = String.IsNullOrEmpty(args.Groups["tags"].Value) ? new string[] { } : args.Groups["tags"].Value.Split(',');
            foreach (var kv in Clients) {
                if (tags == null || tags.Count() == 0 || (tags.Count() > 0 && kv.Value.HasTags(tags.ToList()))) {
                    Logger.WriteToChat($"Client: ({kv.Key}) {kv.Value.Name}//{kv.Value.WorldName}: Tags({(kv.Value.Tags == null ? "null" : String.Join(",", kv.Value.Tags.ToArray()))})", Logger.LogMessageType.Generic, true, false, false);
                    if (UB.Plugin.Debug) {
                        var extra = new StringBuilder("\t");
                        foreach (var prop in kv.Value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                            extra.Append($"{prop.Name}:{prop.GetValue(kv.Value, null)},");
                        }
                        Logger.Debug(extra.ToString());
                    }
                    showedClients = true;
                }
            }
            if (!showedClients)
                Logger.WriteToChat($"No net clients to show");
        }
        #endregion /ub bc <millisecondDelay> <command>
        #endregion Commands

        public Networking(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            StartClient();
            UB.Core.RenderFrame += Core_RenderFrame;
            Tags.Changed += Tags_Changed;
        }

        private void Tags_Changed(object sender, SettingChangedEventArgs e) {
            SendObject(new ClientInfoMessage(UB.CharacterName, UB.WorldName, Tags.Value.ToList()));
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (DateTime.UtcNow - lastClientCleanup > TimeSpan.FromSeconds(3)) {
                    lastClientCleanup = DateTime.UtcNow;
                    var clients = Clients.ToArray();
                    foreach (var client in clients) {
                        if (DateTime.UtcNow - client.Value.LastUpdate > TimeSpan.FromSeconds(15)) {
                            Logger.WriteToChat($"Client Timed Out: {client.Value.WorldName}//{client.Value.Name}");
                            Clients.Remove(client.Key);
                        }
                    }
                }

                while (GameThreadActionQueue.TryDequeue(out Action action)) {
                    action.Invoke();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void StartClient() {
            if (ubNet != null) {
                StopClient();
            }

            Action<Action> runOnMainThread = (a) => { GameThreadActionQueue.Enqueue(a); };
            Action<string> log = (s) => { runOnMainThread.Invoke(() => Logger.LogException(s)); };
            ubNet = new UBClient(ServerHost, ServerPort, log, runOnMainThread, new HotReloadSerializationBinder());
            ubNet.OnMessageReceived += UbNet_OnMessageReceived;
            ubNet.OnRemoteClientConnected += UbNet_OnRemoteClientConnected;
            ubNet.OnRemoteClientDisconnected += UbNet_OnRemoteClientDisconnected;
            AddMessageHandler<LoginMessage>(Handle_LoginMessage);
            AddMessageHandler<PlayerUpdateMessage>(Handle_PlayerUpdateMessage);
            AddMessageHandler<CommandBroadcastMessage>(Handle_CommandBroadcastMessage);
            AddMessageHandler<ClientInfoMessage>(Handle_ClientInfo);
            AddMessageHandler<TrackedItemUpdateMessage>(Handle_TrackedItemUpdateMessage);
            AddMessageHandler<CharacterPositionMessage>(Handle_CharacterPositionMessage);
            ubNet.OnConnected += UbNet_OnConnected;
            ubNet.OnDisconnected += UbNet_OnDisconnected;
            IsRunning = true;
        }

        public void StopClient() {
            if (ubNet == null)
                return;
            ubNet.OnDisconnected -= UbNet_OnDisconnected;
            ubNet.OnMessageReceived -= UbNet_OnMessageReceived;
            ubNet.OnRemoteClientConnected -= UbNet_OnRemoteClientConnected;
            ubNet.OnRemoteClientDisconnected -= UbNet_OnRemoteClientDisconnected;
            RemoveMessageHandler<LoginMessage>(Handle_LoginMessage);
            RemoveMessageHandler<PlayerUpdateMessage>(Handle_PlayerUpdateMessage);
            RemoveMessageHandler<CommandBroadcastMessage>(Handle_CommandBroadcastMessage);
            RemoveMessageHandler<ClientInfoMessage>(Handle_ClientInfo);
            RemoveMessageHandler<TrackedItemUpdateMessage>(Handle_TrackedItemUpdateMessage);
            RemoveMessageHandler<CharacterPositionMessage>(Handle_CharacterPositionMessage);
            ubNet.Dispose();
            IsRunning = false;
        }

        internal void RunOnGameThread(Action action) {
            GameThreadActionQueue.Enqueue(action);
        }

        private ClientInfo GetClient(MessageHeader header) {
            if (!Clients.ContainsKey(header.SendingClientId)) {
                var client = new ClientInfo(header.SendingClientId);
                Clients.Add(header.SendingClientId, client);
            }
            return Clients[header.SendingClientId];
        }

        #region UBNet Message Handlers
        private void Handle_ClientInfo(MessageHeader header, ClientInfoMessage message) {
            var client = GetClient(header);
            if (client == null)
                return;
            //Logger.WriteToChat($"Got ClientInfo from: {client.ClientId} {message.CharacterName}//{message.WorldName} ({(message.Tags == null ? "null" : String.Join(",", message.Tags.ToArray()))})");
            if (message.Tags != null)
                client.Tags = message.Tags;
            client.Name = message.CharacterName;
            client.WorldName = message.WorldName;
            client.LastUpdate = DateTime.UtcNow;
        }

        private void Handle_TrackedItemUpdateMessage(MessageHeader header, TrackedItemUpdateMessage message) {
            var client = GetClient(header);
            client.LastUpdate = DateTime.UtcNow;
            if (message.TrackedItems != null)
                client.TrackedItems = message.TrackedItems;

            //Logger.WriteToChat($"Got tracked items from {client.Name}: {String.Join(", ", client.TrackedItems.Select(i => $"{i.Name}:{i.Count}").ToArray())}");
        }

        private void Handle_CharacterPositionMessage(MessageHeader header, CharacterPositionMessage message) {
            var client = GetClient(header);
            client.HasPositionInfo = true;
            client.Z = message.Z;
            client.NS = message.NS;
            client.EW = message.EW;
            client.LandCell = message.LandCell;
            client.Heading = message.Heading;
            client.LastUpdate = DateTime.UtcNow;
        }

        private void UbNet_OnMessageReceived(object sender, OnMessageEventArgs e) {
            switch (e.Header.Type) {
                case MessageHeaderType.Serialized:
                    if (Clients.TryGetValue(e.Header.SendingClientId, out ClientInfo client))
                        client.LastUpdate = DateTime.UtcNow;
                    break;
            }
        }

        private void HandleDisconnectedClient(int clientId) {
            //Logger.WriteToChat($"Got disconnect: {clientId}");
            if (Clients.TryGetValue(clientId, out ClientInfo client)) {
                Logger.WriteToChat($"Client Disconnected: {client.WorldName}//{client.Name}");
                Clients.Remove(clientId);
            }
        }

        private void Handle_LoginMessage(MessageHeader header, LoginMessage message) {
            var client = GetClient(header);
            //Logger.WriteToChat($"\tGot loginmessage from: {client.ClientId} {message.Name}//{message.WorldName} ({(message.Tags == null ? "null" : String.Join(",", message.Tags.ToArray()))})");
            client.Name = message.Name;
            client.WorldName = message.WorldName;
            if (message.Tags != null)
                client.Tags = message.Tags;
            client.LastUpdate = DateTime.UtcNow;

            if (!client.HasLoginInfo) {
                client.HasLoginInfo = true;
                Logger.WriteToChat($"Client Connected: {client.WorldName}//{client.Name}");
                SendObject(new LoginMessage() {
                    Name = UB.CharacterName,
                    WorldName = UB.WorldName,
                    Tags = Tags.Value.ToList()
                });
            }
        }

        private void Handle_PlayerUpdateMessage(MessageHeader header, PlayerUpdateMessage message) {
            var client = GetClient(header);
            //Logger.WriteToChat($"\tGot PlayerUpdateMessage from: {client.ClientId} {client.Name}//{client.WorldName}");
            client.CurrentHealth = message.CurHealth;
            client.CurrentStamina = message.CurStam;
            client.CurrentMana = message.CurMana;
            client.MaxHealth = message.MaxHealth;
            client.MaxStamina = message.MaxStam;
            client.MaxMana = message.MaxMana;
            client.PlayerId = message.PlayerId;
            client.LastUpdate = DateTime.UtcNow;
        }

        private void Handle_CommandBroadcastMessage(MessageHeader header, CommandBroadcastMessage message) {
            UB.Plugin.AddDelayedCommand(message.Command, message.Delay);
        }
        #endregion UBNet Message Handlers

        #region UBNet events
        private void UbNet_OnConnected(object sender, EventArgs e) {
            try {
                Logger.WriteToChat($"Connected to UBNet", Logger.LogMessageType.Generic, true, false, false);
                Connected = true;
                SendObject(new LoginMessage() {
                    Name = UB.CharacterName,
                    WorldName = UB.WorldName,
                    Tags = Tags.Value.ToList()
                });
                OnConnected?.Invoke(this, e);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UbNet_OnDisconnected(object sender, EventArgs e) {
            try {
                Logger.WriteToChat($"Disconnected from UBNet", Logger.LogMessageType.Generic, true, false, false);
                Connected = false;
                var keys = Clients.Keys.ToArray();
                foreach (var k in keys)
                    Clients.Remove(k);
                OnDisconnected?.Invoke(this, e);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UbNet_OnRemoteClientConnected(object sender, RemoteClientConnectionEventArgs e) {
            OnRemoteClientConnected?.Invoke(this, e);
        }

        private void UbNet_OnRemoteClientDisconnected(object sender, RemoteClientConnectionEventArgs e) {
            OnRemoteClientDisconnected?.Invoke(this, e);
            HandleDisconnectedClient(e.ClientId);
        }
        #endregion UBNet events

        public void SendObject(object obj, int targetClientId=0) {
            ubNet.SendObject(new MessageHeader() {
                SendingClientId = ubNet.ClientId,
                Type = MessageHeaderType.Serialized,
                TargetClientId = targetClientId
            }, obj);
        }

        public virtual Type GetMessageType(string typeStr) {
            var res = typeof(UBClient).Assembly.GetType(typeStr);
            return res == null ? GetType().Assembly.GetType(typeStr) : res;
        }

        #region Network Message Handlers
        /// <summary>
        /// Adds a message handler for the specified type T
        /// </summary>
        /// <typeparam name="T">the message type to subscribe to</typeparam>
        /// <param name="handler">handler method</param>
        public void AddMessageHandler<T>(Action<MessageHeader, T> handler) {
            ubNet.AddMessageHandler(handler);
        }

        /// <summary>
        /// Removes a message handler for the specified type T
        /// </summary>
        /// <typeparam name="T">the message type to subscribe to</typeparam>
        /// <param name="handler">handler method</param>
        public void RemoveMessageHandler<T>(Action<MessageHeader, T> handler) {
            ubNet.RemoveMessageHandler(handler);
        }
        #endregion Network Message Handlers

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Tags.Changed -= Tags_Changed;
            UB.Core.RenderFrame -= Core_RenderFrame;
            StopClient();
        }
    }
}
