using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace UtilityBelt.Views.Inspector {

    internal class DynamicEventArgs : EventArgs {
        public List<object> EventParameters { get; }

        public object Arg1 { get; set; }
        public object Arg2 { get; set; }
        public object Arg3 { get; set; }
        public object Arg4 { get; set; }
        public object Arg5 { get; set; }
        public object Arg6 { get; set; }
        public object Arg7 { get; set; }
        public object Arg8 { get; set; }
        public object Arg9 { get; set; }

        public DynamicEventArgs(List<object> eventParameters) {
            EventParameters = eventParameters;
        }
    }

    internal class DynamicEventHandler {
        public Delegate Delegate;
        public EventInfo EventInfo;

        public event EventHandler<DynamicEventArgs> OnEvent;

        /// <summary>
        /// Number of times this event delegate has been called
        /// </summary>
        public uint CalledCount = 0;

        public DynamicEventHandler() {
        }

        internal void HandleEvent(params object[] args) {
            CalledCount++;
            var dEvent = new DynamicEventArgs(args.ToList());
            for (var i = 0; i < args.Length; i++) {
                var prop = dEvent.GetType().GetProperty($"Arg{i}", BindingFlags.Instance | BindingFlags.Public);
                if (prop != null) {
                    prop.SetValue(dEvent, args[i], null);
                }
            }
            OnEvent?.Invoke(this, dEvent);
        }

        public void HandleHelper() {
            HandleEvent();
        }
    }

    internal class DynamicEventHandler<T> : DynamicEventHandler {
        public DynamicEventHandler() : base() { }

        public void HandleHelper(object sender, T args) {
            HandleEvent(sender, args);
        }
    }

    internal class DynamicEventHandler<T1, T2> : DynamicEventHandler {
        public DynamicEventHandler() : base() { }

        public void HandleHelper(T1 a, T2 b) {
            HandleEvent(a, b);
        }
    }

    internal class DynamicEventHandler<T1, T2, T3> : DynamicEventHandler {
        public DynamicEventHandler() : base() { }

        public void HandleHelper(T1 a, T2 b, T3 c) {
            HandleEvent(a, b, c);
        }
    }

    internal class DynamicEventHandler<T1, T2, T3, T4> : DynamicEventHandler {
        public DynamicEventHandler() : base() { }

        public void HandleHelper(T1 a, T2 b, T3 c, T4 d) {
            HandleEvent(a, b, c, d);
        }
    }

    internal class DynamicEventHandler<T1, T2, T3, T4, T5> : DynamicEventHandler {
        public DynamicEventHandler() : base() { }

        public void HandleHelper(T1 a, T2 b, T3 c, T4 d, T5 e) {
            HandleEvent(a, b, c, d, e);
        }
    }

    internal class DynamicEventHandler<T1, T2, T3, T4, T5, T6> : DynamicEventHandler {
        public DynamicEventHandler() : base() { }

        public void HandleHelper(T1 a, T2 b, T3 c, T4 d, T5 e, T6 f) {
            HandleEvent(a, b, c, d, e, f);
        }
    }

    internal class DynamicEventHandler<T1, T2, T3, T4, T5, T6, T7> : DynamicEventHandler {
        public DynamicEventHandler() : base() { }

        public void HandleHelper(T1 a, T2 b, T3 c, T4 d, T5 e, T6 f, T7 g) {
            HandleEvent(a, b, c, d, e, f, g);
        }
    }

    internal class DynamicEventHandler<T1, T2, T3, T4, T5, T6, T7, T8> : DynamicEventHandler {
        public DynamicEventHandler() : base() { }

        public void HandleHelper(T1 a, T2 b, T3 c, T4 d, T5 e, T6 f, T7 g, T8 h) {
            HandleEvent(a, b, c, d, e, f, g, h);
        }
    }

    internal class DynamicEventHandler<T1, T2, T3, T4, T5, T6, T7, T8, T9> : DynamicEventHandler {
        public DynamicEventHandler() : base() { }

        public void HandleHelper(T1 a, T2 b, T3 c, T4 d, T5 e, T6 f, T7 g, T8 h, T9 i) {
            HandleEvent(a, b, c, d, e, f, g, h, i);
        }
    }
}
