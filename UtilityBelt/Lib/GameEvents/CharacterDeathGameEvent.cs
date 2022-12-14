using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Service.Lib.Settings;

namespace UtilityBelt.Lib.GameEvents {
    [Summary("Triggered when the character dies.")]
    public class CharacterDeathGameEvent : BaseGameEvent {
        [Summary("The death message, eg: `You are liquified by Olthoi Slasher's attack!`")]
        public string DeathMessage { get; private set; }

        [Summary("Items dropped, eg: `70 Pyreals, and your 4 Resister's Crystals!`")]
        public string DroppedItems { get; private set; }

        public CharacterDeathGameEvent() : base() {
            Type = GameEventType.CharacterDeath;
        }

        public override void AddEventSubscription() {
            CoreManager.Current.CharacterFilter.Death += CharacterFilter_Death;
            CoreManager.Current.ChatBoxMessage += Current_ChatBoxMessage;
        }

        private void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            //You've lost 70 Pyreals, your 23 Pearls of Spirit Drinking, your 2 Evader's Crystals, and your 4 Resister's Crystals!
            if (e.Text.StartsWith("You've lost ")) {
                DroppedItems = e.Text.Replace("You've lost ", "");
                // lost items message comes after CharacterFilter_Death so we fire the event here
                FireEvent();
            }
            //Check for no items dropped and PK lite
            if (e.Text.StartsWith("You have retained all your items.") || 
                e.Text.StartsWith("You are enveloped in a feeling of warmth")) {
                DroppedItems = "";
                FireEvent();
            }
        }

        private void CharacterFilter_Death(object sender, Decal.Adapter.Wrappers.DeathEventArgs e) {
            DeathMessage = e.Text;
        }

        public override void RemoveEventSubscription() {
            CoreManager.Current.CharacterFilter.Death -= CharacterFilter_Death;
            CoreManager.Current.ChatBoxMessage -= Current_ChatBoxMessage;
        }
    }
}
