using Framework.Song.Tracks.Notes.Guitar_Pro;
using Framework.Song.Tracks.Notes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Framework.FlatMaps;
using System.Diagnostics;

namespace Framework.Song.Tracks.Instrument.ProGuitarTrack
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class ProGuitarDifficulty<FretType> : DifficultyTrack<Guitar_Pro<FretType>>
        where FretType : unmanaged, IFretted
    {
        public readonly TimedFlatMap<Arpeggio<FretType>> arpeggios = new();

        public override bool IsOccupied() { return !arpeggios.IsEmpty() || base.IsOccupied(); }

        public override void Clear()
        {
            base.Clear();
            arpeggios.Clear();
        }

        public override string GetDebuggerDisplay_Short()
        {
            return $"{notes.Count} | ";
        }

        protected new string GetDebuggerDisplay()
        {
            string str = $"Arpeggios: {arpeggios.Count}";
            if (base.IsOccupied())
                str += "; " + GetDebuggerDisplay_Short();
            return str;
        }
    }
}
