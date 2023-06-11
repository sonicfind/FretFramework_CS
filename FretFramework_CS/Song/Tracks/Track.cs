using Framework.FlatMaps;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public abstract class Track
    {
        public TimedFlatMap<List<SpecialPhrase>> specialPhrases = new();
        public TimedFlatMap<List<string>> events = new();
        public virtual bool IsOccupied() { return !specialPhrases.IsEmpty() || !events.IsEmpty(); }

        public virtual void Clear()
        {
            specialPhrases.Clear();
            events.Clear();
        }
        public abstract void TrimExcess();

        public virtual string GetDebuggerDisplay_Short()
        {
            return $"Phrases: {specialPhrases.Count}; Events: {events.Count};";
        }

        protected string GetDebuggerDisplay()
        {
            return GetDebuggerDisplay_Short();
        }
    }
}
