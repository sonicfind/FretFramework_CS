using Framework.Song.Tracks.Notes.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes.Drums
{
    public unsafe struct Drum_5 : IDrumNote
    {
        private DrumPad snare;
        private DrumPad yellow;
        private DrumPad blue;
        private DrumPad orange;
        private DrumPad green;
        private TruncatableSustain _bass;
        private TruncatableSustain _doubleBass;
        public TruncatableSustain Bass
        {
            get { return _bass; }
            set
            {
                _bass = value;
                if (_bass.IsActive())
                    _doubleBass.Disable();
            }
        }
        public TruncatableSustain DoubleBass
        {
            get { return _doubleBass; }
            set
            {
                _doubleBass = value;
                if (_doubleBass.IsActive())
                    _bass.Disable();
            }
        }
        public bool IsFlammed { get; set; }

        public ref DrumPad this[uint lane]
        {
            get
            {
                fixed (DrumPad* pads = &snare)
                    return ref pads[lane];
            }
        }

        public bool HasActiveNotes()
        {
            fixed (DrumPad* pads = &snare)
                for(uint i = 0; i < 5; ++i)
                    if (pads[i].IsActive())
                        return true;
            return _bass.IsActive() || _doubleBass.IsActive();
        }

        public bool Set(uint lane, ulong length)
        {
            if (lane == 0)
            {
                _bass.Duration = length;
                _doubleBass.Disable();
            }
            else if (lane == 1)
            {
                _doubleBass.Duration = length;
                _bass.Disable();
            }
            else if (lane <= 6)
            {
                fixed (DrumPad* pads = &snare)
                    pads[lane - 2].duration = length;
            }
            else
                return false;
            return true;
        }

        public Drum_5() { }
        public Drum_5(Drum_Legacy drum)
        {
            _bass = drum.Bass;
            _doubleBass = drum.DoubleBass;
            IsFlammed = drum.IsFlammed;
            fixed (DrumPad* pads = &snare)
            {
                for (uint i = 0; i < 5; i++)
                {
                    var pad = drum[i];
                    if (pad.IsActive())
                    {
                        pads[i].duration = pad.duration;
                        pads[i].Dynamics = pad.Dynamics;
                    }
                }
            }
        }

        public bool Set_From_Chart(nuint lane, ulong length)
        {
            fixed (DrumPad* pads = &snare)
            {
                if (lane == 0) _bass.Duration = length;
                else if (lane <= 5) pads[lane - 1].duration = length;
                else if (lane == 32)
                {
                    _doubleBass = _bass;
                    _bass.Disable();
                }
                else if (34 <= lane && lane <= 38) pads[lane - 34].Dynamics = DrumDynamics.Accent;
                else if (40 <= lane && lane <= 44) pads[lane - 40].Dynamics = DrumDynamics.Ghost;
                else
                    return false;
                return true;
            }
        }
    }
}
