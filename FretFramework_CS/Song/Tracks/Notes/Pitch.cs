using Framework.Song.Tracks.Notes.Interfaces;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes
{
    public struct Pitch
    {
        internal const byte OCTAVE_LENGTH = 12;
        public int OCTAVE_MIN { get; private init; }
        public int OCTAVE_MAX { get; private init; }

        private PitchName _note;
        private int _octave = -1;

        public PitchName Note
        {
            get { return _note; }
            set
            {
                if (Octave == OCTAVE_MAX && value != PitchName.C)
                    throw new Exception("Pitch out of range");
                _note = value;
            }
        }
        public int Octave
        {
            get { return _octave; }
            set
            {
                if (value < OCTAVE_MIN || OCTAVE_MAX < value || (value == OCTAVE_MAX && _note != PitchName.C))
                    throw new Exception("Octave out of range");
                _octave = value;
            }
        }

        public uint Binary
        {
            get { return (uint)(Octave + 1) * OCTAVE_LENGTH + (uint)_note; }
            set
            {
                int octave = (int)value / OCTAVE_LENGTH - 1;
                PitchName note = (PitchName)(value % OCTAVE_LENGTH);

                if (octave < OCTAVE_MIN || OCTAVE_MAX < octave || (octave == OCTAVE_MAX && note != PitchName.C))
                    throw new Exception("Binary pitch value out of range");

                _octave = octave;
                _note = note;
            }
        }

        public Pitch(int min, int max)
        {
            OCTAVE_MIN = min;
            OCTAVE_MAX = max;
        }
    }
}
