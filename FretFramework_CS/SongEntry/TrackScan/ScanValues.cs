using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan
{
    public unsafe struct ScanValues
    {
        internal static byte[] shifts = { 1, 2, 4, 8, 16 };
        public byte subTracks = 0;
        public sbyte intensity = -1;
        public ScanValues() { }

        public void Set(int subTrack)
        {
            subTracks |= shifts[subTrack];
        }
        public bool this[int subTrack]
        {
            get { return (shifts[subTrack] & subTracks) > 0; }
        }

        public static ScanValues operator |(ScanValues lhs, ScanValues rhs)
        {
            lhs.subTracks |= rhs.subTracks;
            return lhs;
        }
    }
}
