using Framework.FlatMaps;
using Framework.Song.Tracks.Notes.Guitar_Pro;
using Framework.Song.Tracks.Notes.Interfaces;
using Framework.Song.Tracks.Notes.Keys_Pro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument.ProKeysTrack
{
    public enum ProKey_Ranges
    {
        C1_E2,
        D1_F2,
        E1_G2,
        F1_A2,
        G1_B2,
        A1_C3,
    };

    public class ProKeysDifficulty : DifficultyTrack<Keys_Pro>
    {
        public readonly TimedNativeFlatMap<ProKey_Ranges> ranges = new();

        public override bool IsOccupied() { return !ranges.IsEmpty() || base.IsOccupied(); }
        public override void Clear()
        {
            base.Clear();
            ranges.Clear();
        }

        public override string GetDebuggerDisplay_Short()
        {
            return $"{notes.Count} | ";
        }

        protected new string GetDebuggerDisplay()
        {
            string str = $"Ranges: {ranges.Count}";
            if (base.IsOccupied())
                str += "; " + GetDebuggerDisplay_Short();
            return str;
        }
    }
}
