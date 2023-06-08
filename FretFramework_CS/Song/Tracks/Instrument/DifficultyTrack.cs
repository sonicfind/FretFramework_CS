using Framework.FlatMaps;
using Framework.Song.Tracks.Notes;
using Framework.Song.Tracks.Notes.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class DifficultyTrack<T> : Track
        where T : struct, INote 
    {
        public readonly TimedFlatMap<T> notes = new();

        public override bool IsOccupied() { return !notes.IsEmpty() || base.IsOccupied(); }

        public override void Clear()
        {
            base.Clear();
            notes.Clear();
        }
        public override void TrimExcess() => notes.TrimExcess();

        public override string GetDebuggerDisplay_Short()
        {
            return $"{notes.Count} | ";
        }

        protected new string GetDebuggerDisplay()
        {
            string str = $"Notes: {notes.Count}";
            if (base.IsOccupied())
                str += "; " + base.GetDebuggerDisplay_Short();
            return str;
        }
    }
}
