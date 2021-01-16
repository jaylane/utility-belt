using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Lib.GameEvents {
    [Summary("Triggered when the character starts the logging out process")]
    public class LogoutGameEvent : BaseGameEvent {
        [Summary("The id of the character logging out")]
        public int Id { get; private set; }

        [Summary("The name of the character logging out")]
        public string Name { get; private set; }

        public LogoutGameEvent() : base() {
            Type = GameEventType.Logout;
        }

        public override void AddEventSubscription() {
            UBHelper.Core.GameStateChanged += Core_GameStateChanged;
        }

        public override void RemoveEventSubscription() {
            UBHelper.Core.GameStateChanged -= Core_GameStateChanged;
        }

        public void Core_GameStateChanged(UBHelper.GameState previous, UBHelper.GameState new_state) {
            if (new_state == UBHelper.GameState.Logging_Out) {
                Id = UBHelper.Core.LoginCharacterID;
                Name = UBHelper.Core.CharacterSet[Id];
                FireEvent();
            }
        }
    }
}
