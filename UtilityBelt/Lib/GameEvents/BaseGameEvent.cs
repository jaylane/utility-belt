using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UBService.Lib.Settings;

namespace UtilityBelt.Lib.GameEvents {
    public class GameEventFiredEventArgs : EventArgs {
        public BaseGameEvent.GameEventType EventType { get; }
        public Dictionary<string, object> Variables { get; }

        public GameEventFiredEventArgs(BaseGameEvent.GameEventType eventType, Dictionary<string, object> variables) {
            EventType = eventType;
            Variables = variables;
        }
    }

    public abstract class BaseGameEvent {
        public event EventHandler<GameEventFiredEventArgs> Fired;
        public enum GameEventType {
            Login = 0,
            LoginComplete = 1,
            Logout = 2,
            CharacterDeath = 3,
        }

        public static BaseGameEvent FromType(GameEventType type) {
            switch (type) {
                case GameEventType.Login:
                    return new LoginGameEvent();
                case GameEventType.LoginComplete:
                    return new LoginCompleteGameEvent();
                case GameEventType.Logout:
                    return new LogoutGameEvent();
                case GameEventType.CharacterDeath:
                    return new CharacterDeathGameEvent();
                default:
                    return null;
            }
        }

        public GameEventType Type { get; set; }

        public BaseGameEvent() {
        }

        public abstract void AddEventSubscription();

        public abstract void RemoveEventSubscription();

        public Dictionary<string, object> GetVariables() {
            var variables = new Dictionary<string, object>();

            var props = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props) {
                var attrs = prop.GetCustomAttributes(typeof(SummaryAttribute), false);
                if (attrs.Length == 0)
                    continue;

                variables.Add(prop.Name.ToLower(), Expressions.ExpressionVisitor.FixTypes(prop.GetValue(this, null)));
            }

            return variables;
        }

        protected void FireEvent() {
            Fired?.Invoke(this, new GameEventFiredEventArgs(Type, GetVariables()));
        }
    }
}
