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
        public readonly TimedFlatMap<Tempo> tempoMarkers = new();
        public readonly TimedFlatMap<TimeSig> timeSigs = new();
        public SyncTrack() {}

        public void AddFromMidi(ref MidiFileReader reader)
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
    };
}
