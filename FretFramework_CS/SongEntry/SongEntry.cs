using Framework.Serialization;
using Framework.Song.Tracks.Instrument.DrumTrack;
using Framework.Song;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Framework.SongEntry.TrackScan.Instrument.Drums;
using Framework.Song.Tracks.Notes.Drums;

namespace Framework.SongEntry
{
    public class SongEntry
    {
        public TrackScans m_tracks = new();

        public void Scan_Midi(string path)
        {
            MidiFileReader reader = new(path, 116);
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() > 1 && reader.GetEvent().type == MidiEventType.Text_TrackName)
                {
                    string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                    if (MidiFileReader.TRACKNAMES.TryGetValue(name, out MidiTrackType type) && type != MidiTrackType.Events)
                        m_tracks.ScanFromMidi(type, ref reader);
                }
            }
        }

        public void Scan_Chart(string path)
        {
            ChartFileReader reader = new(path);
            if (!reader.ValidateHeaderTrack())
                throw new Exception("[Song] track expected at the start of the file");
            // Add [Song] parsing later
            reader.SkipTrack();

            LegacyDrumScan legacy = new();
            while (reader.IsStartOfTrack())
            {
                if (!reader.ValidateDifficulty() || !reader.ValidateInstrument() || !m_tracks.ScanFromDotChart(ref legacy, ref reader))
                    reader.SkipTrack();
            }

            if (legacy.Type == DrumType.FIVE_LANE)
                m_tracks.drums5 |= legacy.Values;
            else
                m_tracks.drums_4pro |= legacy.Values;
        }
    }
}
