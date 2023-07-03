using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Framework.Song.Tracks.Notes.Interfaces;
using Framework.Types;

namespace Framework.Song.Tracks.Notes.Keys
{
    public unsafe struct Keys : INote, IReadableFromDotChart
    {
        private TruncatableSustain lanes_0;
        private TruncatableSustain lanes_1;
        private TruncatableSustain lanes_2;
        private TruncatableSustain lanes_3;
        private TruncatableSustain lanes_4;
        public ref TruncatableSustain this[uint lane]
        {
            get
            {
                fixed (TruncatableSustain* lanes = &lanes_0)
                    return ref lanes[lane];
            }
        }
        public Keys() { }

        public bool HasActiveNotes()
        {
            fixed (TruncatableSustain* lanes = &lanes_0)
                for (uint lane = 0; lane < 5; lane++)
                    if (lanes[lane].IsActive())
                        return true;
            return false;
        }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane >= 5)
                return false;

            fixed (TruncatableSustain* lanes = &lanes_0)
                lanes[lane].Duration = length;
            return true;
        }
    }
}
