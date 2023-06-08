using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes.Guitar_Pro
{
    public unsafe struct Arpeggio<FretType>
        where FretType : struct, IFretted
    {
        private FretType string_1 = new();
        private FretType string_2 = new();
        private FretType string_3 = new();
        private FretType string_4 = new();
        private FretType string_5 = new();
        private FretType string_6 = new();
        public ref FretType this[uint lane]
        {
            get
            {
                fixed (FretType* strings = &string_1)
                    return ref strings[lane];
            }
        }
        public NormalizedDuration duration;
        public Arpeggio() { }
    }
}
