﻿using Framework.Serialization;
using Framework.Song.Tracks.Notes.Drums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument.DrumTrack
{
    public class Midi_Drum5_Loader : Midi_Drum_Loader_Base<Drum_5>
    {
        public Midi_Drum5_Loader(byte multiplierNote) : base(multiplierNote) { }

        protected override bool IsNote() { return 60 <= note.value && note.value <= 101; }

        protected override void ParseLaneColor(ref InstrumentTrack<Drum_5> track)
        {
            uint noteValue = note.value - 60;
            uint lane = LANEVALUES[noteValue];
            int diffIndex = DIFFVALUES[noteValue];
            if (lane < 7)
            {
                difficulties[diffIndex].notes[lane] = currEvent.position;
                ref Drum_5 drum = ref track[diffIndex].notes.Get_Or_Add_Back(currEvent.position);
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
                }
            }
        }

        protected override void ParseLaneColor_Off(ref InstrumentTrack<Drum_5> track)
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

        protected override void ToggleExtraValues(ref InstrumentTrack<Drum_5> track)
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
        }

        protected override void ToggleExtraValues_Off(ref InstrumentTrack<Drum_5> track)
        {
            if (note.value == 109)
                for (uint i = 0; i < 4; ++i)
                    difficulties[i].Flam = false;
        }
    }
}
