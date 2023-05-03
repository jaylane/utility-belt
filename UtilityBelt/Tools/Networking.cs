using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Lib;
using System.ComponentModel;
using System.Windows.Forms;
using UtilityBelt.Service.Lib.Settings;
using System.Collections.Concurrent;
using UtilityBelt.Lib.Networking.Messages;
using System.Text.RegularExpressions;
using System.Threading;
using UtilityBelt.Views;
using System.Collections.ObjectModel;
using UtilityBelt.Lib.Networking;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Reflection;
using UtilityBelt.Lib.Expressions;
using Hellosam.Net.Collections;
using UtilityBelt.Service;
using Newtonsoft.Json;
using UtilityBelt.Networking.Messages;
using AcClient;
using static UtilityBelt.Tools.Networking;
using Decal.Adapter.Wrappers;
using UtilityBelt.Networking.Lib;
using Decal.Adapter;
using UtilityBelt.Networking;
using UtilityBelt.Service.Lib.ACClientModule;
using ProtoBuf;

namespace UtilityBelt.Tools {
    [Name("Networking")]
    [Summary("Provides client communication via a local tcp server.")]
    [FullDescription(@"Allows clients to communicate with each other via a local tcp server.  Clients can  have tags added to them using the setting `Networking.Tags`.  Tags can be used in combination with the `/ub bct` command to broadcast a command to all characters of a specific tag.  See below for examples.")]
    public class Networking : ToolBase {
        private DateTime lastClientDataSend = DateTime.UtcNow;

        public event EventHandler<EventArgs> OnConnected;
        public event EventHandler<EventArgs> OnDisconnected;
        public event EventHandler<RemoteClientEventArgs> OnRemoteClientConnected;
        public event EventHandler<RemoteClientEventArgs> OnRemoteClientDisconnected;
        public event EventHandler<RemoteClientEventArgs> OnRemoteClientUpdated;


        public readonly ObservableCollection<ClientData> Clients = new ObservableCollection<ClientData>();

        public class RemoteClientEventArgs : EventArgs {
            public ClientData ClientData { get; set; }

            public RemoteClientEventArgs(ClientData clientData) {
                ClientData = clientData;
            }
        }

        #region Config
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
            UB.Plugin.AddDelayedCommand(command, 0);
            int currentDelay = 0;
            foreach (var client in Clients.ToList()) {
                currentDelay += delay;
                Logger.WriteToChat($"Sending {client.Name}: \"{command}\" with delay inbetween of {delay}ms");
                TypedBroadcast<CommandBroadcastResponse, CommandBroadcastRequest>(new CommandBroadcastRequest(command, currentDelay), new ClientFilter(client.Id),
                    (sendingClientId, currentRequest, totalRequests, success, response) => {
                        if (sendingClientId > 0) {
                            var remote = Clients.FirstOrDefault(c => c.Id == sendingClientId);
                            if (remote is not null) {
                                WriteToChat($"{remote} ack'd command broadcast request. ({command} /// {currentDelay}ms)");
                            }
                        }
                    });
            }
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

            Logger.WriteToChat($"Broadcasting command to clients with tags ({String.Join(",", tags.ToArray())}): \"{command}\" with delay inbetween of {delay}ms");
            if (tags.Where(t => UBService.UBNet.UBNetClient.Tags.Contains(t)).Any()) {
                UB.Plugin.AddDelayedCommand(command, 0);
            }
            int currentDelay = 0;
            foreach (var client in Clients.Where(c => c.HasAnyTags(tags)).ToList()) {
                currentDelay += delay;
                TypedBroadcast<CommandBroadcastResponse, CommandBroadcastRequest>(new CommandBroadcastRequest(command, currentDelay), new ClientFilter(client.Id),
                    (sendingClientId, currentRequest, totalRequests, success, response) => {
                        if (sendingClientId > 0) {
                            var remote = Clients.FirstOrDefault(c => c.Id == sendingClientId);
                            if (remote is not null) {
                                //WriteToChat($"{remote} ack'd command broadcast request. ({command} /// {currentDelay}ms)");
                            }
                        }
                    });
            }
        }

