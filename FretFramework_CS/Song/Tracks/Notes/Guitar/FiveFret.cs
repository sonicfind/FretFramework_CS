using Framework.Song.Tracks.Notes.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes.Guitar
{
    
    [DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
    public unsafe struct FiveFret : INote, IReadableFromDotChart
    {
        private TruncatableSustain open;
        private TruncatableSustain green;
        private TruncatableSustain red;
        private TruncatableSustain yellow;
        private TruncatableSustain blue;
        private TruncatableSustain orange;
        public ForceStatus Forcing { get; set; }
        public bool IsTap { get; set; }
        public void ToggleTap() { IsTap = !IsTap; }

        public TruncatableSustain this[uint lane]
        {
            get
            {
                if (lane > 5)
                    throw new ArgumentOutOfRangeException("lane");

                fixed (TruncatableSustain* lanes = &open)
                    return lanes[lane];
            }
        }

        public bool Set(uint lane, ulong length)
        {
            if (lane >= 6)
                return false;

            fixed (TruncatableSustain* lanes = &open)
            {
                lanes[lane].Duration = length;
                if (lane == 0)
                {
                    for (int i = 1; i < 6; ++i)
                        lanes[i].Disable();
                }
                else
                    lanes[0].Disable();
            }
            return true;
        }

        public void Disable(uint lane)
        {
            if (lane > 5)
                throw new ArgumentOutOfRangeException("lane");

            fixed (TruncatableSustain* lanes = &open)
                lanes[lane].Disable();
        }

        public bool HasActiveNotes()
        {
            fixed (TruncatableSustain* lanes = &open)
                for (uint lane = 0; lane < 6; lane++)
                    if (lanes[lane].IsActive())
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
                for (int i = 1; i < 6; ++i)
                    if (lanes[i].IsActive())
                        num++;
            }
            return num > 1;
        }

        public bool HasSameFretting(FiveFret note)
        {
            if (open.IsActive())
                return note.open.IsActive();

            fixed (TruncatableSustain* lanes = &open)
            {
                for (uint i = 1; i < 6; ++i)
                    if (lanes[i].IsActive() != note[i].IsActive())
                        return false;
            }
            return true;
        }

        // Assumes the current note is NOT a chord
        public bool IsContainedIn(FiveFret note)
        {
            fixed (TruncatableSustain* lanes = &open)
                for (uint i = 0; i < 6; ++i)
                    if (lanes[i].IsActive())
                        return note[i].IsActive();
            return false;
        }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane < 5)
            {
                fixed (TruncatableSustain* lanes = &open)
                    lanes[lane + 1].Duration = length;
                open.Disable();
            }
            else if (lane == 5)
                Forcing = ForceStatus.FORCED_LEGACY;
            else if (lane == 6)
                IsTap = true;
            else if (lane == 7)
            {
                open.Duration = length;
                fixed (TruncatableSustain* lanes = &open)
                    for (uint i = 1; i < 6; ++i)
                        lanes[i].Disable();
            }
            else
                return false;
            return true;
        }

        public FiveFret() { }

        public new string ToString()
        {
            string display = string.Empty;
            if (open.IsActive())
                display += $"Open: {open.Duration}";
            else
            {
                if (green.IsActive())
                    display += $"Green: {green.Duration}; ";
                if (red.IsActive())
                    display += $"Red: {red.Duration}; ";
                if (yellow.IsActive())
                    display += $"Yellow: {yellow.Duration}; ";
                if (blue.IsActive())
                    display += $"Blue: {blue.Duration}; ";
                if (orange.IsActive())
                    display += $"Orange: {orange.Duration};";
            }
            return display;
        }
    }
}
