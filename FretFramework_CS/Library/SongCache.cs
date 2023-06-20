using Framework.Serialization.XboxSTFS;
using Framework.SongEntry.ConEntry;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Library
{
    public class CONEntryGroup
    {
        private readonly FileInfo info;
        private readonly CONFile _file;
        private int dtaIndex = -1;
        private readonly List<CONEntry> entries = new();
        private readonly object entryLock = new();

        public CONFile File { get { return _file!; } }
        public int Count => entries.Count;

        public static bool TryLoadCon(FileInfo info, out CONEntryGroup? group)
        {
            group = null;
            CONFile? file = CONFile.LoadCON(info.FullName);
            if (file == null)
                return false;
            group = new(info, file);
            return true;
        }

        private CONEntryGroup(FileInfo info, CONFile file)
        {
            this.info = info;
            _file = file;
        }

        public void AddEntry(CONEntry entry) { lock (entryLock) entries.Add(entry); }

        internal const string SongsFilePath = "songs/songs.dta";

        public bool LoadSongs(out List<DTAFileNode>? nodes)
        {
            nodes = null;
            dtaIndex = _file.GetFileIndex(SongsFilePath);
            if (dtaIndex == -1)
            {
                Debug.WriteLine("DTA file was not located in CON");
                return false;
            }

            PointerHandler dtaFile = _file.LoadSubFile(dtaIndex)!;
            try
            {
                using DTAFileReader reader = new(dtaFile, true);
                nodes = DTAFileNode.GetNodes(reader);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to parse songs.dta for `{info.FullName}`.");
                Debug.WriteLine(e.Message);
            }
            return true;
        }
    }

    public class SongCache
    {
        private readonly object dirLock = new();
        private readonly object basicLock = new();
        private readonly object CONlock = new();

        private readonly List<CONEntryGroup> conGroups = new();
        private readonly List<SongEntry.SongEntry> basicEntries = new();
        private readonly HashSet<string> preScannedDirectories = new();

        public void AddBasicEntry(SongEntry.SongEntry entry)
        {
            lock (basicLock) basicEntries.Add(entry);
        }

        public void AddConGroup(CONEntryGroup group)
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
