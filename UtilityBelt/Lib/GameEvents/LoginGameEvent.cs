using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Lib.GameEvents {
    [Summary("Triggered when the character first logs in, before it is controllable.")]
    public class LoginGameEvent : BaseGameEvent {
        [Summary("The id of the character logging in")]
        public int Id { get; private set; }

        [Summary("The name of the character logging in")]
        public string Name { get; private set; }

        public LoginGameEvent() : base() {
            Type = GameEventType.Login;
        }

        public override void AddEventSubscription() {
            UBHelper.Core.GameStateChanged += Core_GameStateChanged;
        }

        public override void RemoveEventSubscription() {
            UBHelper.Core.GameStateChanged -= Core_GameStateChanged;
        }

        public void Core_GameStateChanged(UBHelper.GameState previous, UBHelper.GameState new_state) {
            if (new_state == UBHelper.GameState.PlayerDesc_Received) {
                Id = UBHelper.Core.LoginCharacterID;
                Name = UBHelper.Core.CharacterSet[Id];
                FireEvent();
            }
        }
    }
}
