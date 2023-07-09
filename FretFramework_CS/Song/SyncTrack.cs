using Framework.FlatMaps;
using Framework.Serialization;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song
{
    public readonly struct SyncTrack
    {
        public readonly TimedNativeFlatMap<Tempo> tempoMarkers = new();
        public readonly TimedNativeFlatMap<TimeSig> timeSigs = new();
        public SyncTrack() {}

        public void AddFromMidi(MidiFileReader reader)
        {
            while (reader.TryParseEvent())
            {
                MidiEvent midiEvent = reader.GetEvent();
                switch (midiEvent.type)
                {
                    case MidiEventType.Tempo:
                        tempoMarkers.Get_Or_Add_Back(midiEvent.position).Micros = reader.ExtractMicrosPerQuarter();
                        break;
                    case MidiEventType.Time_Sig:
                        timeSigs.Get_Or_Add_Back(midiEvent.position) = reader.ExtractTimeSig();
                        break;
                }
            }
        }

        public void AddFromDotChart(ChartFileReader reader)
        {
            while (reader.IsStillCurrentTrack())
            {
                var trackEvent = reader.ParseEvent();
                switch (trackEvent.Item2)
                {
                    case ChartEvent.BPM:
                        tempoMarkers.Get_Or_Add_Back(trackEvent.Item1).Micros = reader.ExtractMicrosPerQuarter();
                        break;
                    case ChartEvent.ANCHOR:
                        tempoMarkers.Get_Or_Add_Back(trackEvent.Item1).Anchor = reader.ExtractAnchor();
                        break;
                    case ChartEvent.TIME_SIG:
                        timeSigs.Get_Or_Add_Back(trackEvent.Item1) = reader.ExtractTimeSig();
                        break;
                }
                reader.NextEvent();
            }
        }
    };
}
