using Framework.Song.Tracks.Notes.Guitar_Pro;
using Framework.Song.Tracks.Notes.Interfaces;
using Framework.Song.Tracks.Notes.Keys;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes.Keys_Pro
{
    public struct Pitched_Key : IEnableable
    {
        private TruncatableSustain _duration;
        public ulong Duration
        {
            get { return _duration; }
            set { _duration = value; }
        }

        public Pitch pitch = new(3, 5);

        public bool IsActive() { return Duration > 0; }
        public void Disable() { _duration = 0; }

        public Pitched_Key() { }
        public Pitched_Key(ulong duration) { Duration = duration; }
    }

    public unsafe struct Keys_Pro : INote
    {
        private Pitched_Key lane_1;
        private Pitched_Key lane_2;
        private Pitched_Key lane_3;
        private Pitched_Key lane_4;
        public Pitched_Key this[uint index]
        {
            get
            {
                if (index < NumActive)
                    fixed (Pitched_Key* lanes = &lane_1)
                        return lanes[index];
                throw new IndexOutOfRangeException();
            }
            set
            {
                if (value.pitch.OCTAVE_MIN != 3 || value.pitch.OCTAVE_MAX != 5)
                    throw new Exception("Min and max octave values invalid for this note type");

                if (index < NumActive)
                {
                    fixed (Pitched_Key* lanes = &lane_1)
                    {
                        if (!value.IsActive())
                        {
                            --NumActive;
                            for (uint i = index; i < NumActive; ++i)
                                lanes[i] = lanes[i + 1];
                        }
                        else
                            lanes[index] = value;
                    }
                }
                else if (value.IsActive())
                {
                    if (index == NumActive && NumActive < 4)
                        AddNote(value, value.pitch.Binary);
                    else
                        throw new IndexOutOfRangeException();
                }
            }
        }

        public uint NumActive { get; private set; } = 0;
        public Keys_Pro() { }

        public bool Add(uint binary, ulong length)
        {
            if (NumActive == 4)
                return false;

            Pitched_Key key = new(length);
            key.pitch.Binary = binary;
            AddNote(key, binary);
            return true;
        }

        public bool Add(PitchName note, int octave, ulong length)
        {
            if (NumActive == 4)
                return false;

            Pitched_Key key = new(length);
            key.pitch.Note = note;
            key.pitch.Octave = octave;
            AddNote(key, key.pitch.Binary);
            return true;
        }

        public void SetLength(uint index, ulong length)
        {
            if (index >= NumActive)
                throw new IndexOutOfRangeException();
            fixed (Pitched_Key* lanes = &lane_1)
                lanes[index].Duration = length;
        }

        public void SetPitch(uint index, PitchName note)
        {
            if (index >= NumActive)
                throw new IndexOutOfRangeException();
            fixed (Pitched_Key* lanes = &lane_1)
                lanes[index].pitch.Note = note;
        }

        public void SetOctave(uint index, int octave)
        {
            if (index >= NumActive)
                throw new IndexOutOfRangeException();
            fixed (Pitched_Key* lanes = &lane_1)
                lanes[index].pitch.Octave = octave;
        }
        public void SetBinary(uint index, uint binary)
        {
            if (index >= NumActive)
                throw new IndexOutOfRangeException();
            fixed (Pitched_Key* lanes = &lane_1)
                lanes[index].pitch.Binary = binary;
        }

        private void AddNote(Pitched_Key key, uint binary)
        {
            fixed (Pitched_Key* lanes = &lane_1)
            {
                uint i = 0;
                while (i < NumActive)
                {
                    uint cmp = lanes[i].pitch.Binary;
                    if (cmp == binary)
                        throw new Exception("Duplicate pitches are not allowed");

                    if (cmp > binary)
                    {
                        for (uint j = NumActive; j > i; --j)
                            lanes[j] = lanes[j - 1];
                        break;
                    }
                    ++i;
                }
                lanes[i] = key;
                NumActive++;
            }
        }

        public bool HasActiveNotes()
        {
            return NumActive > 0;
        }
    }
}