        private void Tags_Changed(object sender, SettingChangedEventArgs e) {
            UBService.UBNet?.UBNetClient?.AddTags(Tags.Value);
        }
        #endregion /ub bc <millisecondDelay> <command>
        #region /ub netclients
        [Summary("Lists all available clients on the network, optionally limited to the specified tag.")]
        [Usage("/ub netclients <tag>")]
        [Example("/ub netclients", "Show all clients on the ub network")]
        [Example("/ub netclients one", "Show all clients on the ub network with tag `one`")]
        [CommandPattern("netclients", @"^((?<tags>([^""\s,]+|""[^""]+"")),?)*$")]
        public void DoNetClients(string _, Match args) {
            bool showedClients = false;
            var tags = new List<string>();

            for (var i = 0; i < args.Groups["tags"].Captures.Count; i++) {
                tags.Add(args.Groups["tags"].Captures[i].Value.Trim('"'));
            }

            foreach (var client in UBService.UBNet.UBNetClient.Clients.Values) {
                if (tags.Count() == 0 || tags.Any(t => client.Tags.Contains(t))) {
                    Logger.WriteToChat(client.ToString(), Logger.LogMessageType.Generic, true, false, false);
                    showedClients = true;
                }
            }
            if (!showedClients)
                Logger.WriteToChat($"No net clients to show");
        }
        #endregion /ub bc <millisecondDelay> <command>
        #endregion Commands

