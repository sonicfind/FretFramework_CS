using Framework.FlatMaps;
using Framework.Song.Tracks.Notes.Vocal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Vocals
{
    public abstract class VocalTrack : Track
    {
        public readonly TimedFlatMap<VocalPercussion> percussion = new();

        public abstract TimedFlatMap<Vocal> this[uint trackIndex]
        {
            get;
        }

        public override bool IsOccupied()
        {
            return !percussion.IsEmpty() || base.IsOccupied();
        }
        public override void Clear()
        {
            base.Clear();
            percussion.Clear();
        }

        public override void TrimExcess()
        {
            if ((percussion.Count < 20 || 400 <= percussion.Count) && percussion.Count < percussion.Capacity)
                percussion.TrimExcess();
        }
    }
}
