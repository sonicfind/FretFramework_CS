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
    public class LeadVocalTrack : VocalTrack
    {
        public readonly TimedFlatMap<Vocal> vocals = new();

        public override TimedFlatMap<Vocal> this[int trackIndex]
        {
            get
            {
                if (trackIndex != 0)
                    throw new ArgumentOutOfRangeException(nameof(trackIndex));
                return vocals;
            }
        }

        public override bool IsOccupied()
        {
            return !vocals.IsEmpty() || base.IsOccupied();
        }
        public override void Clear()
        {
            base.Clear();
            vocals.Clear();
        }
        public override void TrimExcess()
        {
            if ((vocals.Count < 100 || 2000 <= vocals.Count) && vocals.Count < vocals.Capacity)
                vocals.TrimExcess();
        }

        private new string GetDebuggerDisplay()
        {
            if (IsOccupied())
                return $"Vocals: {vocals.Count}; " + base.GetDebuggerDisplay();
            return string.Empty;
        }
    }
}
