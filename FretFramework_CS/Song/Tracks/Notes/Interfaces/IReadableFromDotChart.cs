using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes.Interfaces
{
    public interface IReadableFromDotChart
    {
        public bool Set_From_Chart(uint lane, ulong length);
    }
}
