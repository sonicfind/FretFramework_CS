using Framework.Serialization;
using Framework.Song.Tracks.Notes.Drums;
using Framework.Song.Tracks.Notes.Guitar;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument.DrumTrack
{
    public class Midi_Instrument_Drum4Pro : Midi_Instrument_Drum<Drum_4Pro>
    {
        public Midi_Instrument_Drum4Pro(byte multiplierNote) : base(multiplierNote) { }

        public override bool IsNote(uint value) { return 60 <= value && value <= 100; }

        public override void ParseLaneColor(MidiNote note, ref InstrumentTrack<Drum_4Pro> track)
        {
            uint noteValue = note.value - 60;
            uint lane = LANEVALUES[noteValue];
            uint diffIndex = DIFFVALUES[noteValue];
            if (lane < 6)
            {
                difficulties[diffIndex].notes[lane] = currEvent.position;
                ref Drum_4Pro drum = ref track[diffIndex].notes.Get_Or_Add_Back(currEvent.position);
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

                    if (lane >= 3)
                        pad.IsCymbal = !toms[lane - 3];
                }
            }
        }

        public override void ParseLaneColor_Off(MidiNote note, ref InstrumentTrack<Drum_4Pro> track)
        {
            uint noteValue = note.value - 60;
            uint lane = LANEVALUES[noteValue];
            uint diffIndex = DIFFVALUES[noteValue];
            
            if (lane < 6)
            {
                ulong colorPosition = difficulties[diffIndex].notes[lane];
                if (colorPosition != ulong.MaxValue)
                {
                    track[diffIndex].notes.Traverse_Backwards_Until(colorPosition).Set(lane, currEvent.position - colorPosition);
                    difficulties[diffIndex].notes[lane] = ulong.MaxValue;
                }
            }
        }

        public override void ToggleExtraValues(MidiNote note, ref InstrumentTrack<Drum_4Pro> track)
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

        public override void ToggleExtraValues_Off(MidiNote note, ref InstrumentTrack<Drum_4Pro> track)
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
