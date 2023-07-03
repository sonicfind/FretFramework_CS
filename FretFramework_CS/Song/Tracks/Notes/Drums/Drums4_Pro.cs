using Framework.Song.Tracks.Notes.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes.Drums
{
    public unsafe struct Drum_4Pro : IDrumNote
    {
        private DrumPad_Pro snare;
        private DrumPad_Pro yellow;
        private DrumPad_Pro blue;
        private DrumPad_Pro green;
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

        public ref DrumPad_Pro this[uint lane]
        {
            get
            {
                fixed (DrumPad_Pro* pads = &snare)
                    return ref pads[lane];
            }
        }

        public bool HasActiveNotes()
        {
            fixed (DrumPad_Pro* pads = &snare)
                for (uint i = 0; i < 4; ++i)
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
            else if (lane <= 5)
            {
                fixed (DrumPad_Pro* pads = &snare)
                    pads[lane - 2].duration = length;
            }
            else
                return false;
            return true;
        }

        public Drum_4Pro() { }
        public Drum_4Pro(Drum_Legacy drum)
        {
            _bass = drum.Bass;
            _doubleBass = drum.DoubleBass;
            IsFlammed = drum.IsFlammed;
            fixed (DrumPad_Pro* pads = &snare)
                for (uint i = 0; i < 4; i++)
                    pads[i] = drum[i];
        }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            fixed (DrumPad_Pro* pads = &snare)
            {
                if (lane == 0) _bass.Duration = length;
                else if (lane <= 4) pads[lane - 1].duration = length;
                else if (lane == 32)
                {
                    _doubleBass = _bass;
                    _bass.Disable();
                }
                else if (66 <= lane && lane <= 68) pads[lane - 65].IsCymbal = true;
                else if (34 <= lane && lane <= 37) pads[lane - 34].Dynamics = DrumDynamics.Accent;
                else if (40 <= lane && lane <= 43) pads[lane - 40].Dynamics = DrumDynamics.Ghost;
                else
                    return false;
                return true;
            }
        }
    }
}
