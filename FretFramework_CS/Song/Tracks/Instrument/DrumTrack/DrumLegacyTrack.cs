using Framework.Serialization;
using Framework.Song.Tracks.Notes.Drums;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument.DrumTrack
{
    public class LegacyDrumTrack : InstrumentTrack<Drum_Legacy>
    {
        private DrumType _type;
        public DrumType Type { get { return _type; } }

        public LegacyDrumTrack(DrumType type = DrumType.UNKNOWN) { _type = type; }

        public DrumType LoadMidi(MidiFileReader reader)
        {
            Midi_DrumsLegacy_Loader loader = new(reader.GetMultiplierNote());
            loader.Load(this, reader);
            for (int i = 0; i < 4; ++i)
            {
                ParseDrumType(ref difficulties[i]);
                if (_type != DrumType.UNKNOWN)
                    break;
            }
            return _type;
        }

        public bool LoadDotChart(ChartFileReader reader)
        {
            ref DifficultyTrack<Drum_Legacy> diff = ref difficulties[reader.Difficulty];
            if (!DotChart_Loader.Load(ref diff, reader))
                return false;

            ParseDrumType(ref diff);
            return true;
        }

        private void ParseDrumType(ref DifficultyTrack<Drum_Legacy> diff)
        {
            for (int i = 0; i < diff.notes.Count; ++i)
            {
                _type = diff.notes.At_index(i).obj.ParseDrumType();
                if (_type != DrumType.UNKNOWN)
                    break;
            }
        }

        public void Transfer(InstrumentTrack<Drum_4Pro> to)
        {
            to.specialPhrases = specialPhrases;
            to.events = events;
            for (int i = 0; i < 4; ++i)
            {
                ref var diff_leg = ref difficulties[i];
                ref var diff_4pro = ref to[i];
                if (!diff_4pro.IsOccupied() && diff_leg.IsOccupied())
                {
                    diff_4pro.specialPhrases = diff_leg.specialPhrases;
                    diff_4pro.events = diff_leg.events;
                    diff_4pro.notes.Capacity = diff_leg.notes.Capacity;
                    for (int n = 0; n < diff_leg.notes.Count; ++n)
                    {
                        ref var note = ref diff_leg.notes.At_index(n);
                        diff_4pro.notes.Add_Back(note.key, new(note.obj));
                    }
                }
            }
        }

        public void Transfer(InstrumentTrack<Drum_5> to)
        {
            to.specialPhrases = specialPhrases;
            to.events = events;
            for (int i = 0; i < 4; ++i)
            {
                ref var diff_leg = ref difficulties[i];
                ref var diff_5 = ref to[i];
                if (!diff_5.IsOccupied() && diff_leg.IsOccupied())
                {
                    diff_5.specialPhrases = diff_leg.specialPhrases;
                    diff_5.events = diff_leg.events;
                    diff_5.notes.Capacity = diff_leg.notes.Capacity;
                    for (int n = 0; n < diff_leg.notes.Count; ++n)
                    {
                        ref var note = ref diff_leg.notes.At_index(n);
                        diff_5.notes.Add_Back(note.key, new(note.obj));
                    }
                }
            }
        }
    }

    public class Midi_DrumsLegacy_Loader : Midi_Drum_Loader_Base<Drum_Legacy>
    {
        public Midi_DrumsLegacy_Loader(byte multiplierNote) : base(multiplierNote) { }

        protected override bool IsNote() { return 60 <= note.value && note.value <= 101; }

        protected override void ParseLaneColor(ref InstrumentTrack<Drum_Legacy> track)
        {
            uint noteValue = note.value - 60;
            uint lane = LANEVALUES[noteValue];
            int diffIndex = DIFFVALUES[noteValue];
            if (lane < 7)
            {
                difficulties[diffIndex].notes[lane] = currEvent.position;
                ref Drum_Legacy drum = ref track[diffIndex].notes.Get_Or_Add_Back(currEvent.position);
                if (difficulties[diffIndex].Flam)
                    drum.IsFlammed = true;

                if (lane >= 2)
                {
                    ref var pad = ref drum[lane - 2];
                    if (!enableDynamics || note.velocity == 100)
                        pad.Dynamics = DrumDynamics.None;
                    else if (note.velocity > 100)
                        pad.Dynamics = DrumDynamics.Accent;
                    else if (note.velocity < 100)
                        pad.Dynamics = DrumDynamics.Ghost;

                    if (3 <= lane && lane <= 5)
                        pad.IsCymbal = !toms[lane - 3];
                }
            }
        }

        protected override void ParseLaneColor_Off(ref InstrumentTrack<Drum_Legacy> track)
        {
            uint noteValue = note.value - 60;
            uint lane = LANEVALUES[noteValue];
            int diffIndex = DIFFVALUES[noteValue];

            if (lane < 7)
            {
                ulong colorPosition = difficulties[diffIndex].notes[lane];
                if (colorPosition != ulong.MaxValue)
                {
                    track[diffIndex].notes.Traverse_Backwards_Until(colorPosition).Set(lane, currEvent.position - colorPosition);
                    difficulties[diffIndex].notes[lane] = ulong.MaxValue;
                }
            }
        }

        protected override void ToggleExtraValues(ref InstrumentTrack<Drum_Legacy> track)
        {
            if (note.value == 109)
            {
                for (int i = 0; i < 4; ++i)
                {
                    difficulties[i].Flam = true;
                    if (track[i].notes.ValidateLastKey(currEvent.position))
                        track[i].notes.Last().IsFlammed = true;
                }
            }
            else if (110 <= note.value && note.value <= 112)
                toms[note.value - 110] = true;
        }

        protected override void ToggleExtraValues_Off(ref InstrumentTrack<Drum_Legacy> track)
        {
            if (note.value == 109)
            {
                for (uint i = 0; i < 4; ++i)
                    difficulties[i].Flam = false;
            }
            else if (110 <= note.value && note.value <= 112)
                toms[note.value - 110] = false;
        }
    }
}
