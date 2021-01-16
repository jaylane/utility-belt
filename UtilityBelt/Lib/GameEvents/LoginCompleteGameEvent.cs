using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Lib.GameEvents {
    [Summary("Triggered when the character is completely logged in, and ready to control.")]
    public class LoginCompleteGameEvent : BaseGameEvent {
        [Summary("The id of the character that just logged in")]
        public int Id { get; private set; }

        [Summary("The name of the character that just logging in")]
        public string Name { get; private set; }

        public LoginCompleteGameEvent() : base() {
            Type = GameEventType.LoginComplete;
        }

        public override void AddEventSubscription() {
            UBHelper.Core.GameStateChanged += Core_GameStateChanged;
        }

        public override void RemoveEventSubscription() {
            UBHelper.Core.GameStateChanged -= Core_GameStateChanged;
        }

        public void Core_GameStateChanged(UBHelper.GameState previous, UBHelper.GameState new_state) {
            if (new_state == UBHelper.GameState.In_Game) {
                Id = UBHelper.Core.LoginCharacterID;
                Name = UBHelper.Core.CharacterSet[Id];
                FireEvent();
            }
        }
    }
}
