using Framework.Song.Tracks.Notes.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes
{
    public struct TruncatableSustain : IEnableable
    {
        public static ulong MinDuration { get; set; } = 180;
        private ulong _duration;
        public ulong Duration
        {
            get { return _duration; }
            set
            {
                if (value < MinDuration)
                    value = 1;
                _duration = value;
            }
        }
        public TruncatableSustain() { }
        public TruncatableSustain(ulong duration) { Duration = duration; }

        static public implicit operator ulong(TruncatableSustain dur) => dur._duration;
        static public implicit operator TruncatableSustain(ulong dur) => new(dur);

        public bool IsActive() { return _duration > 0; }
        public void Disable() { _duration = 0; }
    }
}
