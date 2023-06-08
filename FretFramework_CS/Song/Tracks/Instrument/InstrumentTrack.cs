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
    public class InstrumentTrack_Base<Difficulty> : Track
        where Difficulty : Track, new()
    {
        protected readonly Difficulty[] difficulties = new Difficulty[4] { new(), new(), new(), new(), };
        public override bool IsOccupied()
        {
            foreach (var difficulty in difficulties)
                if (difficulty.IsOccupied())
                    return true;
            return base.IsOccupied();
        }
        public override void Clear()
        {
            base.Clear();
            foreach (var difficulty in difficulties)
                difficulty.Clear();
        }
        public override void TrimExcess()
        {
            foreach (var difficulty in difficulties)
                difficulty.TrimExcess();
        }
        public ref Difficulty this[uint index] { get { return ref difficulties[index]; } }

        protected new string GetDebuggerDisplay()
        {
            string str = string.Empty;
            if (difficulties[3].IsOccupied())
                str += "Expert: " + difficulties[3].GetDebuggerDisplay_Short();
            if (difficulties[2].IsOccupied())
                str += "Hard: " + difficulties[2].GetDebuggerDisplay_Short();
            if (difficulties[1].IsOccupied())
                str += "Medium: " + difficulties[1].GetDebuggerDisplay_Short();
            if (difficulties[0].IsOccupied())
                str += "Easy: " + difficulties[0].GetDebuggerDisplay_Short();
            return str;
        }

    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class InstrumentTrack<T> : InstrumentTrack_Base<DifficultyTrack<T>>
        where T : struct, INote
    {
        protected new string GetDebuggerDisplay()
        {
            string str = string.Empty;
            if (difficulties[3].IsOccupied())
                str += "Expert: " + difficulties[3].GetDebuggerDisplay_Short();
            if (difficulties[2].IsOccupied())
                str += "Hard: " + difficulties[2].GetDebuggerDisplay_Short();
            if (difficulties[1].IsOccupied())
                str += "Medium: " + difficulties[1].GetDebuggerDisplay_Short();
            if (difficulties[0].IsOccupied())
                str += "Easy: " + difficulties[0].GetDebuggerDisplay_Short();
            return str;
        }
    }
}
