using Framework.Hashes;
using Framework.Serialization;
using Framework.SongEntry;
using Framework.Types;
using System.Diagnostics;
using System.Text;

namespace Framework.Library
{
    public partial class SongCache
    {
        public static SongLibrary ScanDirectories(List<string> baseDirectories, string cacheFileDirectory, bool writeCache)
        {
            string cacheFile = Path.Combine(cacheFileDirectory, "songcache_CS.bin");
            SongCache cache = new();
            cache.LoadCacheFile(cacheFile, baseDirectories);
            Parallel.For(0, baseDirectories.Count, i => cache!.ScanDirectory(new(baseDirectories[i])));
            Task cons = Task.Run(cache.LoadCONSongs);
            Task extractedCons = Task.Run(cache.LoadExtractedCONSongs);
            Task.WaitAll(cons, extractedCons);
            cache.FinalizeIniEntries();

            if (writeCache)
                cache.SaveToFile(cacheFile);

            return cache.library;
        }

        private const int CACHE_VERSION = 23_06_28_02;
        private static readonly object dirLock = new();
        private static readonly object fileLock = new();
        private static readonly object iniLock = new();
        private static readonly object conLock = new();
        private static readonly object extractedLock = new();
        private static readonly object updateLock = new();
        private static readonly object upgradeLock = new();
        private static readonly object updateGroupLock = new();
        private static readonly object upgradeGroupLock = new();
        private static readonly object entryLock = new();

        static SongCache() { }

        private readonly List<UpdateGroup> updateGroups = new();
        private readonly Dictionary<string, List<(string, DTAFileReader)>> updates = new();
        private readonly List<UpgradeGroup> upgradeGroups = new();
        
        private readonly Dictionary<string, PackedCONGroup> conGroups = new();
        private readonly Dictionary<string, (DTAFileReader, SongProUpgrade)> upgrades = new();
        private readonly Dictionary<string, ExtractedConGroup> extractedConGroups = new();
        private readonly Dictionary<SHA1Wrapper, List<IniSongEntry>> iniEntries = new();

        private readonly SongLibrary library = new();
        private readonly HashSet<string> preScannedDirectories = new();
        private readonly HashSet<string> preScannedFiles = new();

        internal readonly (string, ChartType)[] CHARTTYPES =
        {
            new("notes.mid",   ChartType.MID),
            new("notes.midi",  ChartType.MIDI),
            new("notes.chart", ChartType.CHART),
        };

        private void AddCONGroup(string filename, PackedCONGroup group)
        {
            lock (conLock) 
                conGroups.Add(filename, group);
        }

        private void AddExtractedCONGroup(string directory, ExtractedConGroup group)
        {
            lock (extractedLock)
                extractedConGroups.Add(directory, group);
        }

        private void AddIniEntry(SHA1Wrapper hash, IniSongEntry entry)
        {
            lock (iniLock)
            {
                if (iniEntries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    iniEntries.Add(hash, new() { entry });
            }
        }

        private void UpdateGroupAdd(string directory, FileInfo dta, bool removeEntries = false)
        {
            DTAFileReader reader = new(dta.FullName);
            UpdateGroup group = new(directory, dta.LastWriteTime);
            while (reader!.StartNode())
            {
                string name = reader.GetNameOfNode();
                group!.updates.Add(name);

                (string, DTAFileReader) node = new(directory, reader.Clone());
                lock (updateLock)
                {
                    if (updates.TryGetValue(name, out var list))
                        list.Add(node);
                    else
                        updates[name] = new() { node };
                }

                if (removeEntries)
                    RemoveCONEntry(name);
                reader.EndNode();
            }

            if (group!.updates.Count > 0)
                lock (updateGroupLock)
                    updateGroups.Add(group);
        }

        private UpgradeGroup? UpgradeGroupAdd(string directory, FileInfo dta, bool removeEntries = false)
        {
            DTAFileReader reader = new(dta.FullName);
            UpgradeGroup group = new(directory, dta.LastWriteTime);
            while (reader!.StartNode())
            {
                string name = reader.GetNameOfNode();
                FileInfo file = new(Path.Combine(directory, $"{name}_plus.mid"));
                if (file.Exists)
                {
                    DateTime lastWrite = file.LastWriteTime;
                    if (CanAddUpgrade(name, ref lastWrite))
                    {
                        SongProUpgrade upgrade = new(file);
                        group!.upgrades[name] = upgrade;
                        AddUpgrade(name, reader.Clone(), upgrade);

                        if (removeEntries)
                            RemoveCONEntry(name);
                    }
                }

                reader.EndNode();
            }

            if (group.upgrades.Count > 0)
            {
                lock (upgradeGroupLock)
                    upgradeGroups.Add(group);
                return group;
            }
            return null;
        }

        private void AddUpgrade(string name, DTAFileReader reader, SongProUpgrade upgrade)
        {
            lock (upgradeLock)
                upgrades[name] = new(reader, upgrade);
        }

        private void AddCONUpgrades(PackedCONGroup group, DTAFileReader reader)
        {
            CONFile file = group.file;
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                FileListing? listing = file[$"songs_upgrades/{name}_plus.mid"];

                if (listing != null)
                {
                    DateTime lastWrite = DateTime.FromBinary(listing.LastWrite);
                    if (CanAddUpgrade_CONInclusive(name, ref lastWrite))
                    {
                        SongProUpgrade upgrade = new(file, listing, lastWrite);
                        group.upgrades[name] = upgrade;
                        AddUpgrade(name, reader.Clone(), upgrade);
                        RemoveCONEntry(name);
                    }
                }

                reader.EndNode();
            }
        }

