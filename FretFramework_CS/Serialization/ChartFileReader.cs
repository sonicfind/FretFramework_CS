using Framework.Song.Tracks;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Serialization
{
    public enum ChartEvent
    {
	    BPM,
	    TIME_SIG,
	    ANCHOR,
	    EVENT,
	    SECTION,
	    NOTE,
	    MULTI_NOTE,
	    MODIFIER,
	    SPECIAL,
	    LYRIC,
	    VOCAL,
	    VOCAL_PERCUSSION,
	    NOTE_PRO,
	    MUTLI_NOTE_PRO,
	    ROOT,
	    LEFT_HAND,
	    PITCH,
	    RANGE_SHIFT,
	    UNKNOWN = 255,
    }

    public enum NoteTracks_Chart
    {
        Single,
        DoubleGuitar,
        DoubleBass,
        DoubleRhythm,
        Drums,
        Keys,
        GHLGuitar,
        GHLBass,
        Invalid,
    };

    public unsafe class ChartFileReader
    {
        internal struct EventCombo
        {
            public byte[] descriptor;
            public ChartEvent eventType;
            public EventCombo(byte[] bytes, ChartEvent chartEvent)
            {
                descriptor = bytes;
                eventType = chartEvent;
            }
        }

        internal static readonly byte[] HEADERTRACK = Encoding.ASCII.GetBytes("[Song]");
        internal static readonly byte[] SYNCTRACK =   Encoding.ASCII.GetBytes("[SyncTrack]");
        internal static readonly byte[] EVENTTRACK =  Encoding.ASCII.GetBytes("[Events]");
        internal static readonly EventCombo TEMPO =       new(Encoding.ASCII.GetBytes("B"),  ChartEvent.BPM );
        internal static readonly EventCombo TIMESIG =     new(Encoding.ASCII.GetBytes("TS"), ChartEvent.TIME_SIG );
        internal static readonly EventCombo ANCHOR =      new(Encoding.ASCII.GetBytes("A"),  ChartEvent.ANCHOR );
        internal static readonly EventCombo TEXT =        new(Encoding.ASCII.GetBytes("E"),  ChartEvent.EVENT );
        internal static readonly EventCombo SECTION =     new(Encoding.ASCII.GetBytes("SE"), ChartEvent.SECTION );
        internal static readonly EventCombo NOTE =        new(Encoding.ASCII.GetBytes("N"),  ChartEvent.NOTE );
        internal static readonly EventCombo SPECIAL =     new(Encoding.ASCII.GetBytes("S"),  ChartEvent.SPECIAL );
        internal static readonly EventCombo LYRIC =       new(Encoding.ASCII.GetBytes("L"),  ChartEvent.LYRIC );
        internal static readonly EventCombo VOCAL =       new(Encoding.ASCII.GetBytes("V"),  ChartEvent.VOCAL );
        internal static readonly EventCombo PERC =        new(Encoding.ASCII.GetBytes("VP"), ChartEvent.VOCAL_PERCUSSION );
        internal const double TEMPO_FACTOR = 60000000000;

        internal EventCombo[] EVENTS_SYNC   = { TEMPO, TIMESIG, ANCHOR };
        internal EventCombo[] EVENTS_EVENTS = { TEXT, SECTION, };
        internal EventCombo[] EVENTS_DIFF   = { NOTE, SPECIAL, TEXT, };

        private readonly TxtFileReader reader;
        private EventCombo[] eventSet = Array.Empty<EventCombo>();
        private ulong tickPosition = 0;
        public NoteTracks_Chart Instrument { get; private set; }
        public uint Difficulty { get; private set; }

        public ChartFileReader(FrameworkFile file) { reader = new TxtFileReader(file); }
        public ChartFileReader(byte[] data) : this(new FrameworkFile(data)) { }
        public ChartFileReader(string path) : this(File.ReadAllBytes(path)) { }

        public bool IsStartOfTrack()
        {
	        return reader.PeekByte() == '[';
        }

        public bool ValidateHeaderTrack()
        {
            return ValidateTrack(HEADERTRACK);
        }

        public bool ValidateSyncTrack()
        {
            if (!ValidateTrack(SYNCTRACK))
                return false;

            eventSet = EVENTS_SYNC;
            return true;
        }

        public bool ValidateEventsTrack()
        {
            if (!ValidateTrack(EVENTTRACK))
                return false;

            eventSet = EVENTS_EVENTS;
            return true;
        }

        internal static readonly byte[][] DIFFICULTIES =
        {
            Encoding.ASCII.GetBytes("[Easy"),
            Encoding.ASCII.GetBytes("[Medium"),
            Encoding.ASCII.GetBytes("[Hard"),
            Encoding.ASCII.GetBytes("[Expert")
        };

        public bool ValidateDifficulty()
        {
            for (uint diff = 4; diff > 0;)
                if (DoesStringMatch(DIFFICULTIES[--diff]))
                {
                    Difficulty = diff;
                    eventSet = EVENTS_DIFF;
                    reader.Position += DIFFICULTIES[diff].Length;
                    return true;
                }
            return false;
        }

        internal static readonly (byte[], NoteTracks_Chart)[] NOTETRACKS =
        {
            new(Encoding.ASCII.GetBytes("Single]"),       NoteTracks_Chart.Single ),
		    new(Encoding.ASCII.GetBytes("DoubleGuitar]"), NoteTracks_Chart.DoubleGuitar ),
		    new(Encoding.ASCII.GetBytes("DoubleBass]"),   NoteTracks_Chart.DoubleBass ),
		    new(Encoding.ASCII.GetBytes("DoubleRhythm]"), NoteTracks_Chart.DoubleRhythm ),
		    new(Encoding.ASCII.GetBytes("Drums]"),        NoteTracks_Chart.Drums ),
		    new(Encoding.ASCII.GetBytes("Keys]"),         NoteTracks_Chart.Keys ),
		    new(Encoding.ASCII.GetBytes("GHLGuitar]"),    NoteTracks_Chart.GHLGuitar ),
            new(Encoding.ASCII.GetBytes("GHLBass]"),      NoteTracks_Chart.GHLBass ),
	    };

        public bool ValidateInstrument()
        {
            foreach (var track in NOTETRACKS)
            {
                if (ValidateTrack(track.Item1))
                {
                    Instrument = track.Item2;
                    return true;
                }
            }
            return false;
        }

        private bool ValidateTrack(ReadOnlySpan<byte> track)
        {
            if (!DoesStringMatch(track))
                return false;

            reader.GotoNextLine();
            tickPosition = 0;
            return true;
        }

        private bool DoesStringMatch(ReadOnlySpan<byte> str)
        {
            if (reader.Next - reader.Position < str.Length)
                return false;
	        return new ReadOnlySpan<byte>(reader.Ptr + reader.Position, str.Length).SequenceEqual(str);
        }

        public bool IsStillCurrentTrack()
        {
            int position = reader.Position;
            if (position == reader.Length)
                return false;

            if (reader.PeekByte() == '}')
            {
                reader.GotoNextLine();
                return false;
            }

            return true;
        }

        public (ulong, ChartEvent) ParseEvent()
        {
            ulong position = reader.ReadUInt64();
            if (position < tickPosition)
                throw new Exception($".Cht/.Chart position out of order (previous: {tickPosition})");

            tickPosition = position;

            byte* ptr = reader.Ptr;
            int curr = reader.Position;
            int start = curr;
            while (('A' <= ptr[curr] && ptr[curr] <= 'Z') || ('a' <= ptr[curr] && ptr[curr] <= 'z'))
                ++curr;

            reader.Position = curr;
            ReadOnlySpan<byte> type = new(ptr + start, curr - start);
            foreach (EventCombo combo in eventSet)
		        if (type.SequenceEqual(combo.descriptor))
                {
                    reader.SkipWhiteSpace();
                    return new(position, combo.eventType);
                }
            return new(position, ChartEvent.UNKNOWN);
        }

        public void SkipEvent()
        {
            reader.GotoNextLine();
        }

        public void NextEvent()
        {
            reader.GotoNextLine();
        }

        public ReadOnlySpan<byte> ExtractText()
        {
            return reader.ExtractTextSpan();
        }

        public (nuint, ulong) ExtractLaneAndSustain()
        {
            nuint lane = reader.ReadNUint();
            ulong sustain = reader.ReadUInt64();
            return new(lane, sustain);
        }

        public SpecialPhrase ExtractSpecialPhrase()
        {
            nuint type = reader.ReadNUint();
            ulong duration = reader.ReadUInt64();
            return new((SpecialPhraseType)type, duration);
        }

        public uint ExtractMicrosPerQuarter()
        {
            return (uint)Math.Round(TEMPO_FACTOR / reader.ReadUInt32());
        }

        public ulong ExtractAnchor()
        {
            return reader.ReadUInt64();
        }

        public TimeSig ExtractTimeSig()
        {
            ulong numerator = reader.ReadUInt64();
            ulong denom = 255, metro = 0, n32nds = 0;
            if (reader.ReadUInt64(ref denom))
                if (reader.ReadUInt64(ref metro))
                    reader.ReadUInt64(ref n32nds);

            return new TimeSig((byte)numerator, (byte)denom, (byte)metro, (byte)n32nds);
        }

        public void SkipTrack()
        {
            reader.GotoNextLine();
            int scopeLevel = 1;
            int length = GetDistanceToTrackCharacter();
            while (reader.Position + length != reader.Length)
            {
                int index = length - 1;
                byte* ptr = reader.Ptr + reader.Position;
                byte point = ptr[index];
                while (index > 0 && point <= 32 && point != '\n')
                    point = ptr[--index];

                reader.Position += length;
                if (point == '\n')
                {
                    if (reader.PeekByte() == '}')
                    {
                        if (scopeLevel == 1)
                        {
                            reader.SetNextPointer();
                            reader.GotoNextLine();
                            return;
                        }
                        else
                            --scopeLevel;
                    }
                    else
                        ++scopeLevel;
                }

                ++reader.Position;
                length = GetDistanceToTrackCharacter();
            }

            reader.Position = reader.Length;
            reader.SetNextPointer();
        }

        private int GetDistanceToTrackCharacter()
        {
            int position = reader.Position;
            int distanceToEnd = reader.Length - position;
            byte* ptr = reader.Ptr + position;
            int i = 0;
            while (i < distanceToEnd)
            {
                byte b = ptr[i];
                if (b == '[' || b == '}')
                    break;
                ++i;
            }
            return i;
        }
    }
}
