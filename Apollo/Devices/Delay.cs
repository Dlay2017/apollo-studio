using System.Collections.Concurrent;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Helpers;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class Delay: Device {
        Time _time;
        public Time Time {
            get => _time;
            set {
                if (_time != null) {
                    _time.FreeChanged -= FreeChanged;
                    _time.ModeChanged -= ModeChanged;
                    _time.StepChanged -= StepChanged;
                }

                _time = value;

                if (_time != null) {
                    _time.Minimum = 10;
                    _time.Maximum = 30000;

                    _time.FreeChanged += FreeChanged;
                    _time.ModeChanged += ModeChanged;
                    _time.StepChanged += StepChanged;
                }
            }
        }

        void FreeChanged(int value) {
            if (Viewer?.SpecificViewer != null) ((DelayViewer)Viewer.SpecificViewer).SetDurationValue(value);
        }

        void ModeChanged(bool value) {
            if (Viewer?.SpecificViewer != null) ((DelayViewer)Viewer.SpecificViewer).SetMode(value);
        }

        void StepChanged(Length value) {
            if (Viewer?.SpecificViewer != null) ((DelayViewer)Viewer.SpecificViewer).SetDurationStep(value);
        }

        double _gate;
        public double Gate {
            get => _gate;
            set {
                if (0.01 <= value && value <= 4) {
                    _gate = value;
                    
                    if (Viewer?.SpecificViewer != null) ((DelayViewer)Viewer.SpecificViewer).SetGate(Gate);
                }
            }
        }

        ConcurrentQueue<Signal> buffer = new ConcurrentQueue<Signal>();
        object locker = new object();
        ConcurrentHashSet<Courier> timers = new ConcurrentHashSet<Courier>();

        public override Device Clone() => new Delay(_time.Clone(), _gate) {
            Collapsed = Collapsed,
            Enabled = Enabled
        };

        public Delay(Time time = null, double gate = 1): base("delay") {
            Time = time?? new Time();
            Gate = gate;
        }

        void Tick(Courier sender) {
            if (Disposed) return;
            
            lock (locker) {
                if (buffer.TryDequeue(out Signal n))
                    InvokeExit(n);
                
                timers.Remove(sender);
            }
        }

        public override void MIDIProcess(Signal n) {
            lock (locker) {
                buffer.Enqueue(n.Clone());

                timers.Add(new Courier(_time * _gate, Tick));
            }
        }

        protected override void Stop() {
            foreach (Courier i in timers) i.Dispose();
            timers.Clear();
            
            buffer.Clear();
            locker = new object();
        }

        public override void Dispose() {
            if (Disposed) return;

            Stop();

            Time.Dispose();
            base.Dispose();
        }
        
        public class DurationUndoEntry: SimplePathUndoEntry<Delay, int> {
            protected override void Action(Delay item, int element) => item.Time.Free = element;
            
            public DurationUndoEntry(Delay delay, int u, int r)
            : base($"Delay Duration Changed to {r}ms", delay, u, r) {}
        }
        
        public class DurationModeUndoEntry: SimplePathUndoEntry<Delay, bool> {
            protected override void Action(Delay item, bool element) => item.Time.Mode = element;
            
            public DurationModeUndoEntry(Delay delay, bool u, bool r)
            : base($"Delay Duration Switched to {(r? "Steps" : "Free")}", delay, u, r) {}
        }
        
        public class DurationStepUndoEntry: SimplePathUndoEntry<Delay, int> {
            protected override void Action(Delay item, int element) => item.Time.Length.Step = element;
            
            public DurationStepUndoEntry(Delay delay, int u, int r)
            : base($"Delay Duration Changed to {Length.Steps[r]}", delay, u, r) {}
        }
        
        public class GateUndoEntry: SimplePathUndoEntry<Delay, double> {
            protected override void Action(Delay item, double element) => item.Gate = element;
            
            public GateUndoEntry(Delay delay, double u, double r)
            : base($"Delay Gate Changed to {r}%", delay, u / 100, r / 100) {}
        }
    }
}