﻿using Framework.Serialization;
using Framework.Song.Tracks.Notes.Drums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument.DrumTrack
{
    public class LegacyDrumTrackHandler
    {
        private readonly InstrumentTrack<Drum_Legacy> legacy = new();
        public DrumType Type { get; set; }
        public LegacyDrumTrackHandler(DrumType type = DrumType.UNKNOWN) { Type = type; }
        public LegacyDrumTrackHandler(ref MidiFileReader reader)
        {
            Midi_Loader.Load(new Midi_Instrument_DrumLegacy(reader.GetMultiplierNote()), legacy, ref reader);
            for (uint d = 0; d < 4; ++d)
            {
                CheckLegacyType(ref legacy[d]);
                if (Type != DrumType.UNKNOWN)
                    break;
            }
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

            CheckLegacyType(ref diff);
            return true;
        }

        private void CheckLegacyType(ref DifficultyTrack<Drum_Legacy> diff)
        {
            foreach (var note in diff.notes)
            {
                Type = note.obj.ParseDrumType();
                if (Type != DrumType.UNKNOWN)
                    break;
            }
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
    public class Midi_Instrument_DrumLegacy : Midi_Instrument_Drum<Drum_Legacy>
    {
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
                toms[note.value - 110] = true;
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
