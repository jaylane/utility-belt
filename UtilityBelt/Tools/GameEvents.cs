using System;
using System.Collections.Generic;
using System.Linq;
using UtilityBelt.Lib;
using System.Text.RegularExpressions;
using Decal.Adapter.Wrappers;
using Decal.Adapter;
using System.Runtime.InteropServices;
using UBService.Lib.Settings;
using VirindiViewService.Controls;
using System.Collections.ObjectModel;
using System.IO;
using UtilityBelt.Lib.Actions;
using UtilityBelt.Views;
using Newtonsoft.Json;
using System.ComponentModel;
using UtilityBelt.Lib.GameEvents;
using static UtilityBelt.Lib.GameEvents.BaseGameEvent;
using System.Reflection;
using UtilityBelt.Lib.Expressions;

namespace UtilityBelt.Tools {
    [Name("GameEvents")]
    [Summary("Allows actions to be triggered on certain game events.")]
    [FullDescription(@"
Allows you to run actions when certain game events take place. Multiple actions can be added to each event.

Clicking the `?` button next to the event type dropdown will output to chat the available expression variables for the specified event.  For example the login event makes the character id available in the `$gevt_id` expression variable, and the character name available in the `$gevt_name` variable.

You can share a set of defined event handlers with multiple characters by using the `Profiles` tab in the main plugin window. Setting the profile the `[character]` will make the defined event handlers unique to this character.

### Examples

#### Run the command `/framerate` on login
* **Event:** Login
* **Action:** ChatCommand
* **Command:** `/framerate`

#### Tell your fellowship the items you lost when your character dies
* **Event:** CharacterDeath
* **Action:** ChatExpression
* **Command:** `\/f I died and lost\: +$gevt_droppeditems`
    ")]
    public class GameEvents : ToolBase {
        /// <summary>
        /// The default character GameEvents profile path
        /// </summary>
        public string CharacterGameEventsFile { get => Path.Combine(Util.GetCharacterDirectory(), GameEventsProfileExtension); }

        /// <summary>
        /// The file path to the currently loaded GameEvents profile
        /// </summary>
        public string GameEventsProfilePath {
            get {
                if (Profile == "[character]")
                    return CharacterGameEventsFile;
                else
                    return Path.Combine(Util.GetProfilesDirectory(), $"{Profile}.{GameEventsProfileExtension}");
            }
        }

        public static readonly string GameEventsProfileExtension = "events.json";

        #region Config
        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("GameEvents profile. Set to `[character]` to use a private set of GameEvents for only this character.")]
        public readonly CharacterState<string> Profile = new CharacterState<string>("[character]");

        [Summary("Defined aliases")]
        public readonly GameEvent<ObservableCollection<UBGameEvent>> GameEventHandlers = new GameEvent<ObservableCollection<UBGameEvent>>(new ObservableCollection<UBGameEvent>());
        #endregion // Config

        HudList UIGameEventsList;
        HudCombo UIGameEventEventCombo;
        HudButton UIGameEventUpdate;
        HudButton UIGameEventCancel;
        HudCombo UIGameEventActionType;
        HudFixedLayout UIGameEventActionFormLayout;
        HudButton UIGameEventEventInfo;

        private bool started = false;
        private UBGameEvent editingGameEvent;
        private BaseAction action;

        Dictionary<BaseGameEvent.GameEventType, BaseGameEvent> events = new Dictionary<BaseGameEvent.GameEventType, BaseGameEvent>();
        Dictionary<BaseGameEvent.GameEventType, bool> eventSubscriptions = new Dictionary<BaseGameEvent.GameEventType, bool>();

        public GameEvents(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            UIGameEventsList = (HudList)UB.MainView.view["GameEventsList"];
            UIGameEventEventCombo = (HudCombo)UB.MainView.view["GameEventEventCombo"];
            UIGameEventUpdate = (HudButton)UB.MainView.view["GameEventUpdate"];
            UIGameEventCancel = (HudButton)UB.MainView.view["GameEventCancel"];
            UIGameEventActionType = (HudCombo)UB.MainView.view["GameEventActionType"];
            UIGameEventActionFormLayout = (HudFixedLayout)UB.MainView.view["GameEventActionFormLayout"];
            UIGameEventEventInfo = (HudButton)UB.MainView.view["GameEventEventInfo"];

            foreach (var item in Enum.GetValues(typeof(ActionType)).Cast<ActionType>())
                UIGameEventActionType.AddItem(item.ToString(), item.ToString());

            UIGameEventActionType.Change += UIGameEventsActionType_Change;
            UIGameEventUpdate.Hit += UIGameEventsUpdate_Hit;
            UIGameEventCancel.Hit += UIGameEventsCancel_Hit;
            UIGameEventsList.Click += UIGameEventsList_Click;
            UIGameEventEventInfo.Hit += UIGameEventEventInfo_Hit;

            UIGameEventCancel.Visible = false;

            TryEnable();
            Enabled.Changed += Enabled_Changed;
            GameEventHandlers.Changed += GameEventHandlers_Changed;

            foreach (BaseGameEvent.GameEventType eventType in Enum.GetValues(typeof(BaseGameEvent.GameEventType))) {
                UIGameEventEventCombo.AddItem(eventType.ToString(), eventType.ToString());
                var gameEvent = BaseGameEvent.FromType(eventType);
                events.Add(eventType, gameEvent);
                eventSubscriptions.Add(eventType, false);
                gameEvent.Fired += GameEvent_Fired;
            }

            UpdateList();
            DrawActionForm();
            UpdateEventRegistrations();
        }

        #region UI Event Handlers
        private void UIGameEventsList_Click(object sender, int row, int col) {
            switch (col) {
                case 0: // event
                case 1: // action
                    Edit(GameEventHandlers.Value[row]);
                    break;
                case 2: // delete
                    GameEventHandlers.Value.RemoveAt(row);
                    break;
            }
        }

        private void UIGameEventsActionType_Change(object sender, EventArgs e) {
            DrawActionForm();
        }

        private void UIGameEventsUpdate_Hit(object sender, EventArgs e) {
            var gameEventTypeStr = ((HudStaticText)UIGameEventEventCombo[UIGameEventEventCombo.Current]).Text;
            var gameEventType = (GameEventType)Enum.Parse(typeof(GameEventType), gameEventTypeStr);
            if (editingGameEvent != null) {
                var index = GameEventHandlers.Value.IndexOf(editingGameEvent);
                editingGameEvent.GameEventType = gameEventType;
                editingGameEvent.Action = action;
                GameEventHandlers.Value.RemoveAt(index);
                GameEventHandlers.Value.Insert(index, editingGameEvent);
            }
            else {
                var gameEvent = new UBGameEvent(gameEventType, action);
                GameEventHandlers.Value.Add(gameEvent);
            }
            ClearForm();
        }

        private void UIGameEventsCancel_Hit(object sender, EventArgs e) {
            ClearForm();
        }

        private void UIGameEventEventInfo_Hit(object sender, EventArgs e) {
            var gameEventTypeStr = ((HudStaticText)UIGameEventEventCombo[UIGameEventEventCombo.Current]).Text;
            var classType = Type.GetType($"UtilityBelt.Lib.GameEvents.{gameEventTypeStr}GameEvent");
            var propOutput = "";
            var output = $"Event: {gameEventTypeStr}\n";
            var classSummaryAttrs = classType.GetCustomAttributes(typeof(SummaryAttribute), false);

            if (classSummaryAttrs.Length == 1)
                output += $"Summary: {((SummaryAttribute)classSummaryAttrs[0]).Summary}\n";

            var props = classType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props) {
                var propSummaryAttrs = prop.GetCustomAttributes(typeof(SummaryAttribute), false);
                if (propSummaryAttrs.Length == 1)
                    propOutput += $"  $gevt_{prop.Name.ToLower()} ({ExpressionVisitor.GetFriendlyType(prop.PropertyType)}): {((SummaryAttribute)propSummaryAttrs[0]).Summary}\n";
            }

            if (!string.IsNullOrEmpty(propOutput)) {
                output += "Event Variables:\n";
                output += propOutput;
            }

            Logger.WriteToChat(output);
        }
        #endregion UI Event Handlers

        private void UpdateList() {
            UIGameEventsList.ClearRows();
            foreach (var gameEvent in GameEventHandlers.Value) {
                var row = UIGameEventsList.AddRow();

                ((HudStaticText)row[0]).Text = gameEvent.GameEventType.ToString();
                ((HudStaticText)row[1]).Text = gameEvent.Action.ToString();
                ((HudPictureBox)row[2]).Image = 0x060011F8; // delete
            }
        }

        private void DrawActionForm() {
            action?.ClearForm(UIGameEventActionFormLayout);
            var type = ((HudStaticText)UIGameEventActionType[UIGameEventActionType.Current]).Text;
            action = BaseAction.FromType((ActionType)Enum.Parse(typeof(ActionType), type));
            action.DrawForm(UIGameEventActionFormLayout);
        }

        private void ClearForm() {
            editingGameEvent = null;
            action?.ClearForm(UIGameEventActionFormLayout);
            UIGameEventCancel.Visible = false;
            UIGameEventUpdate.Text = "Add";
            UIGameEventActionType.Current = 0;
            UIGameEventEventCombo.Current = 0;
            action?.ClearForm(UIGameEventActionFormLayout);
            action = BaseAction.FromType(0);
            action.DrawForm(UIGameEventActionFormLayout);
        }

        private void Edit(UBGameEvent gameEvent) {
            ClearForm();
            action.ClearForm(UIGameEventActionFormLayout);
            editingGameEvent = gameEvent;
            UIGameEventActionType.Current = (int)editingGameEvent.Action.Type;
            UIGameEventEventCombo.Current = (int)editingGameEvent.GameEventType;
            action = editingGameEvent.Action.Clone();
            action.DrawForm(UIGameEventActionFormLayout);
            UIGameEventUpdate.Text = "Update";
            UIGameEventCancel.Visible = true;
        }

        private void TryEnable() {
            if (Enabled && !started) {
                UpdateEventRegistrations();
                started = true;
            }
            else if (!Enabled && started) {
                RemoveEventRegistrations();
                started = false;
            }
        }

        private void UpdateEventRegistrations() {
            var enabledTypes = new BaseGameEvent.GameEventType[GameEventHandlers.Value.Count];
            for (var i = 0; i < GameEventHandlers.Value.Count; i++) {
                enabledTypes[i] = GameEventHandlers.Value[i].GameEventType;
            }
            foreach (var e in events) {
                var shouldSubscribe = enabledTypes.Contains(e.Key);
                if (shouldSubscribe && !eventSubscriptions[e.Key]) {
                    e.Value.AddEventSubscription();
                    eventSubscriptions[e.Key] = true;
                }
                else if (!shouldSubscribe && eventSubscriptions[e.Key]) {
                    e.Value.RemoveEventSubscription();
                    eventSubscriptions[e.Key] = false;
                }
            }
        }

        private void RemoveEventRegistrations() {
            foreach (var e in events) {
                if (eventSubscriptions[e.Key]) {
                    e.Value.RemoveEventSubscription();
                    eventSubscriptions[e.Key] = false;
                }
            }
        }

        private void GameEvent_Fired(object sender, GameEventFiredEventArgs e) {
            var registeredEvents = GameEventHandlers.Value.Where(ge => ge.GameEventType == e.EventType);
            foreach (var variable in e.Variables) {
                UB.VTank.Setvar($"gevt_{variable.Key}", variable.Value);
            }
            foreach (var registeredEvent in registeredEvents) {
                registeredEvent.Action.Run();
            }
        }

        private void Enabled_Changed(object sender, SettingChangedEventArgs e) {
            TryEnable();
        }

        private void GameEventHandlers_Changed(object sender, SettingChangedEventArgs e) {
            UpdateEventRegistrations();
            UpdateList();
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            RemoveEventRegistrations();
            foreach (var e in events) {
                e.Value.Fired -= GameEvent_Fired;
            }
            GameEventHandlers.Changed -= GameEventHandlers_Changed;
            Enabled.Changed -= Enabled_Changed;
        }
    }
}
