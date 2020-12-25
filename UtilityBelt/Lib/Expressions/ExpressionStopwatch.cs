using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Expressions {
    public class ExpressionStopwatch : ExpressionObjectBase {
        private bool _isRunning = false;
        public bool IsRunning {
            get => _isRunning;
            set {
                if (value != Watch.IsRunning) {
                    if (value)
                        Watch.Start();
                    else
                        Watch.Stop();
                }
                _isRunning = value;
            }
        }
        public System.Diagnostics.Stopwatch Watch { get; }

        public ExpressionStopwatch() {
            Watch = new System.Diagnostics.Stopwatch();
        }

        public void Start() {
            IsRunning = true;
            Watch.Start();
        }

        public void Stop() {
            IsRunning = false;
            Watch.Stop();
        }

        public double Elapsed() {
            return (double)(Watch.ElapsedMilliseconds / 1000.0);
        }

        public override string ToString() {
            return $"Running:{Watch.IsRunning} Elapsed:({Elapsed()})";
        }
    }
}