        #region Expressions
        #region netclients[number spellid]
        [ExpressionMethod("netclients")]
        [ExpressionParameter(0, typeof(string), "tag", "Optional network tag to filter by")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of network client data, optionally filtered by tag")]
        [Summary("Gets a list of network clients, optionally filtered by tag")]
        [Example("netclients[]", "returns a list of all network clients")]
        [Example("netclients[test]", "returns a list of all network clients with the tag `test`")]
        public object netclients(string tag = null) {
            var clients = new ExpressionList();
            foreach (var client in Clients) {
                if (string.IsNullOrEmpty(tag) || client.HasAnyTags(new List<string>() { tag })) {
                    var clientTags = new ExpressionList();
                    clientTags.AddRange(client.RemoteClient.Tags.Select(x => (object)x));
                    var clientData = new ExpressionDictionary();
                    clientData.Items.Add("ClientId", (double)client.Id);
                    clientData.Items.Add("PlayerId", (double)client.PlayerId);
                    clientData.Items.Add("Position", new ExpressionCoordinates((double)client.EW, (double)client.NS, (double)client.Z));
                    clientData.Items.Add("Name", client.Name);
                    clientData.Items.Add("Tags", clientTags);
                    clientData.Items.Add("WorldName", client.WorldName);
                    clientData.Items.Add("CurrentHealth", (double)client.CurrentHealth);
                    clientData.Items.Add("CurrentMana", (double)client.CurrentMana);
                    clientData.Items.Add("CurrentStamina", (double)client.CurrentStamina);
                    clientData.Items.Add("MaxHealth", (double)client.MaxHealth);
                    clientData.Items.Add("MaxMana", (double)client.MaxMana);
                    clientData.Items.Add("MaxStamina", (double)client.MaxStamina);
                    clientData.Items.Add("Heading", (double)client.Heading);
                    clients.Items.Add(clientData);
                }
            }

            return clients;
        }
        #endregion //netclients[number spellid]
        #endregion Expressions

        public Networking(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            try {
                UBService.UBNet.UBNetClient.OnRemoteClientConnected += UBNetClient_OnRemoteClientConnected;
                UBService.UBNet.UBNetClient.OnRemoteClientUpdated += UBNetClient_OnRemoteClientUpdated;
                UBService.UBNet.UBNetClient.OnRemoteClientDisconnected += UBNetClient_OnRemoteClientDisconnected;
                UBService.UBNet.UBNetClient.OnConnected += UBNetClient_OnConnected;
                UBService.UBNet.UBNetClient.OnDisconnected += UBNetClient_OnDisconnected;

                UBService.UBNet.UBNetClient.OnTypedChannelMessage += UBNetClient_OnTypedChannelMessage;
                UBService.UBNet.UBNetClient.OnTypedBroadcastRequest += UBNetClient_OnTypedBroadcastRequest;

                UBService.UBNet.UBNetClient.SubscribeToChannel("utilitybelt");

                if (UBHelper.Core.GameState == UBHelper.GameState.In_Game) {
                    InitClients();
                }
                else {
                    UBHelper.Core.GameStateChanged += Core_GameStateChanged;
                }

                Tags.Changed += Tags_Changed;

                Tags_Changed(null, null);
            }
            catch (Exception ex) { Logger.LogException(ex); }

        }

        private void UBNetClient_OnTypedBroadcastRequest(object sender, TypedBroadcastEventArgs e) {
            if (e.IsType(typeof(CommandBroadcastRequest)) && e.TryDeserialize<CommandBroadcastRequest>(out var commandMessage)) {
                UB.Plugin.AddDelayedCommand(commandMessage.Command, commandMessage.Delay);
                UBService.UBNet?.UBNetClient?.SendBroadcastResponse(new CommandBroadcastResponse(true, commandMessage.Command, commandMessage.Delay), e.Message);
            }
        }

        private void UBNetClient_OnTypedChannelMessage(object sender, TypedChannelMessageReceivedEventArgs e) {
            var client = Clients.FirstOrDefault(c => c.Id == e.Message.SendingClientId);
            if (client is null)
                return;
            if (e.IsType(typeof(ClientData)) && e.TryDeserialize<ClientData>(out var clientData)) {
                HandleClientData(e.Message.SendingClientId, clientData);
            }
            else if (e.IsType(typeof(TrackedItemUpdateMessage)) && e.TryDeserialize<TrackedItemUpdateMessage>(out var trackedItemUpdateMessage)) {
                HandleTrackedItemUpdateMessage(e.Message.SendingClientId, trackedItemUpdateMessage);
            }
            else if (e.IsType(typeof(CharacterPositionMessage)) && e.TryDeserialize<CharacterPositionMessage>(out var characterPositionMessage)) {
                HandleCharacterPositionMessage(e.Message.SendingClientId, characterPositionMessage);
            }
            else if (e.IsType(typeof(VitalUpdateMessage)) && e.TryDeserialize<VitalUpdateMessage>(out var vitalUpdateMessage)) {
                HandleVitalUpdateMessage(e.Message.SendingClientId, vitalUpdateMessage);
            }

            if (DateTime.UtcNow - lastClientDataSend > TimeSpan.FromSeconds(5)) {
                SendClientData(true);
                lastClientDataSend = DateTime.UtcNow;
            }
        }

        private void Core_GameStateChanged(UBHelper.GameState previous, UBHelper.GameState new_state) {
            try {
                if (new_state == UBHelper.GameState.In_Game) {
                    UBHelper.Core.GameStateChanged -= Core_GameStateChanged;
                    InitClients();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void InitClients() {
            try {
                if (UBService.UBNet.UBNetClient?.IsConnected == true) {
                    foreach (var client in UBService.UBNet.UBNetClient.Clients.Values) {
                        var clientData = new ClientData(client.Id);
                        if (!Clients.Where(c => c.Id == client.Id).Any()) {
                            Clients.Add(clientData);
                        }
                    }

                    SendClientData(true);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal void ChannelBroadcast<T>(string channel, T obj) {
            try {
                UBService.UBNet.UBNetClient.PublishToChannel(channel, obj);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal void TypedBroadcast<TResponse, TRequest>(TRequest obj, ClientFilter filter = null, Action<uint, uint, uint, bool, TResponse> progressHandler = null, TimeSpan? timeout = null) {
            try {
                UBService.UBNet.UBNetClient.SendBroadcastRequest(obj, filter, progressHandler, timeout);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UBNetClient_OnConnected(object sender, EventArgs e) {
            try {
                foreach (var client in UBService.UBNet.UBNetClient.Clients.Values) {
                    if (client.Type != ClientType.GameClient || UBService.UBNet.UBNetClient.Id == client.Id)
                        continue;
                    var clientData = new ClientData(client.Id);
                    if (!Clients.Where(c => c.Id == client.Id).Any()) {
                        Clients.Add(clientData);
                        OnRemoteClientConnected?.Invoke(this, new RemoteClientEventArgs(clientData));
                    }
                }

                SendClientData(true);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UBNetClient_OnDisconnected(object sender, EventArgs e) {
            try {
                foreach (var client in Clients.ToArray()) {
                    OnRemoteClientDisconnected?.Invoke(this, new RemoteClientEventArgs(client));
                }
                Clients.Clear();
                OnDisconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UBNetClient_OnRemoteClientUpdated(object sender, ClientEventArgs e) {
            try {
                if (e.Client.Type != ClientType.GameClient || UBService.UBNet.UBNetClient.Id == e.Client.Id)
                    return;
                var existing = Clients.FirstOrDefault(c => c.Id == e.Client.Id);
                if (existing == null) {
                    var clientData = new ClientData(e.Client.Id);
                    Clients.Add(clientData);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UBNetClient_OnRemoteClientConnected(object sender, ClientEventArgs e) {
            try {
                if (e.Client.Type != ClientType.GameClient || UBService.UBNet.UBNetClient.Id == e.Client.Id)
                    return;

                var existing = Clients.FirstOrDefault(c => c.Id == e.Client.Id);
                if (existing == null) {
                    var clientData = new ClientData(e.Client.Id);
                    Clients.Add(clientData);
                    OnRemoteClientConnected?.Invoke(this, new RemoteClientEventArgs(clientData));
                }

                SendClientData(true);

                WriteToChat($"{e.Client.Name} connected.");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UBNetClient_OnRemoteClientDisconnected(object sender, ClientEventArgs e) {
            try {
                if (e.Client.Type != ClientType.GameClient || UBService.UBNet.UBNetClient.Id == e.Client.Id)
                    return;

                var existing = Clients.FirstOrDefault(c => c.Id == e.Client.Id);
                if (existing != null) {
                    Clients.Remove(existing);
                    OnRemoteClientDisconnected?.Invoke(this, new RemoteClientEventArgs(existing));
                }
                WriteToChat($"{e.Client.Name} disconnected.");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal void SendClientData(bool forced) {
            try {
                if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                    return;

                var data = GetClientData();
                if (data is not null) {
                    ChannelBroadcast("utilitybelt", data);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal ClientData GetClientData() {
            try {
                if (UBHelper.vTank.Instance == null || !UB.VTank.VitalSharing || UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                    return null;

                var me = UtilityBeltPlugin.Instance.Core.CharacterFilter.Id;
                var pos = PhysicsObject.GetPosition(me);
                var lc = PhysicsObject.GetLandcell(me);
                return new ClientData(UBService.UBNet.UBNetClient.Id) {
                    PlayerId = UB.Core.CharacterFilter.Id,
                    CurrentHealth = UB.Core.Actions.Vital[VitalType.CurrentHealth],
                    CurrentStamina = UB.Core.Actions.Vital[VitalType.CurrentStamina],
                    CurrentMana = UB.Core.Actions.Vital[VitalType.CurrentMana],

                    MaxHealth = UB.Core.Actions.Vital[VitalType.MaximumHealth],
                    MaxStamina = UB.Core.Actions.Vital[VitalType.MaximumStamina],
                    MaxMana = UB.Core.Actions.Vital[VitalType.MaximumMana],

                    Z = pos.Z,
                    EW = Geometry.LandblockToEW((uint)lc, pos.X),
                    NS = Geometry.LandblockToNS((uint)lc, pos.Y),
                    Heading = UtilityBeltPlugin.Instance.Core.Actions.Heading,
                    LandCell = (uint)lc,

                    WorldName = UB.Core.CharacterFilter.Server,

                    TrackedItems = UB.VTankFellowHeals.GetTrackedItems()
                };
            }
            catch {
                return null;
            }
        }

        #region UBNet Message Handlers
        private void HandleCharacterPositionMessage(uint sendingClientId, CharacterPositionMessage positionMessage) {
            try {
                if (UBService.UBNet.UBNetClient.Id == sendingClientId)
                    return;
                if (!UBService.UBNet.UBNetClient.Clients.Values.Where(c => c.Id == sendingClientId).Any())
                    return;
                var existing = Clients.FirstOrDefault(c => c.Id == sendingClientId);
                if (existing is not null) {
                    existing.HasPositionInfo = true;
                    existing.Z = positionMessage.Z;
                    existing.NS = positionMessage.NS;
                    existing.EW = positionMessage.EW;
                    existing.LandCell = (uint)positionMessage.LandCell;
                    existing.Heading = positionMessage.Heading;
                    OnRemoteClientUpdated?.Invoke(this, new RemoteClientEventArgs(existing));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void HandleTrackedItemUpdateMessage(uint sendingClientId, TrackedItemUpdateMessage trackedItemUpdate) {
            try {
                if (UBService.UBNet.UBNetClient.Id == sendingClientId)
                    return;
                if (!UBService.UBNet.UBNetClient.Clients.Values.Where(c => c.Id == sendingClientId).Any())
                    return;
                var existing = Clients.FirstOrDefault(c => c.Id == sendingClientId);
                if (existing is not null) {
                    if (trackedItemUpdate.TrackedItems != null)
                        existing.TrackedItems = trackedItemUpdate.TrackedItems;
                    OnRemoteClientUpdated?.Invoke(this, new RemoteClientEventArgs(existing));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void HandleVitalUpdateMessage(uint sendingClientId, VitalUpdateMessage vitalUpdate) {
            try {
                if (UBService.UBNet.UBNetClient.Id == sendingClientId)
                    return;
                var existing = Clients.FirstOrDefault(c => c.Id == sendingClientId);
                if (existing is not null) {
                    existing.HasVitalInfo = true;
                    existing.CurrentHealth = vitalUpdate.CurrentHealth;
                    existing.CurrentMana = vitalUpdate.CurrentMana;
                    existing.CurrentStamina = vitalUpdate.CurrentStamina;
                    existing.MaxHealth = vitalUpdate.MaxHealth;
                    existing.MaxMana = vitalUpdate.MaxMana;
                    existing.MaxStamina = vitalUpdate.MaxStamina;
                    OnRemoteClientUpdated?.Invoke(this, new RemoteClientEventArgs(existing));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void HandleClientData(uint sendingClientId, ClientData clientData) {
            try {
                if (UBService.UBNet.UBNetClient.Id == sendingClientId)
                    return;
                if (!UBService.UBNet.UBNetClient.Clients.Values.Where(c => c.Id == sendingClientId).Any()) {
                    return;
                }
                var existing = Clients.FirstOrDefault(c => c.Id == sendingClientId);
                if (existing is not null) {
                    existing.CurrentHealth = clientData.CurrentHealth;
                    existing.CurrentMana = clientData.CurrentMana;
                    existing.CurrentStamina = clientData.CurrentStamina;
                    existing.EW = clientData.EW;
                    existing.HasPositionInfo = clientData.HasPositionInfo;
                    existing.Heading = clientData.Heading;
                    existing.LandCell = clientData.LandCell;
                    existing.MaxHealth = clientData.MaxHealth;
                    existing.MaxMana = clientData.MaxMana;
                    existing.MaxStamina = clientData.MaxStamina;
                    existing.NS = clientData.NS;
                    existing.PlayerId = clientData.PlayerId;
                    existing.TrackedItems = clientData.TrackedItems;
                    existing.WorldName = clientData.WorldName;
                    existing.Z = clientData.Z;
                }
                else {
                    existing = clientData;
                    Clients.Add(clientData);
                }
                OnRemoteClientUpdated?.Invoke(this, new RemoteClientEventArgs(clientData));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion UBNet Message Handlers

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);


            try {
                UBService.UBNet.UBNetClient.UnsubscribeFromChannel("utilitybelt");

                UBService.UBNet.UBNetClient.OnRemoteClientConnected -= UBNetClient_OnRemoteClientConnected;
                UBService.UBNet.UBNetClient.OnRemoteClientUpdated -= UBNetClient_OnRemoteClientUpdated;
                UBService.UBNet.UBNetClient.OnRemoteClientDisconnected -= UBNetClient_OnRemoteClientDisconnected;
                UBService.UBNet.UBNetClient.OnConnected -= UBNetClient_OnConnected;
                UBService.UBNet.UBNetClient.OnDisconnected -= UBNetClient_OnDisconnected;

                UBService.UBNet.UBNetClient.OnTypedChannelMessage -= UBNetClient_OnTypedChannelMessage;
            }
            catch { }
        }
    }
}
