using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using Framework.Types;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Framework.Serialization.XboxSTFS
{
    public unsafe class DTAFileReader : TxtReader_Base
    {
        private readonly List<int> nodeEnds = new();

        private DTAFileReader(FrameworkFile file, bool disposeFile) : base(file, disposeFile) { SkipWhiteSpace(); }

        public DTAFileReader(FrameworkFile file) : this(file, false) { }

        public DTAFileReader(byte[] data) : this(new FrameworkFile_Handle(data), true) { }

        public DTAFileReader(string path) : this(new FrameworkFile_Alloc(path), true) { }

        public DTAFileReader(PointerHandler pointer, bool dispose = false) : this(new FrameworkFile_Pointer(pointer, dispose), true) { }

        public override void SkipWhiteSpace()
        {
            int length = file.Length;
            while (_position < length)
            {
                byte ch = file.ptr[_position];
                if (ch <= 32)
                    ++_position;
                else if (ch == ';')
                {
                    ++_position;
                    while (_position < length)
                    {
                        ++_position;
                        if (file.ptr[_position - 1] == '\n')
                            break;
                    }
                }
                else
                    break;
            }
        }

        public string GetNameOfNode()
        {
            byte ch = file.ptr[_position];
            if (ch == '(')
                return string.Empty;

            bool hasApostrophe = true;
            if (ch != '\'')
            {
                if (file.ptr[_position - 1] != '(')
                    throw new Exception("Invalid name call");
                hasApostrophe = false;
            }
            else
                ch = file.ptr[++_position];

            int start = _position;
            while (ch != '\'')
            {
                if (ch <= 32)
                {
                    if (hasApostrophe)
                        throw new Exception("Invalid name format");
                    break;
                }
                ch = file.ptr[++_position];
            }
            int end = _position++;
            SkipWhiteSpace();
            return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(file.ptr + start, end - start));
        }

        public string ExtractText()
        {
            byte ch = file.ptr[_position];
            bool inSquirley = ch == '{';
            bool inQuotes = !inSquirley && ch == '\"';
            bool inApostrophes = !inQuotes && ch == '\'';

            if (inSquirley || inQuotes || inApostrophes)
                ++_position;

            int start = _position++;
            while (_position < _next)
            {
                ch = file.ptr[_position];
                if (ch == '{')
                    throw new Exception("Text error - no { braces allowed");

                if (ch == '}')
                {
                    if (inSquirley)
                        break;
                    throw new Exception("Text error - no \'}\' allowed");
                }
                else if (ch == '\"')
                {
                    if (inQuotes)
                        break;
                    if (!inSquirley)
                        throw new Exception("Text error - no quotes allowed");
                }
                else if (ch == '\'')
                {
                    if (inApostrophes)
                        break;
                    if (!inSquirley && !inQuotes)
                        throw new Exception("Text error - no apostrophes allowed");
                }
                else if (ch <= 32)
                {
                    if (inApostrophes)
                        throw new Exception("Text error - no whitespace allowed");
                    if (!inSquirley && !inQuotes)
                        break;
                }
                ++_position;
            }

            int end = _position;
            if (_position != _next)
            {
                ++_position;
                SkipWhiteSpace();
            }
            else if (inSquirley || inQuotes || inApostrophes)
                throw new Exception("Improper end to text");

            return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(file.ptr + start, end - start));
        }

        public List<ushort> ExtractList()
        {
            List<ushort> values = new();
            while (*CurrentPtr != ')')
                values.Add(ReadUInt16());
            return values;
        }

        public List<string> ExtractStringList()
        {
            List<string> strings = new();
            while (*CurrentPtr != ')')
                strings.Add(ExtractText());
            return strings;
        }

        public bool StartNode()
        {
            byte ch = file.ptr[_position];
            if (ch != '(')
                return false;

            ++_position;
            SkipWhiteSpace();

            int scopeLevel = 1;
            bool inApostropes = false;
            bool inQuotes = false;
            bool inComment = false;
            int pos = _position;
            int length = file.Length;
            while (scopeLevel >= 1 && pos < length)
            {
                ch = file.ptr[pos];
                if (inComment)
                {
                    if (ch == '\n')
                        inComment = false;
                }
                else if (ch == '\"')
                {
                    if (inApostropes)
                        throw new Exception("Ah hell nah wtf");
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes)
                {
                    if (!inApostropes)
                    {
                        if (ch == '(')
                            ++scopeLevel;
                        else if (ch == ')')
                            --scopeLevel;
                        else if (ch == '\'')
                            inApostropes = true;
                        else if (ch == ';')
                            inComment = true;
                    }
                    else if (ch == '\'')
                        inApostropes = false;
                }
                ++pos;
            }
            nodeEnds.Add(pos - 1);
            _next = pos - 1;
            return true;
        }

        public void EndNode()
        {
            int index = nodeEnds.Count - 1;
            _position = nodeEnds[index] + 1;
            nodeEnds.RemoveAt(index);
            if (index > 0)
                _next = nodeEnds[--index];
            SkipWhiteSpace();
        }
    };

    public class DTARanks
    {
        public ushort guitar5 = ushort.MaxValue;
        public ushort bass5 = ushort.MaxValue;
        public ushort drum4 = ushort.MaxValue;
        public ushort keys = ushort.MaxValue;
        public ushort vocals = ushort.MaxValue;
        public ushort harmony = ushort.MaxValue;
        public ushort real_guitar = ushort.MaxValue;
        public ushort real_bass = ushort.MaxValue;
        public ushort drum4_pro = ushort.MaxValue;
        public ushort real_keys = ushort.MaxValue;
        public ushort band = ushort.MaxValue;
    };

    public unsafe struct DTAAudio
    {
        public fixed float pan[2];
        public fixed float volume[2];
        public fixed float core[2];

        public DTAAudio()
        {
            core[0] = -1;
            core[1] = -1;
        }
    };

    public unsafe class DTAFileNode
    {
        public static List<DTAFileNode> GetNodes(DTAFileReader reader)
        {
            List<DTAFileNode> nodes = new();
            while (reader.StartNode())
            {
                nodes.Add(new DTAFileNode(reader));
                reader.EndNode();
            }
            return nodes;
        }

        public DTAFileNode(DTAFileReader reader)
        {
            NodeName = reader.GetNameOfNode();
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                switch (name)
                {
                    case "name": Name = reader.ExtractText(); break;
                    case "artist": Artist = reader.ExtractText(); break;
                    case "master": IsMaster = reader.ReadBoolean(); break;
                    case "context": Context = reader.ReadUInt32(); break;
                    case "song": SongLoop(ref reader); break;
                    case "song_vocals": while(reader.StartNode()) reader.EndNode(); break;
                    case "song_scroll_speed": ScrollSpeed = reader.ReadUInt32(); break;
                    case "tuning_offset_cents": TuningOffsetCents = reader.ReadInt32(); break;
                    case "bank": VocalPercBank = reader.ExtractText(); break;
                    case "anim_tempo":
                        {
                            string val = reader.ExtractText();
                            AnimTempo = val switch
                            {
                                "kTempoSlow" => 16,
                                "kTempoMedium" => 32,
                                "kTempoFast" => 64,
                                _ => uint.Parse(val)
                            };
                            break;
                        }
                    case "preview":
                        PreviewStart = reader.ReadUInt32();
                        PreviewEnd = reader.ReadUInt32();
                        break;
                    case "rank":
                        {
                            ranks = new();
                            RankLoop(ref reader); break;
                        }
                    case "solo":
                        {
                            soloes = new();
                            while (reader.StartNode())
                            {
                                soloes.Add(reader.ExtractText());
                                reader.EndNode();
                            }
                            break;
                        }
                    case "genre": Genre = reader.ExtractText(); break;
                    case "decade": Decade = reader.ExtractText(); break;
                    case "vocal_gender": VocalIsMale = reader.ExtractText() == "male"; break;
                    case "format": Format = reader.ReadUInt32(); break;
                    case "version": Version = reader.ReadUInt32(); break;
                    case "fake": Fake = reader.ExtractText(); break;
                    case "downloaded": Downloaded = reader.ExtractText(); break;
                    case "game_origin":
                        {
                            Source = reader.ExtractText();
                            if ((Source == "ugc" || Source == "ugc_plus"))
                            {
                                if (!NodeName.StartsWith("UGC_"))
                                    Source = "customs";
                            }
                            else if (Source == "rb1" || Source == "rb1_dlc" || Source == "rb1dlc" ||
                                Source == "gdrb" || Source == "greenday" || Source == "beatles" ||
                                Source == "tbrb" || Source == "lego" || Source == "lrb" ||
                                Source == "rb2" || Source == "rb3" || Source == "rb3_dlc" || Source == "rb3dlc")
                            {
                                Source = "Harmonix";
                            }
                            break;
                        }
                    case "song_id": SongID = reader.ExtractText(); break;
                    case "rating": Rating = reader.ReadUInt32(); break;
                    case "short_version": ShortVersion = reader.ReadUInt32(); break;
                    case "album_art": HasAlbumArt = reader.ReadBoolean(); break;
                    case "year_released": Year_Released = reader.ReadUInt32(); break;
                    case "year_recorded": Year_Recorded = reader.ReadUInt32(); break;
                    case "album_name": Album = reader.ExtractText(); break;
                    case "album_track_number": AlbumTrack = reader.ReadUInt16(); break;
                    case "pack_name": Packname = reader.ExtractText(); break;
                    case "base_points": BasePoints = reader.ReadUInt32(); break;
                    case "band_fail_cue": BandFailCue = reader.ExtractText(); break;
                    case "drum_bank": DrumBank = reader.ExtractText(); break;
                    case "song_length": Length = reader.ReadUInt32(); break;
                    case "sub_genre": Subgenre = reader.ExtractText(); break;
                    case "author": Charter = reader.ExtractText(); break;
                    case "guide_pitch_volume": GuidePitchVolume = reader.ReadFloat(); break;
                    case "encoding": Encoding = reader.ExtractText(); break;
                    case "vocal_tonic_note": VocalTonic = reader.ReadUInt32(); break;
                    case "song_tonality": Tonality = reader.ReadBoolean(); break;
                    case "alternate_path": AlternatePath = reader.ReadBoolean(); break;
                    case "real_guitar_tuning":
                        {
                            realGuitarTuning = new short[6];
                            while (reader.StartNode())
                            {
                                for (int i = 0; i < 6; i++)
                                    realGuitarTuning[i] = reader.ReadInt16();
                                reader.EndNode();
                            }
                            break;
                        }
                    case "real_bass_tuning":
                        {
                            realBassTuning = new short[4];
                            while (reader.StartNode())
                            {
                                for (int i = 0; i < 4; i++)
                                    realBassTuning[i] = reader.ReadInt16();
                                reader.EndNode();
                            }
                            break;
                        }
                    case "video_venues":
                        {
                            videoVenues = new();
                            while(reader.StartNode())
                            {
                                videoVenues.Add(reader.ExtractText());
                                reader.EndNode();
                            }
                            break;
                        }
                    case "extra_authoring":
                        foreach (string str in reader.ExtractStringList())
                        {
                            if (str == "disc_update")
                            {
                                DiscUpdate = true;
                                break;
                            }
                        }
                        break;
                    default:
                        others.Add(new(name, reader.ExtractText()));
                        break;
                }
                reader.EndNode();
            }
        }

        private void SongLoop(ref DTAFileReader reader)
        {
            while (reader.StartNode())
            {
                string descriptor = reader.GetNameOfNode();
                switch (descriptor)
                {
                    case "name": Location = reader.ExtractText(); break;
                    case "tracks": TracksLoop(ref reader); break;
                    case "crowd_channels": crowdIndices = reader.ExtractList(); break;
                    case "vocal_parts": NumVocalParts = reader.ReadUInt16(); break;
                    case "pans": PanLoop(ref reader); break;
                    case "vols": VolLoop(ref reader); break;
                    case "cores": CoreLoop(ref reader); break;
                    case "hopo_threshold": Hopo_Threshold = reader.ReadUInt32(); break;
                    case "midi_file": MidiFile = reader.ExtractText(); break;
                }
                reader.EndNode();
            }
        }

        private void TracksLoop(ref DTAFileReader reader)
        {
            while (reader.StartNode())
            {
                while (reader.StartNode())
                {
                    switch(reader.GetNameOfNode())
                    {
                        case "drum":
                            {
                                while (reader.StartNode())
                                {
                                    drumIndices = reader.ExtractList();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "bass":
                            {
                                while (reader.StartNode())
                                {
                                    bassIndices = reader.ExtractList();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "guitar":
                            {
                                while (reader.StartNode())
                                {
                                    guitarIndices = reader.ExtractList();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "keys":
                            {
                                while (reader.StartNode())
                                {
                                    keysIndices = reader.ExtractList();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "vocals":
                            {
                                while (reader.StartNode())
                                {
                                    vocalsIndices = reader.ExtractList();
                                    reader.EndNode();
                                }
                                break;
                            }
                    }
                    reader.EndNode();
                }
                reader.EndNode();
            }
        }

        private void PanLoop(ref DTAFileReader reader)
        {
            while (reader.StartNode())
            {
                if (drumIndices != null)
                {
                    if (drumIndices.Count > 2)
                    {
                        kick.pan[0] = reader.ReadFloat();
                        if (drumIndices.Count > 5)
                            kick.pan[1] = reader.ReadFloat();
                    }

                    if (drumIndices.Count > 1)
                    {
                        snare.pan[0] = reader.ReadFloat();
                        if (drumIndices.Count > 4)
                            snare.pan[1] = reader.ReadFloat();
                    }

                    if (drumIndices.Count > 0)
                    {
                        cymbals.pan[0] = reader.ReadFloat();
                        if (drumIndices.Count > 3)
                            cymbals.pan[1] = reader.ReadFloat();
                    }
                }

                if (bassIndices != null)
                {
                    bass.pan[0] = reader.ReadFloat();
                    if (bassIndices.Count > 1)
                        bass.pan[1] = reader.ReadFloat();
                }

                if (guitarIndices != null)
                {
                    guitar.pan[0] = reader.ReadFloat();
                    if (guitarIndices.Count > 1)
                        guitar.pan[1] = reader.ReadFloat();
                }

                if (keysIndices != null)
                {
                    keys.pan[0] = reader.ReadFloat();
                    if (keysIndices.Count > 1)
                        keys.pan[1] = reader.ReadFloat();
                }

                if (vocalsIndices != null)
                {
                    vocals.pan[0] = reader.ReadFloat();
                    if (vocalsIndices.Count > 1)
                        vocals.pan[1] = reader.ReadFloat();
                }

                if (reader.ReadFloat(ref tracks.pan[0]) && reader.ReadFloat(ref tracks.pan[1]))
                {
                    if (crowdIndices != null)
                    {
                        crowd.pan[0] = reader.ReadFloat();
                        if (crowdIndices.Count > 1)
                            crowd.pan[1] = reader.ReadFloat();
                    }
                }
                reader.EndNode();
            }
        }

        private void VolLoop(ref DTAFileReader reader)
        {
            while (reader.StartNode())
            {
                if (drumIndices != null)
                {
                    if (drumIndices.Count > 2)
                    {
                        kick.volume[0] = reader.ReadFloat();
                        if (drumIndices.Count > 5)
                            kick.volume[1] = reader.ReadFloat();
                    }

                    if (drumIndices.Count > 1)
                    {
                        snare.volume[0] = reader.ReadFloat();
                        if (drumIndices.Count > 4)
                            snare.volume[1] = reader.ReadFloat();
                    }

                    if (drumIndices.Count > 0)
                    {
                        cymbals.volume[0] = reader.ReadFloat();
                        if (drumIndices.Count > 3)
                            cymbals.volume[1] = reader.ReadFloat();
                    }
                }

                if (bassIndices != null)
                {
                    bass.volume[0] = reader.ReadFloat();
                    if (bassIndices.Count > 1)
                        bass.volume[1] = reader.ReadFloat();
                }

                if (guitarIndices != null)
                {
                    guitar.volume[0] = reader.ReadFloat();
                    if (guitarIndices.Count > 1)
                        guitar.volume[1] = reader.ReadFloat();
                }

                if (keysIndices != null)
                {
                    keys.volume[0] = reader.ReadFloat();
                    if (keysIndices.Count > 1)
                        keys.volume[1] = reader.ReadFloat();
                }

                if (vocalsIndices != null)
                {
                    vocals.volume[0] = reader.ReadFloat();
                    if (vocalsIndices.Count > 1)
                        vocals.volume[1] = reader.ReadFloat();
                }

                if (reader.ReadFloat(ref tracks.volume[0]) && reader.ReadFloat(ref tracks.volume[1]))
                {
                    if (crowdIndices != null)
                    {
                        crowd.volume[0] = reader.ReadFloat();
                        if (crowdIndices.Count > 1)
                            crowd.volume[1] = reader.ReadFloat();
                    }
                }
                reader.EndNode();
            }
        }

        private void CoreLoop(ref DTAFileReader reader)
        {
            while (reader.StartNode())
            {
                if (drumIndices != null)
                {
                    if (drumIndices.Count > 2)
                    {
                        kick.core[0] = reader.ReadFloat();
                        if (drumIndices.Count > 5)
                            kick.core[1] = reader.ReadFloat();
                    }

                    if (drumIndices.Count > 1)
                    {
                        snare.core[0] = reader.ReadFloat();
                        if (drumIndices.Count > 4)
                            snare.core[1] = reader.ReadFloat();
                    }

                    if (drumIndices.Count > 0)
                    {
                        cymbals.core[0] = reader.ReadFloat();
                        if (drumIndices.Count > 3)
                            cymbals.core[1] = reader.ReadFloat();
                    }
                }

                if (bassIndices != null && bassIndices.Count > 0)
                {
                    bass.core[0] = reader.ReadFloat();
                    if (bassIndices.Count > 1)
                        bass.core[1] = reader.ReadFloat();
                }

                if (guitarIndices != null && guitarIndices.Count > 0)
                {
                    guitar.core[0] = reader.ReadFloat();
                    if (guitarIndices.Count > 1)
                        guitar.core[1] = reader.ReadFloat();
                }

                if (keysIndices != null && keysIndices.Count > 0)
                {
                    keys.core[0] = reader.ReadFloat();
                    if (keysIndices.Count > 1)
                        keys.core[1] = reader.ReadFloat();
                }

                if (vocalsIndices != null && vocalsIndices.Count > 0)
                {
                    vocals.core[0] = reader.ReadFloat();
                    if (vocalsIndices.Count > 1)
                        vocals.core[1] = reader.ReadFloat();
                }

                if (reader.ReadFloat(ref tracks.core[0]) && reader.ReadFloat(ref tracks.core[1]))
                {
                    if (crowdIndices != null && crowdIndices.Count > 0)
                    {
                        crowd.core[0] = reader.ReadFloat();
                        if (crowdIndices.Count > 1)
                            crowd.core[1] = reader.ReadFloat();
                    }
                }
                reader.EndNode();
            }
        }

        private void RankLoop(ref DTAFileReader reader)
        {
            while (reader.StartNode())
            {
                switch (reader.GetNameOfNode())
                {
                    case "drum":
                    case "drums": ranks!.drum4_pro = reader.ReadUInt16(); break;
                    case "guitar": ranks!.guitar5 = reader.ReadUInt16(); break;
                    case "bass": ranks!.bass5 = reader.ReadUInt16(); break;
                    case "vocals": ranks!.vocals = reader.ReadUInt16(); break;
                    case "keys": ranks!.keys = reader.ReadUInt16(); break;
                    case "realGuitar":
                    case "real_guitar": ranks!.real_guitar = reader.ReadUInt16(); break;
                    case "realBass":
                    case "real_bass": ranks!.real_bass = reader.ReadUInt16(); break;
                    case "realKeys":
                    case "real_keys": ranks!.real_keys = reader.ReadUInt16(); break;
                    case "realDrums":
                    case "real_drums": ranks!.drum4_pro = reader.ReadUInt16(); break;
                    case "harmVocals":
                    case "vocal_harm": ranks!.harmony = reader.ReadUInt16(); break;
                    case "band": ranks!.band = reader.ReadUInt16(); break;
                }
                reader.EndNode();
            }
        }

        public string NodeName { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;
        public string Artist { get; private set; } = string.Empty;
        public string Album { get; private set; } = string.Empty;
        public string Genre { get; private set; } = string.Empty;
        public string Subgenre { get; private set; } = string.Empty;
        public string Charter { get; private set; } = string.Empty;
        public string Source { get; private set; } = string.Empty;
        public string Encoding { get; private set; } = string.Empty;

        public string SongID { get; private set; } = string.Empty;
        public string VocalPercBank { get; private set; } = string.Empty;
        public string DrumBank { get; private set; } = string.Empty;
        public string BandFailCue { get; private set; } = string.Empty;
        public string Decade { get; private set; } = string.Empty;
        public string Fake { get; private set; } = string.Empty;
        public string Downloaded { get; private set; } = string.Empty;
        public string Packname { get; private set; } = string.Empty;

        public uint Year_Released { get; private set; } = uint.MaxValue;
        public uint Year_Recorded { get; private set; } = uint.MaxValue;
        public ushort AlbumTrack { get; private set; } = ushort.MaxValue;
        public uint ScrollSpeed { get; private set; } = uint.MaxValue;
        public uint Length { get; private set; } = uint.MaxValue;
        public uint Version { get; private set; } = uint.MaxValue;
        public uint Format { get; private set; } = uint.MaxValue;
        public uint Rating { get; private set; } = uint.MaxValue;
        public int  TuningOffsetCents { get; private set; } = int.MaxValue;
        public uint VocalTonic { get; private set; } = uint.MaxValue;
        public uint ShortVersion { get; private set; } = 0;
        public uint AnimTempo { get; private set; } = uint.MaxValue;
        public uint Context { get; private set; } = uint.MaxValue;
        public uint BasePoints { get; private set; } = uint.MaxValue;
        public uint Hopo_Threshold { get; private set; } = uint.MaxValue;

        public float GuidePitchVolume { get; private set; } = -1;

        public bool IsMaster { get; private set; } = false;
        public bool HasAlbumArt { get; private set; } = false;
        public bool Tonality { get; private set; } = false;
        public bool VocalIsMale { get; private set; } = true;
        public bool AlternatePath { get; private set; } = false;

        public uint PreviewStart { get; private set; } = uint.MaxValue;
        public uint PreviewEnd { get; private set; } = uint.MaxValue;

        public readonly DTARanks? ranks;

        private readonly List<string>? soloes;
        private readonly List<string>? videoVenues;

        private short[] realGuitarTuning = Array.Empty<short>();
        private short[] realBassTuning = Array.Empty<short>();

        public short[] RealGuitarTuning { get { return realGuitarTuning; } }
        public short[] RealBassTuning { get { return realBassTuning; } }

        public string Location { get; private set; } = string.Empty;
        
        
        List<ushort>? drumIndices;
        List<ushort>? bassIndices;
        List<ushort>? guitarIndices;
        List<ushort>? keysIndices;
        List<ushort>? vocalsIndices;
        List<ushort>? crowdIndices;
        
        public ushort NumVocalParts { get; private set; } = ushort.MaxValue;

        private DTAAudio kick;
        private DTAAudio snare;
        private DTAAudio cymbals;
        private DTAAudio bass;
        private DTAAudio guitar;
        private DTAAudio keys;
        private DTAAudio vocals;
        private DTAAudio tracks;
        private DTAAudio crowd;
        public DTAAudio KickAudio => kick;
        public DTAAudio SnareAudio => snare;
        public DTAAudio CymbalsAudio => cymbals;
        public DTAAudio BassAudio => bass;
        public DTAAudio GuitarAudio => guitar;
        public DTAAudio KeysAudio => keys;
        public DTAAudio VocalAudio => vocals;
        public DTAAudio TrackAudio => tracks;
        public DTAAudio CrowdAudio => crowd;

        public string MidiFile { get; private set; } = string.Empty;
        public bool DiscUpdate { get; private set; }

        readonly List<(string, string)> others = new();
    };
}
