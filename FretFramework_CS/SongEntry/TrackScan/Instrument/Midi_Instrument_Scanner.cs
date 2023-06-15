using Framework.FlatMaps;
using Framework.Serialization;
using Framework.Song.Tracks.Instrument;
using Framework.Song.Tracks.Notes.Interfaces;
using Framework.Song.Tracks;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan.Instrument
{
    public abstract class Midi_Instrument_Scanner_Base
    {
        internal static readonly byte[] SYSEXTAG = Encoding.ASCII.GetBytes("PS");
        protected MidiEvent currEvent;
        protected ScanValues value = new();
        protected MidiNote note;

        public ScanValues Scan(MidiFileReader reader)
        {
            while (reader.TryParseEvent())
            {
                currEvent = reader.GetEvent();
                if (currEvent.type == MidiEventType.Note_On)
                {
                    reader.ExtractMidiNote(ref note);
                    if (note.velocity > 0)
                    {
                        if (ParseNote())
                            break;
                    }
                    else if (ParseNote_Off())
                        break;
                }
                else if (currEvent.type == MidiEventType.Note_Off)
                {
                    reader.ExtractMidiNote(ref note);
                    if (ParseNote_Off())
                        break;
                }
                else if (currEvent.type == MidiEventType.SysEx || currEvent.type == MidiEventType.SysEx_End)
                    ParseSysEx(reader.ExtractTextOrSysEx());
                else if (currEvent.type <= MidiEventType.Text_EnumLimit)
                    ParseText(reader.ExtractTextOrSysEx());
            }
            return value;
        }

        private bool ParseNote()
        {
            if (ProcessSpecialNote())
                return false;

            if (IsNote())
                return ParseLaneColor();
            return ToggleExtraValues();
        }

        private bool ParseNote_Off()
        {
            return ProcessSpecialNote_Off() || ParseLaneColor_Off();
        }

        public abstract bool ParseLaneColor();

        public abstract bool ParseLaneColor_Off();

        public virtual bool IsFullyScanned() { return value.subTracks == 15; }

        public virtual void ParseText(ReadOnlySpan<byte> str) {}

        public virtual bool IsNote() { return 60 <= note.value && note.value <= 100; }

        public virtual bool ProcessSpecialNote() { return false; }

        public virtual bool ProcessSpecialNote_Off() { return false; }

        public virtual bool ToggleExtraValues() { return false; }

        public virtual void ParseSysEx(ReadOnlySpan<byte> str) { }
    }

    public abstract class Midi_Instrument_Scanner : Midi_Instrument_Scanner_Base
    {
        internal static readonly int[] DIFFVALUES = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
        };
        protected bool[] difficulties = new bool[4];
    }
}
