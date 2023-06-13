using Framework.Types;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Framework.Serialization
{
    public class MidiFileReader : IDisposable
    {
        public static readonly Dictionary<string, MidiTrackType> TRACKNAMES = new();
        
        static MidiFileReader() {
            TRACKNAMES.Add("EVENTS", MidiTrackType.Events);
            TRACKNAMES.Add("PART GUITAR", MidiTrackType.Guitar_5);
            TRACKNAMES.Add("T1 GEMS", MidiTrackType.Guitar_5);
            TRACKNAMES.Add("PART GUITAR GHL", MidiTrackType.Guitar_6);
            TRACKNAMES.Add("PART BASS", MidiTrackType.Bass_5);
            TRACKNAMES.Add("PART BASS GHL", MidiTrackType.Bass_6);
            TRACKNAMES.Add("PART RHYTHM", MidiTrackType.Rhythm);
            TRACKNAMES.Add("PART GUITAR COOP", MidiTrackType.Coop);
            TRACKNAMES.Add("PART KEYS", MidiTrackType.Keys);
            TRACKNAMES.Add("PART DRUMS", MidiTrackType.Drums);
            TRACKNAMES.Add("PART VOCALS", MidiTrackType.Vocals);
            TRACKNAMES.Add("PART HARM1", MidiTrackType.Harm1);
            TRACKNAMES.Add("PART HARM2", MidiTrackType.Harm2);
            TRACKNAMES.Add("PART HARM3", MidiTrackType.Harm3);
            TRACKNAMES.Add("HARM1", MidiTrackType.Harm1);
            TRACKNAMES.Add("HARM2", MidiTrackType.Harm2);
            TRACKNAMES.Add("HARM3", MidiTrackType.Harm3);
            TRACKNAMES.Add("PART REAL_GUITAR", MidiTrackType.Real_Guitar);
            TRACKNAMES.Add("PART REAL_GUITAR_22", MidiTrackType.Real_Guitar_22);
            TRACKNAMES.Add("PART REAL_BASS", MidiTrackType.Real_Bass);
            TRACKNAMES.Add("PART REAL_BASS_22", MidiTrackType.Real_Bass_22);
            TRACKNAMES.Add("PART REAL_KEYS_X", MidiTrackType.Real_Keys_X);
            TRACKNAMES.Add("PART REAL_KEYS_H", MidiTrackType.Real_Keys_H);
            TRACKNAMES.Add("PART REAL_KEYS_M", MidiTrackType.Real_Keys_M);
            TRACKNAMES.Add("PART REAL_KEYS_E", MidiTrackType.Real_Keys_E);
            TRACKNAMES.Add("BEATS", MidiTrackType.Beats);
        }

        internal static readonly byte[][] TRACKTAGS = { Encoding.ASCII.GetBytes("MThd"), Encoding.ASCII.GetBytes("MTrk") };

        private struct MidiHeader
        {
            public ushort format;
            public ushort numTracks;
            public ushort tickRate;
        };
        private MidiHeader m_header;
        private ushort m_trackCount = 0;

        private MidiEvent m_event;
        private int m_runningOffset;

        private readonly byte m_multiplierNote = 116;
        private readonly BinaryFileReader m_reader;

        public MidiFileReader(FrameworkFile file)
        {
            m_reader = new BinaryFileReader(file);
            ProcessHeaderChunk();
        }

        public MidiFileReader(byte[] data) : this(new FrameworkFile(data)) {}
        public MidiFileReader(string path, byte multiplierNote) : this(File.ReadAllBytes(path))
        {
            m_multiplierNote = multiplierNote;
        }

        public void Dispose()
        {
            m_reader.Dispose();
            GC.SuppressFinalize(this);
        }

        public bool StartTrack()
        {
            if (m_trackCount == m_header.numTracks)
                return false;

            if (m_event.type != MidiEventType.Reset_Or_Meta)
                m_reader.ExitSection();

            m_reader.ExitSection();
            m_trackCount++;

            if (!m_reader.CompareTag(TRACKTAGS[1]))
                throw new Exception($"Midi Track Tag 'MTrk' not found for Track '{m_trackCount}'");

            m_reader.EnterSection((int)m_reader.ReadUInt32(Endianness.BigEndian));

            m_event.position = 0;
            m_event.type = MidiEventType.Reset_Or_Meta;

            int start = m_reader.Position;
            if (!TryParseEvent() || m_event.type != MidiEventType.Text_TrackName)
            {
                m_reader.ExitSection();
                m_reader.Position = start;
                m_event.position = 0;
                m_event.type = MidiEventType.Reset_Or_Meta;
            }
            return true;
        }

        public bool TryParseEvent()
        {
            if (m_event.type != MidiEventType.Reset_Or_Meta)
                m_reader.ExitSection();

            m_event.position += m_reader.ReadVLQ();
            byte tmp = m_reader.PeekByte();
            MidiEventType type = (MidiEventType)tmp;
            if (type < MidiEventType.Note_Off)
            {
                if (m_event.type < MidiEventType.Note_Off || m_event.type >= MidiEventType.SysEx)
                    throw new Exception("Invalid running event");
                m_reader.EnterSection(m_runningOffset);
            }
            else
            {
                m_reader.Move_Unsafe(1);
                if (type < MidiEventType.SysEx)
                {
                    m_event.channel = (byte)(tmp & 15);
                    m_event.type = (MidiEventType)(tmp & 240);
                    m_runningOffset = m_event.type switch
                    {
                        MidiEventType.Note_On => 2,
                        MidiEventType.Note_Off => 2,
                        MidiEventType.Control_Change => 2,
                        MidiEventType.Key_Pressure => 2,
                        MidiEventType.Pitch_Wheel => 2,
                        _ => 1
                    };
                    m_reader.EnterSection(m_runningOffset);
                }
                else
                {
                    switch (type)
                    {
                        case MidiEventType.Reset_Or_Meta:
                            type = (MidiEventType)m_reader.ReadByte();
                            goto case MidiEventType.SysEx_End;
                        case MidiEventType.SysEx:
                        case MidiEventType.SysEx_End:
                            m_reader.EnterSection((int)m_reader.ReadVLQ());
                            break;
                        case MidiEventType.Song_Position:
                            m_reader.EnterSection(2);
                            break;
                        case MidiEventType.Song_Select:
                            m_reader.EnterSection(1);
                            break;
                        default:
                            m_reader.EnterSection(0);
                            break;
                    }
                    m_event.type = type;

                    if (m_event.type == MidiEventType.End_Of_Track)
                        return false;
                }
            }
            return true;
        }

        public ref MidiEvent GetParsedEvent() { return ref m_event; }

        public ushort GetTickRate() { return m_header.tickRate; }
        public ushort GetTrackNumber() { return m_trackCount; }
        public MidiEvent GetEvent() { return m_event; }
        public byte GetMultiplierNote() { return m_multiplierNote; }

        public ReadOnlySpan<byte> ExtractTextOrSysEx()
        {
            return m_reader.ReadSpan(m_reader.Boundary - m_reader.Position);
        }

        public void ExtractMidiNote(ref MidiNote note)
        {
            ReadOnlySpan<byte> span = m_reader.ReadSpan(2);
            note.value = span[0];
            note.velocity = span[1];
        }

        public MidiNote ExtractMidiNote()
        {
            MidiNote note = new();
            ExtractMidiNote(ref note);
            return note;
        }
        public ControlChange ExtractControlChange()
        {
            ReadOnlySpan<byte> bytes = m_reader.ReadSpan(2);
            return new ControlChange()
            {
                Controller = bytes[0],
                Value = bytes[1],
            };
        }

        public uint ExtractMicrosPerQuarter()
        {
            ReadOnlySpan<byte> bytes = m_reader.ReadSpan(3);
            return (uint)(bytes[0] << 16) | BinaryPrimitives.ReadUInt16BigEndian(bytes[1..]);
        }
        public TimeSig ExtractTimeSig()
        {
            ReadOnlySpan<byte> bytes = m_reader.ReadSpan(4);
            return new TimeSig(bytes[0], bytes[1], bytes[2], bytes[3]);
        }
        private void ProcessHeaderChunk()
        {
            if (!m_reader.CompareTag(TRACKTAGS[0]))
                throw new Exception("Midi Header Chunk Tag 'MTrk' not found");

            m_reader.EnterSection((int)m_reader.ReadUInt32(Endianness.BigEndian));
            m_header.format = m_reader.ReadUInt16(Endianness.BigEndian);
            m_header.numTracks = m_reader.ReadUInt16(Endianness.BigEndian);
            m_header.tickRate = m_reader.ReadUInt16(Endianness.BigEndian);
            m_event.type = MidiEventType.Reset_Or_Meta;
        }
    };
}
