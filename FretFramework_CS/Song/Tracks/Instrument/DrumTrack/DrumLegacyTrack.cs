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
    public class LegacyDrumTrackLoader
    {
        private readonly InstrumentTrack<Drum_Legacy> legacy = new();
        public DrumType Type { get; set; }
        public LegacyDrumTrackLoader(DrumType type = DrumType.UNKNOWN) { Type = type; }
        public LegacyDrumTrackLoader(ref MidiFileReader reader)
        {
            Midi_Instrument_DrumLegacy loader = new(reader.GetMultiplierNote());
            Midi_Loader.Load(loader, legacy, ref reader);
            Type = loader.type;
        }

        public bool WasLoaded()
        {
            return legacy.IsOccupied();
        }

        public bool LoadDotChart(ref ChartFileReader reader)
        {
            ref DifficultyTrack<Drum_Legacy> diff = ref legacy[reader.Difficulty];
            if (!DotChart_Loader.Load(ref diff, ref reader))
                return false;

            foreach (var note in diff.notes)
            {
                Type = note.obj.ParseDrumType();
                if (Type != DrumType.UNKNOWN)
                    break;
            }
            return true;
        }

        public void Transfer(InstrumentTrack<Drum_4Pro> to)
        {
            to.specialPhrases = legacy.specialPhrases;
            to.events = legacy.events;
            for (uint i = 0; i < 4; ++i)
            {
                if (!to[i].IsOccupied() && legacy[i].IsOccupied())
                {
                    to[i].specialPhrases = legacy[i].specialPhrases;
                    to[i].events = legacy[i].events;
                    to[i].notes.Capacity = legacy[i].notes.Capacity;
                    foreach (var note in legacy[i].notes)
                        to[i].notes.Add_Back(note.key, new(note.obj));
                }
            }
        }

        public void Transfer(InstrumentTrack<Drum_5> to)
        {
            to.specialPhrases = legacy.specialPhrases;
            to.events = legacy.events;
            for (uint i = 0; i < 4; ++i)
            {
                if (!to[i].IsOccupied() && legacy[i].IsOccupied())
                {
                    to[i].specialPhrases = legacy[i].specialPhrases;
                    to[i].events = legacy[i].events;
                    to[i].notes.Capacity = legacy[i].notes.Capacity;
                    foreach (var note in legacy[i].notes)
                        to[i].notes.Add_Back(note.key, new(note.obj));
                }
            }
        }
    }
    public class Midi_Instrument_DrumLegacy : Midi_Drum_Loader_Base<Drum_Legacy>
    {
        public DrumType type = DrumType.UNKNOWN;
        public Midi_Instrument_DrumLegacy(byte multiplierNote) : base(multiplierNote) { }

        public override bool IsNote(uint value) { return 60 <= value && value <= 101; }

        public override void ParseLaneColor(MidiNote note, ref InstrumentTrack<Drum_Legacy> track)
        {
            uint noteValue = note.value - 60;
            uint lane = LANEVALUES[noteValue];
            uint diffIndex = DIFFVALUES[noteValue];
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
                    else if (lane == 6)
                        type = DrumType.FIVE_LANE;
                }
            }
        }

        public override void ParseLaneColor_Off(MidiNote note, ref InstrumentTrack<Drum_Legacy> track)
        {
            uint noteValue = note.value - 60;
            uint lane = LANEVALUES[noteValue];
            uint diffIndex = DIFFVALUES[noteValue];

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

        public override void ToggleExtraValues(MidiNote note, ref InstrumentTrack<Drum_Legacy> track)
        {
            if (note.value == 109)
            {
                for (uint i = 0; i < 4; ++i)
                {
                    difficulties[i].Flam = true;
                    if (track[i].notes.ValidateLastKey(currEvent.position))
                        track[i].notes.Last().IsFlammed = true;
                }
            }
            else if (110 <= note.value && note.value <= 112)
            {
                toms[note.value - 110] = true;
                type = DrumType.FOUR_PRO;
            }
        }

        public override void ToggleExtraValues_Off(MidiNote note, ref InstrumentTrack<Drum_Legacy> track)
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
