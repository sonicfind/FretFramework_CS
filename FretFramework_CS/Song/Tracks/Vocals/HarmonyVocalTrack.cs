using Framework.FlatMaps;
using Framework.Song.Tracks.Notes.Vocal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Vocals
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class HarmonyVocalTrack : VocalTrack
    {
        public readonly TimedFlatMap<Vocal>[] vocals;

        public override TimedFlatMap<Vocal> this[uint trackIndex]
        {
            get { return vocals[trackIndex]; }
        }

        public HarmonyVocalTrack(uint numTracks)
        {
            vocals = new TimedFlatMap<Vocal>[numTracks];
            for (uint i = 0; i < numTracks; i++)
                vocals[i] = new();
        }

        public override bool IsOccupied()
        {
            foreach (var track in vocals)
                if (!track.IsEmpty())
                    return true;
            return base.IsOccupied();
        }
        public override void Clear()
        {
            base.Clear();
            foreach (var track in vocals)
                track.Clear();
        }
        public override void TrimExcess()
        {
            foreach (var track in vocals)
                if ((track.Count < 100 || 2000 <= track.Count) && track.Count < track.Capacity)
                    track.TrimExcess();
        }
        private new string GetDebuggerDisplay()
        {
            if (IsOccupied())
            {
                string str = string.Empty;
                for (uint i = 0; i < vocals.Length; ++i)
                    str += $"Track {i}: {vocals[i].Count} | ";
                return str + base.GetDebuggerDisplay();
            }
            return string.Empty;
        }
    }
}
