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
        public static readonly Dictionary<string, MidiTrackType> TRACKNAMES = new()
        {
            {"EVENTS", MidiTrackType.Events},
            {"PART GUITAR", MidiTrackType.Guitar_5},
            {"T1 GEMS", MidiTrackType.Guitar_5},
            {"PART GUITAR GHL", MidiTrackType.Guitar_6},
            {"PART BASS", MidiTrackType.Bass_5},
            {"PART BASS GHL", MidiTrackType.Bass_6},
            {"PART RHYTHM", MidiTrackType.Rhythm},
            {"PART GUITAR COOP", MidiTrackType.Coop},
            {"PART KEYS", MidiTrackType.Keys},
            {"PART DRUMS", MidiTrackType.Drums},
            {"PART VOCALS", MidiTrackType.Vocals},
            {"PART HARM1", MidiTrackType.Harm1},
            {"PART HARM2", MidiTrackType.Harm2},
            {"PART HARM3", MidiTrackType.Harm3},
            {"HARM1", MidiTrackType.Harm1},
            {"HARM2", MidiTrackType.Harm2},
            {"HARM3", MidiTrackType.Harm3},
            {"PART REAL_GUITAR", MidiTrackType.Real_Guitar},
            {"PART REAL_GUITAR_22", MidiTrackType.Real_Guitar_22},
            {"PART REAL_BASS", MidiTrackType.Real_Bass},
            {"PART REAL_BASS_22", MidiTrackType.Real_Bass_22},
            {"PART REAL_KEYS_X", MidiTrackType.Real_Keys_X},
            {"PART REAL_KEYS_H", MidiTrackType.Real_Keys_H},
            {"PART REAL_KEYS_M", MidiTrackType.Real_Keys_M},
            {"PART REAL_KEYS_E", MidiTrackType.Real_Keys_E},
            {"BEATS", MidiTrackType.Beats},
        };

        internal static readonly byte[][] TRACKTAGS = { Encoding.ASCII.GetBytes("MThd"), Encoding.ASCII.GetBytes("MTrk") };

        static MidiFileReader() {}

        private struct MidiHeader
        {
            public ushort format;
            public ushort numTracks;
            public ushort tickRate;
        };
        private MidiHeader m_header;
        private ushort m_trackCount = 0;

        private MidiEvent m_event;
        private MidiEventType m_midiEvent = MidiEventType.Reset_Or_Meta;
        private int m_runningOffset;

        private readonly byte m_multiplierNote;
        private readonly BinaryFileReader m_reader;
        private bool disposeReader = false;

        public MidiFileReader(BinaryFileReader reader, bool disposeReader, byte multiplierNote = 116)
        {
            m_reader = reader;
            this.disposeReader = disposeReader;
            m_multiplierNote = multiplierNote;
            ProcessHeaderChunk();
        }

        public MidiFileReader(FrameworkFile file, bool disposeFile = false, byte multiplierNote = 116) : this(new BinaryFileReader(file, disposeFile), true, multiplierNote) { }

        public MidiFileReader(byte[] data, byte multiplierNote = 116) : this(new BinaryFileReader(data), true, multiplierNote) { }

        public MidiFileReader(string path, byte multiplierNote = 116) : this(new BinaryFileReader(path), true, multiplierNote) { }

        public MidiFileReader(PointerHandler handler, bool disposeHandler = false, byte multiplierNote = 116) : this(new BinaryFileReader(handler, disposeHandler), true, multiplierNote) { }

        public void Dispose()
        {
            if (disposeReader)
            {
                m_reader.Dispose();
                disposeReader = false;
            }
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
                if (m_midiEvent == MidiEventType.Reset_Or_Meta)
                    throw new Exception("Invalid running event");
                m_event.type = m_midiEvent;
                m_reader.EnterSection(m_runningOffset);
            }
            else
            {
                m_reader.Move_Unsafe(1);
                if (type < MidiEventType.SysEx)
                {
                    m_event.channel = (byte)(tmp & 15);
                    m_midiEvent = (MidiEventType)(tmp & 240);
                    m_runningOffset = m_midiEvent switch
                    {
                        MidiEventType.Note_On => 2,
                        MidiEventType.Note_Off => 2,
                        MidiEventType.Control_Change => 2,
                        MidiEventType.Key_Pressure => 2,
                        MidiEventType.Pitch_Wheel => 2,
                        _ => 1
                    };
                    m_event.type = m_midiEvent;
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
            note.value = m_reader.ReadByte();
            note.velocity = m_reader.ReadByte();
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
