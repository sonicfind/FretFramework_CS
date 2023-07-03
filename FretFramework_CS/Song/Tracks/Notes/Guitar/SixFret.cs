using Framework.Song.Tracks.Notes.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes.Guitar
{
    public unsafe struct SixFret : INote, IReadableFromDotChart
    {
        private TruncatableSustain open;
        private TruncatableSustain black_1;
        private TruncatableSustain black_2;
        private TruncatableSustain black_3;
        private TruncatableSustain white_1;
        private TruncatableSustain white_2;
        private TruncatableSustain white_3;
        
        public ForceStatus Forcing { get; set; }
        public bool IsTap { get; set; }
        public void ToggleTap() { IsTap = !IsTap; }

        public TruncatableSustain this[uint lane]
        {
            get
            {
                fixed (TruncatableSustain* lanes = &open)
                    return lanes[lane];
            }
        }

        public bool Set(uint lane, ulong length)
        {
            if (lane >= 7)
                return false;

            fixed (TruncatableSustain* lanes = &open)
            {
                lanes[lane].Duration = length;
                if (lane == 0)
                {
                    for (int i = 1; i < 7; ++i)
                        lanes[i].Disable();
                }
                else
                    lanes[0].Disable();
            }
            return true;
        }

        public void Disable(uint lane)
        {
            fixed (TruncatableSustain* lanes = &open)
                lanes[lane].Disable();
        }

        public bool HasActiveNotes()
        {
            fixed (TruncatableSustain* lanes = &open)
                for (uint i = 1; i < 7; ++i)
                    if (lanes[i].IsActive())
                        return true;
            return false;
        }

        public bool IsChorded()
        {
            if (open.IsActive())
                return false;

            int num = 0;
            fixed (TruncatableSustain* lanes = &open)
            {
                for (uint i = 1; i < 7; ++i)
                    if (lanes[i].IsActive())
                        num++;
            }
            return num > 1;
        }

        public bool HasSameFretting(SixFret note)
        {
            if (open.IsActive())
                return note.open.IsActive();

            fixed (TruncatableSustain* lanes = &open)
            {
                for (uint i = 1; i < 7; ++i)
                    if (lanes[i].IsActive() != note[i].IsActive())
                        return false;
            }
            return true;
        }

        // Assumes the current note is NOT a chord
        public bool IsContainedIn(SixFret note)
        {
            fixed (TruncatableSustain* lanes = &open)
            {
                for (uint i = 0; i < 7; ++i)
                    if (lanes[i].IsActive())
                        return note[i].IsActive();
            }
            return false;
        }

        public SixFret() { }

        internal static readonly uint[] SIXFRETLANES = new uint[5] { 4, 5, 6, 1, 2 };
        public bool Set_From_Chart(uint lane, ulong length)
        {
            fixed (TruncatableSustain* lanes = &open)
            {
                if (lane < 5)
                {
                    lanes[SIXFRETLANES[lane]].Duration = length;
                    lanes[0].Disable();
                }
                else if (lane == 8)
                {
                    lanes[3].Duration = length;
                    lanes[0].Disable();
                }
                else if (lane == 5)
                    Forcing = ForceStatus.FORCED_LEGACY;
                else if (lane == 6)
                    IsTap = true;
                else if (lane == 7)
                {
                    lanes[0].Duration = length;
                    for (uint i = 1; i < 7; ++i)
                        lanes[i].Disable();
                }
                else
                    return false;
                return true;
            }
        }

        public new string ToString()
        {
            string display = string.Empty;
            if (open.IsActive())
                display += $"Open: {open.Duration}";
            else
            {
                if (black_1.IsActive())
                    display += $"Black 1: {black_1.Duration}; ";
                if (black_2.IsActive())
                    display += $"Black 2: {black_2.Duration}; ";
                if (black_3.IsActive())
                    display += $"Black 3: {black_3.Duration}; ";
                if (white_1.IsActive())
                    display += $"White 1: {white_1.Duration}; ";
                if (white_2.IsActive()) 
                    display += $"White 2: {white_2.Duration};";
                if (white_3.IsActive())
                    display += $"White 3: {white_3.Duration};";
            }
            return display;
        }
    }
}
