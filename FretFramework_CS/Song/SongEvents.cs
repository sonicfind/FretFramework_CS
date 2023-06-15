using Framework.FlatMaps;
using Framework.Serialization;
using Framework.Song.Tracks;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song
{
    public struct SongSection
    {
        public string Name { get; set; } = string.Empty;
        public static implicit operator string(SongSection section) => section.Name;
        public static implicit operator SongSection(string str) => new(str);

        public SongSection() {}
        public SongSection(string name) { Name = name; }
    }

    public readonly struct SongEvents
    {
        public readonly TimedFlatMap<SongSection> sections = new();
        public readonly TimedFlatMap<List<byte[]>> globals = new();
        public SongEvents() {}

        internal static byte[][] PREFIXES = { Encoding.ASCII.GetBytes("[section "), Encoding.ASCII.GetBytes("[prc_") };

        public bool AddFromMidi(MidiFileReader reader, Encoding encoding)
        {
            if (!globals.IsEmpty() || !sections.IsEmpty())
                return false;

            while (reader.TryParseEvent())
            {
                MidiEvent midiEvent = reader.GetEvent();
                if (midiEvent.type <= MidiEventType.Text_EnumLimit)
                {
                    ReadOnlySpan<byte> bytes = reader.ExtractTextOrSysEx();
                    if (bytes.StartsWith(PREFIXES[0]))
                        sections.Get_Or_Add_Back(midiEvent.position) = encoding.GetString(bytes[PREFIXES[0].Length..(bytes.Length - 1)]);
                    else if (bytes.StartsWith(PREFIXES[1]))
                        sections.Get_Or_Add_Back(midiEvent.position) = encoding.GetString(bytes[PREFIXES[1].Length..(bytes.Length - 1)]);
                    else
                        globals.Get_Or_Add_Back(midiEvent.position).Add(bytes.ToArray());
                }
            }
            return true;
        }
    };
}