        private bool ProcessCONEntry(string name, ConSongEntry currentSong, out SHA1Wrapper? hash)
        {
            if (updates.TryGetValue(name, out var updateList))
            {
                foreach (var update in updateList!)
                    currentSong.Update(update.Item1, name, update.Item2.Clone());
            }

            if (upgrades.TryGetValue(name, out var upgrade))
            {
                currentSong.Upgrade = upgrade.Item2;
                currentSong.SetFromDTA(name, upgrade.Item1.Clone());
            }

            return currentSong.Scan(out hash, name);
        }

        private bool CanAddUpgrade(string shortname, ref DateTime lastWrite)
        {
            lock (upgradeLock)
            {
                foreach (var group in upgradeGroups)
                {
                    if (group.upgrades.TryGetValue(shortname, out var currUpgrade))
                    {
                        if (currUpgrade.UpgradeLastWrite >= lastWrite)
                            return false;
                        group.upgrades.Remove(shortname);
                        break;
                    }
                }
            }
            return true;
        }

        private bool CanAddUpgrade_CONInclusive(string shortname, ref DateTime lastWrite)
        {
            lock (conLock)
            {
                foreach (var group in conGroups)
                {
                    var upgrades = group.Value.upgrades;
                    if (upgrades.TryGetValue(shortname, out SongProUpgrade? currUpgrade))
                    {
                        if (currUpgrade!.UpgradeLastWrite >= lastWrite)
                            return false;
                        upgrades.Remove(shortname);
                        return true;
                    }
                }
            }

            return CanAddUpgrade(shortname, ref lastWrite);
        }

        private void RemoveCONEntry(string shortname)
        {
            lock (conLock)
            {
                List<string> entriesToRemove = new();
                foreach (var group in conGroups)
                {
                    group.Value.RemoveEntry(shortname);
                    if (group.Value.EntryCount == 0)
                        entriesToRemove.Add(group.Key);
                }

                for (int i = 0; i < entriesToRemove.Count; i++)
                    conGroups.Remove(entriesToRemove[i]);
            }

            lock (extractedLock)
            {
                List<string> entriesToRemove = new();
                foreach (var group in extractedConGroups)
                {
                    group.Value.RemoveEntry(shortname);
                    if (group.Value.EntryCount == 0)
                        entriesToRemove.Add(group.Key);
                }

                for (int i = 0; i < entriesToRemove.Count; i++)
                    extractedConGroups.Remove(entriesToRemove[i]);
            }
        }

        private bool AddEntry(SHA1Wrapper hash, SongEntry.SongEntry entry)
        {
            lock (entryLock)
            {
                if (library.entries.TryGetValue(hash, out List<SongEntry.SongEntry>? list))
                    list.Add(entry);
                else
                    library.entries.Add(hash, new() { entry });
            }
            return true;
        }
    }

    internal abstract class CONGroup
    {
        public class EntryNode
        {
            public readonly ConSongEntry entry;
            public readonly SHA1Wrapper hash;
            public EntryNode(ConSongEntry entry, SHA1Wrapper hash)
            {
                this.entry = entry;
                this.hash = hash;
            }
        }

        protected readonly Dictionary<string, EntryNode> entries = new();
        protected readonly object entryLock = new();
        public int EntryCount => entries.Count;
        public void AddEntry(string name, ConSongEntry entry, SHA1Wrapper hash) { lock (entryLock) entries.Add(name, new(entry, hash)); }

        public void RemoveEntry(string name) { lock (entryLock) entries.Remove(name); }

