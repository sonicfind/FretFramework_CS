using Framework.Serialization;
using Framework.Song.Tracks.Instrument.DrumTrack;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan.Instrument.Drums
{
    public struct LegacyDrumScan
    {
        private ScanValues _values;
        private DrumType _type;

        public ScanValues Values { get { return _values; } }
        public DrumType Type { get { return _type; } }

        public LegacyDrumScan(DrumType type = DrumType.UNKNOWN) { _type = type; }

        public DrumType ScanMidi(ref MidiFileReader reader)
        {
            Midi_DrumLegacy_Scanner scanner = new();
            _values = scanner.Scan(ref reader);
            return scanner.Type;
        }

        public bool ScanDotChart(ref ChartFileReader reader)
        {
            int index = reader.Difficulty;
            if (_values[index])
                return false;

            bool found = false;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    nuint lane = reader.ExtractLaneAndSustain().Item1;
                    if (lane <= 5)
                    {
                        if (!found)
                        {
                            _values.Set(index);
                            found = true;
                        }

                        if (lane == 5)
                            _type = DrumType.FIVE_LANE;
                    }
                    else if (66 <= lane && lane <= 68)
                        _type = DrumType.FOUR_PRO;

                    if (found && Type != DrumType.UNKNOWN)
                        return false;
                }
                reader.NextEvent();
            }
            return true;
        }
    }

    public class Midi_DrumLegacy_Scanner : Midi_Drum_Scanner_Base
    {
        private DrumType _type;
        public DrumType Type { get { return _type; } }

        public override bool IsFullyScanned() { return _type != DrumType.UNKNOWN && value.subTracks == 31; }
        public override bool IsNote() { return 60 <= note.value && note.value <= 101; }

        public override bool ParseLaneColor()
        {
            uint noteValue = note.value - 60;
            uint lane = LANEVALUES[noteValue];
            if (lane < 7)
            {
                notes[DIFFVALUES[noteValue], lane] = true;
                if (lane == 6)
                {
                    _type = DrumType.FIVE_LANE;
                    return IsFullyScanned();
                }
            }
            return false;
        }

        public override bool ParseLaneColor_Off()
        {
            uint noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 7)
                {
                    value.Set(diffIndex);
                    difficulties[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }

        public override bool ToggleExtraValues()
        {
            if (110 <= note.value && note.value <= 112)
            {
                _type = DrumType.FOUR_PRO;
                return IsFullyScanned();
            }
            return false;
        }
    }
}
