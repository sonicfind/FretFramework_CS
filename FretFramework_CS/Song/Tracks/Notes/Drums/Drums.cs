using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Framework.Song.Tracks.Notes.Interfaces;

namespace Framework.Song.Tracks.Notes.Drums
{
    public enum DrumDynamics
    {
        None,
        Accent,
        Ghost
    }

    public interface IDrumPad : IEnableable
    {
        public DrumDynamics Dynamics { get; set; }
        public void WheelDynamics();
    }

    public struct DrumPad : IDrumPad
    {
        public TruncatableSustain duration;

        public bool IsActive() { return duration.IsActive(); }
        public void Disable() { duration.Disable(); }

        public DrumDynamics Dynamics { get; set; }
        public void WheelDynamics()
        {
            if (Dynamics == DrumDynamics.None)
                Dynamics = DrumDynamics.Accent;
            else if (Dynamics == DrumDynamics.Accent)
                Dynamics = DrumDynamics.Ghost;
            else
                Dynamics = DrumDynamics.None;
        }

        public DrumPad() { }
        public DrumPad(ulong duration)
        {
            this.duration = duration;
        }
        public DrumPad(ulong duration, DrumDynamics dynamics) : this(duration) { Dynamics = dynamics; }
        public DrumPad(DrumPad_Pro pad) : this(pad.duration, pad.Dynamics) {}
    }

    public struct DrumPad_Pro : IDrumPad
    {
        public TruncatableSustain duration;

        public bool IsActive() { return duration.IsActive(); }
        public void Disable() { duration.Disable(); }

        public DrumDynamics Dynamics { get; set; }
        public void WheelDynamics()
        {
            if (Dynamics == DrumDynamics.None)
                Dynamics = DrumDynamics.Accent;
            else if (Dynamics == DrumDynamics.Accent)
                Dynamics = DrumDynamics.Ghost;
            else
                Dynamics = DrumDynamics.None;
        }
        public bool IsCymbal { get; set; }

        public DrumPad_Pro() { }
        public DrumPad_Pro(ulong duration)
        {
            this.duration = duration;
        }
        public DrumPad_Pro(ulong duration, DrumDynamics dynamics, bool cymbal) : this(duration)
        {
            Dynamics = dynamics;
            IsCymbal = cymbal;
        }
        public void ToggleCymbal() { IsCymbal = !IsCymbal; }
    }

    public interface IDrumNote : INote, IReadableFromDotChart
    {
        public TruncatableSustain Bass
        {
            get;
            set;
        }
        public TruncatableSustain DoubleBass
        {
            get;
            set;
        }
        public bool IsFlammed { get; set; }
        public bool Set(uint lane, ulong length);
    }
}
