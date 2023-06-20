using Framework.Library;
using Framework.Serialization.XboxSTFS;
using Framework.SongEntry.ConEntry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Library
{
    public class CONGroup
    {
        public CONFile File { get; init; }
        private readonly List<CONEntry> entries = new();
        private readonly object entryLock = new();
        public int Count => entries.Count;

        public CONGroup(CONFile file) { File = file; }
        public void AddEntry(CONEntry entry) { lock (entryLock) entries.Add(entry); }
    }

    public class SongCache
    {
        private readonly object dirLock = new();
        private readonly object basicLock = new();
        private readonly object CONlock = new();

        private readonly List<CONGroup> conGroups = new();
        private readonly List<SongEntry.SongEntry> basicEntries = new();
        private readonly HashSet<string> preScannedDirectories = new();

        public void AddBasicEntry(SongEntry.SongEntry entry)
        {
            lock (basicLock) basicEntries.Add(entry);
        }

        public void AddConGroup(CONGroup group)
        {
            lock (CONlock) conGroups.Add(group);
        }

        public void MarkDirectory(string directory)
        {
            lock (dirLock) preScannedDirectories.Add(directory);
        }

        public bool FindOrMarkDirectory(string directory)
        {
            lock (dirLock)
            {
                if (preScannedDirectories.Contains(directory))
                    return false;

                preScannedDirectories.Add(directory);
                return true;
            }
        }
    }
}
