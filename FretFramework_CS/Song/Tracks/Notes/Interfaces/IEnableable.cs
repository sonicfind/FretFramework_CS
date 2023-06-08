using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Notes.Interfaces
{
    public interface IEnableable
    {
        public bool IsActive();
        public void Disable();
    }
}