        public bool TryGetEntry(string name, out EntryNode? entry) { return entries.TryGetValue(name, out entry); }

        protected void WriteEntriesToCache(BinaryWriter writer)
        {
            writer.Write(entries.Count);
            foreach (var entry in entries)
            {
                writer.Write(entry.Key);

                byte[] data = entry.Value.entry.FormatCacheData();
                writer.Write(data.Length + 20);
                writer.Write(data);

                entry.Value.hash.Write(writer);
            }
        }
    }

    internal class PackedCONGroup : CONGroup
    {
        public readonly CONFile file;
        public readonly DateTime lastWrite;
        public readonly Dictionary<string, SongProUpgrade> upgrades = new();
        
        public int UpgradeCount => upgrades.Count;
        
        private readonly object upgradeLock = new();

        private FileListing? songDTA;
        private FileListing? upgradeDta;
        public int DTALastWrite
        {
            get
            {
                if (songDTA == null)
                    return 0;
                return songDTA.LastWrite;
            }
        }
        public int UpgradeDTALastWrite
        {
            get
            {
                if (upgradeDta == null)
                    return 0;
                return upgradeDta.LastWrite;
            }
        }

        public PackedCONGroup(CONFile file, DateTime lastWrite)
        {
            this.file = file;
            this.lastWrite = lastWrite;
        }

        public void AddUpgrade(string name, SongProUpgrade upgrade) { lock (upgradeLock) upgrades[name] = upgrade; }

        internal const string SongsFilePath = "songs/songs.dta";
        internal const string UpgradesFilePath = "songs_upgrades/upgrades.dta";

        public bool LoadUpgrades(out DTAFileReader? reader)
        {
            upgradeDta = file[UpgradesFilePath];
            if (upgradeDta == null)
            {
                reader = null;
                return false;
            }

            reader = new(file.LoadSubFile(upgradeDta!)!, true);
            return true;
        }

        public bool LoadSongs(out DTAFileReader? reader)
        {
            if (songDTA == null && !SetSongDTA())
            {
                reader = null;
                return false;
            }

            reader = new(file.LoadSubFile(songDTA!)!, true);
            return true;
        }

        public bool SetSongDTA()
        {
            songDTA = file[SongsFilePath];
            return songDTA != null;
        }

        public byte[] FormatUpgradesForCache(string filepath)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(filepath);
            writer.Write(lastWrite.ToBinary());
            writer.Write(upgradeDta!.LastWrite);
            writer.Write(upgrades.Count);
            foreach (var upgrade in upgrades)
            {
                writer.Write(upgrade.Key);
                upgrade.Value.WriteToCache(writer);
            }
            return ms.ToArray();
        }

        public byte[] FormatEntriesForCache(string filepath)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(filepath);
            writer.Write(songDTA!.LastWrite);
            WriteEntriesToCache(writer);
            return ms.ToArray();
        }
    }

    internal class ExtractedConGroup : CONGroup
    {
        private readonly FileInfo info;

        public ExtractedConGroup(FileInfo info)
        {
            this.info = info;
        }

        public DTAFileReader? LoadDTA()
        {
            try
            {
                return new(info.FullName);
            }
            catch
            {
                return null;
            }
        }

        public byte[] FormatEntriesForCache(string directory)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(info.LastWriteTime.ToBinary());
            WriteEntriesToCache(writer);
            return ms.ToArray();
        }
    }

    internal class UpdateGroup
    {
        public readonly string directory;
        private readonly DateTime dtaLastWrite;
        public readonly List<string> updates = new();

        public UpdateGroup(string directory, DateTime dtaLastWrite)
        {
            this.directory = directory;
            this.dtaLastWrite = dtaLastWrite;
        }

        public byte[] FormatForCache()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(dtaLastWrite.ToBinary());
            writer.Write(updates.Count);
            for (int i = 0; i < updates.Count; ++i)
                writer.Write(updates[i]);
            return ms.ToArray();
        }
    }

    internal class UpgradeGroup
    {
        public readonly string directory;
        private readonly DateTime dtaLastWrite;
        public readonly Dictionary<string, SongProUpgrade> upgrades = new();

        public UpgradeGroup(string directory, DateTime dtaLastWrite)
        {
            this.directory = directory;
            this.dtaLastWrite = dtaLastWrite;
        }

        public byte[] FormatForCache()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(dtaLastWrite.ToBinary());
            writer.Write(upgrades.Count);
            foreach (var upgrade in upgrades)
            {
                writer.Write(upgrade.Key);
                upgrade.Value.WriteToCache(writer);
            }
            return ms.ToArray();
        }
    }
}
