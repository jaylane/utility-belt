using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace UBService.Lib {
    /// <summary>
    /// A timer class that fires OnTick event every TickInterval
    /// </summary>
    public class Timer : IDisposable {
        internal static List<Timer> RunningTimers = new List<Timer>();
        private bool _paused = false;
        private TimeSpan _timeRemainingWhenPaused = TimeSpan.Zero;
        private DateTime _internalLastTick = DateTime.UtcNow;

        /// <summary>
        /// Fired every TickInterval
        /// </summary>
        public event EventHandler<EventArgs> OnTick;

        /// <summary>
        /// TimeSpan between OnTick event is fired
        /// </summary>
        public TimeSpan TickInterval { get; set; }

        /// <summary>
        /// The last time OnTick was fired (utc)
        /// </summary>
        public DateTime LastTick { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// Wether this timer is currently running, or is paused
        /// </summary>
        public bool IsRunning {
            get => !_paused;
            set {
                if (value == _paused)
                    return;

                if (_paused) {
                    _timeRemainingWhenPaused = TickInterval - (DateTime.UtcNow - LastTick);
                }
                else {
                    _internalLastTick = DateTime.UtcNow - _timeRemainingWhenPaused;
                }
            }
        }

        /// <summary>
        /// Create a new timer that fires every interval
        /// </summary>
        /// <param name="interval">The interval to fire OnTick event at</param>
        /// <param name="startPaused">Set to true to start the timer in a paused state</param>
        public Timer(TimeSpan interval, bool startPaused = false) {
            TickInterval = interval;
            RunningTimers.Add(this);
            _paused = startPaused;
            _timeRemainingWhenPaused = interval;
        }

        /// <summary>
        /// Resets the timer by setting its LastTick to the current time.
        /// </summary>
        public void Reset() {
            LastTick = DateTime.UtcNow;
            _internalLastTick = LastTick;
        }

        internal void TryTick() {
            if (DateTime.UtcNow - _internalLastTick >= TickInterval) {
                try {
                    OnTick?.Invoke(this, EventArgs.Empty);
                }
                catch { }
                LastTick = DateTime.UtcNow;
                _internalLastTick = LastTick;
            }
        }

        /// <summary>
        /// Disposes the timer
        /// </summary>
        public void Dispose() {
            RunningTimers.Remove(this);
        }
    }
}
