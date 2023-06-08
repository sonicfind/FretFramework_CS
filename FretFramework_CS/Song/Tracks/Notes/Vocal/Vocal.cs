using Framework.Song.Tracks.Notes.Interfaces;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes.Vocal
{
    public struct Vocal
    {
        public string lyric = string.Empty;
        public Pitch pitch = new(2, 6);
        public NormalizedDuration duration;

        public bool IsPlayable() { return lyric.Length > 0 && (pitch.Octave > -1 || lyric[0] == '#'); }

        public Vocal() { }
        public Vocal(string lyric) { this.lyric = lyric; }
    }


}
