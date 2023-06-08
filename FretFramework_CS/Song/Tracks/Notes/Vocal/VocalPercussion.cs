using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes.Vocal
{
    public struct VocalPercussion
    {
        public bool IsPlayable { get; set; }
        public void TogglePlayability() { IsPlayable = !IsPlayable; }
    }
}
