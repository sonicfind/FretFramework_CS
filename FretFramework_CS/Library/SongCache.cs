using Framework.Hashes;
using Framework.Serialization;
using Framework.SongEntry;
using Framework.Types;
using System.Diagnostics;
using System.Text;

namespace Framework.Library
{
    public class SongCache
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
        private static readonly object invalidLock = new();

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

        private readonly HashSet<string> invalidSongsInCache = new();

        internal readonly (string, ChartType)[] CHARTTYPES =
        {
            new("notes.mid",   ChartType.MID),
            new("notes.midi",  ChartType.MIDI),
            new("notes.chart", ChartType.CHART),
        };

        internal Encoding UTF8 = Encoding.UTF8;

        private void LoadCacheFile(string cacheFile, List<string> baseDirectories)
        {
            {
                FileInfo info = new(cacheFile);
                if (!info.Exists || info.Length < 28)
                    return;
            }

            using BinaryReader reader = new(new FileStream(cacheFile, FileMode.Open, FileAccess.Read), Encoding.UTF8, false);

            if (reader.ReadInt32() != CACHE_VERSION)
                return;

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(length);
                entryTasks.Add(Task.Run(() => ReadIniEntry(buffer, baseDirectories)));
            }

            List<Task> conTasks = new();
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(length);
                conTasks.Add(Task.Run(() => ReadUpdateDirectory(buffer, baseDirectories)));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(length);
                conTasks.Add(Task.Run(() => ReadUpgradeDirectory(buffer, baseDirectories)));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(length);
                conTasks.Add(Task.Run(() => ReadUpgradeCON(buffer, baseDirectories)));
            }

            Task.WaitAll(conTasks.ToArray());

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(length);
                entryTasks.Add(Task.Run(() => ReadCONGroup(buffer, baseDirectories)));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(length);
                entryTasks.Add(Task.Run(() => ReadExtractedCONGroup(buffer, baseDirectories)));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private static bool StartsWithBaseDirectory(string path, List<string> baseDirectories)
        {
            for (int i = 0; i != baseDirectories.Count; ++i)
                if (path.StartsWith(baseDirectories[i]))
                    return true;
            return false;
        }

        private void ReadIniEntry(byte[] buffer, List<string> baseDirectories)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            string directory = reader.ReadString();
            if (!StartsWithBaseDirectory(directory, baseDirectories))
                return;

            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= CHARTTYPES.Length)
                return;

            ref var chartType = ref CHARTTYPES[chartTypeIndex];
            FileInfo chartFile = new(Path.Combine(directory, chartType.Item1));
            if (!chartFile.Exists)
                return;

            if (chartFile.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return;

            FileInfo? iniFile = null;
            if (reader.ReadBoolean())
            {
                iniFile = new(Path.Combine(directory, "song.ini"));
                if (!iniFile.Exists)
                    return;

                if (iniFile.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
            }

            MarkDirectory(directory);
            IniSongEntry entry = new(directory, chartFile, iniFile, ref chartType, reader);
            SHA1Wrapper hash = new(reader);
            AddEntry(hash, entry);
            AddIniEntry(hash, entry);
        }

        private void ReadUpdateDirectory(byte[] buffer, List<string> baseDirectories)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            string directory = reader.ReadString();
            DateTime dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            if (StartsWithBaseDirectory(directory, baseDirectories))
            {
                FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
                if (dta.Exists)
                {
                    MarkDirectory(directory);
                    UpdateGroupAdd(directory, dta);

                    if (dta.LastWriteTime == dtaLastWrite)
                        return;
                }
            }

            for (int i = 0; i < count; i++)
                AddInvalidSong(reader.ReadString());
        }

        private void AddInvalidSong(string name)
        {
            lock (invalidLock) invalidSongsInCache.Add(name);
        }

        private void ReadUpgradeDirectory(byte[] buffer, List<string> baseDirectories)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader cacheReader = new(ms, Encoding.UTF8, false);

            string directory = cacheReader.ReadString();
            DateTime dtaLastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
            int count = cacheReader.ReadInt32();

            if (StartsWithBaseDirectory(directory, baseDirectories))
            {
                FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
                if (dta.Exists)
                {
                    MarkDirectory(directory);
                    UpgradeGroup? group = UpgradeGroupAdd(directory, dta);

                    if (group != null && dta.LastWriteTime == dtaLastWrite)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string name = cacheReader.ReadString();
                            if (group.upgrades[name].UpgradeLastWrite != DateTime.FromBinary(cacheReader.ReadInt32()))
                                AddInvalidSong(name);
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(cacheReader.ReadString());
                cacheReader.BaseStream.Position += 4;
            }
        }

        private void ReadUpgradeCON(byte[] buffer, List<string> baseDirectories)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader cacheReader = new(ms, Encoding.UTF8, false);

            string filename = cacheReader.ReadString();
            DateTime conLastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
            int dtaIndex = cacheReader.ReadInt32();
            int dtaLastWrite = cacheReader.ReadInt32();
            int count = cacheReader.ReadInt32();

            if (StartsWithBaseDirectory(filename, baseDirectories))
            {
                FileInfo info = new(filename);
                if (info.Exists)
                {
                    MarkFile(filename);

                    CONFile? file = CONFile.LoadCON(info.FullName);
                    if (file == null)
                        goto Invalidate;

                    PackedCONGroup group = new(file, info.LastWriteTime);
                    AddCONGroup(filename, group);

                    if (group.LoadUpgrades(out var reader))
                    {
                        AddCONUpgrades(group, reader!);

                        if (group.UpgradeDTALastWrite == dtaLastWrite)
                        {
                            if (group.lastWrite != conLastWrite)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    string name = cacheReader.ReadString();
                                    if (group.upgrades[name].UpgradeLastWrite != DateTime.FromBinary(cacheReader.ReadInt32()))
                                        AddInvalidSong(name);
                                }
                            }
                            return;
                        }
                    }
                }
            }

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(cacheReader.ReadString());
                cacheReader.BaseStream.Position += 4;
            }
        }

        private void ReadCONGroup(byte[] buffer, List<string> baseDirectories)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            string filename = reader.ReadString();
            if (!StartsWithBaseDirectory(filename, baseDirectories))
                return;

            int dtaLastWrite = reader.ReadInt32();
            if (!FindCONGroup(filename, out PackedCONGroup? group))
            {
                FileInfo info = new(filename);
                if (!info.Exists)
                    return;

                MarkFile(filename);

                CONFile? file = CONFile.LoadCON(info.FullName);
                if (file == null)
                    return;

                group = new(file, info.LastWriteTime);
                AddCONGroup(filename, group);
            }

            if (!group!.SetSongDTA() || group.DTALastWrite != dtaLastWrite)
                return;

            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadString();
                int length = reader.ReadInt32();
                if (invalidSongsInCache.Contains(name))
                {
                    reader.BaseStream.Position += length;
                    continue;
                }
                
                byte[] entryData = reader.ReadBytes(length);
                entryTasks.Add(Task.Run(() => ReadCONEntry(group, name, entryData)));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private bool FindCONGroup(string filename, out PackedCONGroup? group)
        {
            lock (conLock)
                return conGroups.TryGetValue(filename, out group);
        }

        private void AddCONGroup(string filename, PackedCONGroup group)
        {
            lock (conLock) 
                conGroups.Add(filename, group);
        }

        private void ReadCONEntry(PackedCONGroup group, string nodeName, byte[] buffer)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            FileListing? midiListing = group.file[reader.ReadString()];
            if (midiListing == null || midiListing.LastWrite != reader.ReadInt32())
                return;

            FileListing? moggListing = null;
            FileInfo? moggInfo = null;
            if (reader.ReadBoolean())
            {
                moggListing = group.file[reader.ReadString()];
                if (moggListing == null || moggListing.LastWrite != reader.ReadInt32())
                    return;
            }
            else
            {
                moggInfo = new FileInfo(reader.ReadString());
                if (!moggInfo.Exists || moggInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
            }

            FileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = new FileInfo(reader.ReadString());
                if (!updateInfo.Exists || updateInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
            }

            ConSongEntry currentSong = new(group.file, nodeName, midiListing, moggListing, moggInfo, updateInfo, reader);
            SHA1Wrapper hash = new(reader);
            group.AddEntry(nodeName, currentSong, hash);
        }

        private void ReadExtractedCONGroup(byte[] buffer, List<string> baseDirectories)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            string directory = reader.ReadString();
            if (!StartsWithBaseDirectory(directory, baseDirectories))
                return;

            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists)
                return;

            MarkDirectory(directory);
            ExtractedConGroup group = new(dtaInfo);
            AddExtractedCONGroup(directory, group);

            if (dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return;

            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadString();
                int length = reader.ReadInt32();

                if (invalidSongsInCache.Contains(name))
                {
                    reader.BaseStream.Position += length;
                    continue;
                }
                
                byte[] entryData = reader.ReadBytes(length);
                entryTasks.Add(Task.Run(() => ReadExtractedCONEntry(group, name, entryData)));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void AddExtractedCONGroup(string directory, ExtractedConGroup group)
        {
            lock (extractedLock)
                extractedConGroups.Add(directory, group);
        }

        private static void ReadExtractedCONEntry(ExtractedConGroup group, string nodeName, byte[] buffer)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            FileInfo midiInfo = new(reader.ReadString());
            if (!midiInfo.Exists || midiInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return;

            FileInfo moggInfo = new(reader.ReadString());
            if (!moggInfo.Exists || moggInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return;

            FileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = new FileInfo(reader.ReadString());
                if (!updateInfo.Exists || updateInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
            }

            ConSongEntry currentSong = new(nodeName, midiInfo, moggInfo, updateInfo, reader);
            SHA1Wrapper hash = new(reader);
            group.AddEntry(nodeName, currentSong, hash);
        }

        private void ScanDirectory(DirectoryInfo directory)
        {
            if (!FindOrMarkDirectory(directory.FullName))
                return;

            FileInfo?[] charts = new FileInfo?[3];
            FileInfo? ini = null;
            List<DirectoryInfo> subDirectories = new();
            DirectoryInfo? songs = null;

            List<FileInfo> files = new();

            try
            {
                foreach (FileSystemInfo info in directory.EnumerateFileSystemInfos())
                {
                    string filename = info.Name.ToLower();
                    if ((info.Attributes & FileAttributes.Directory) > 0)
                    {
                        DirectoryInfo dir = (info as DirectoryInfo)!;
                        if (filename == "songs_updates")
                            AddUpdateDirectory(dir.FullName);
                        else if (filename == "song_upgrades")
                            AddUpgradeDirectory(dir.FullName);
                        else if (filename == "songs")
                            songs = dir;
                        else
                            subDirectories.Add((info as DirectoryInfo)!);
                        continue;
                    }

                    FileInfo file = (info as FileInfo)!;
                    if (filename == "song.ini")
                    {
                        ini = file;
                        continue;
                    }

                    bool found = false;
                    for (int i = 0; i < 3; ++i)
                    {
                        if (filename == CHARTTYPES[i].Item1)
                        {
                            charts[i] = file;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        files.Add(file);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(directory.FullName);
                return;
            }

            if (ini == null)
            {
                charts[0] = null;
                charts[1] = null;
            }

            if (ScanIniEntry(charts, ini))
                return;

            if (songs != null && AddExtractedCONDirectory(songs.FullName))
                return;

            Parallel.For(0, files.Count, i => AddPossibleCON(files[i]));
            Parallel.For(0, subDirectories.Count, i => ScanDirectory(subDirectories[i]));
        }

        private bool ScanIniEntry(FileInfo?[] charts, FileInfo? ini)
        {
            for (int i = 0; i < 3; ++i)
            {
                var chart = charts[i];
                if (chart != null)
                {
                    try
                    {
                        using FrameworkFile_Alloc file = new(chart.FullName);
                        IniSongEntry entry = new(file, chart, ini, ref CHARTTYPES[i]);
                        if (entry.ScannedSuccessfully())
                        {
                            SHA1Wrapper hash = new(file.CalcSHA1());
                            if (AddEntry(hash, entry))
                                AddIniEntry(hash, entry);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(chart.FullName);
                    }
                    return true;
                }
            }
            return false;
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

        private void AddUpdateDirectory(string directory)
        {
            if (!FindOrMarkDirectory(directory))
                return;

            FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
            if (!dta.Exists)
                return;

            UpdateGroupAdd(directory, dta);
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

        private void AddUpgradeDirectory(string directory)
        {
            if (!FindOrMarkDirectory(directory))
                return;

            FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
            if (!dta.Exists)
                return;

            UpgradeGroupAdd(directory, dta, true);
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

        private void AddPossibleCON(FileInfo info)
        {
            if (!FindOrMarkFile(info.FullName))
                return;

            CONFile? file = CONFile.LoadCON(info.FullName);
            if (file == null)
                return;

            PackedCONGroup group = new(file, info.LastWriteTime);
            AddCONGroup(info.FullName, group);

            if (group.LoadUpgrades(out var reader))
                AddCONUpgrades(group, reader!);
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

        private bool AddExtractedCONDirectory(string dir)
        {
            if (!FindOrMarkDirectory(dir))
                return false;

            FileInfo dta = new(Path.Combine(dir, "songs.dta"));
            if (!dta.Exists)
                return false;

            AddExtractedCONGroup(dir, new(dta));
            return true;
        }

        private void LoadCONSongs()
        {
            Parallel.ForEach(conGroups, node => {
                var group = node.Value;
                if (group.LoadSongs(out var reader))
                {
                    while (reader!.StartNode())
                    {
                        string name = reader.GetNameOfNode();
                        if (group.TryGetEntry(name, out var entryNode))
                        {
                            if (!AddEntry(entryNode!.hash, entryNode.entry))
                                group.RemoveEntry(name);
                        }
                        else
                        {
                            try
                            {
                                ConSongEntry currentSong = new(group.file, name, reader);
                                if (ProcessCONEntry(name, currentSong, out SHA1Wrapper? hash))
                                {
                                    if (AddEntry(hash!, currentSong))
                                        group.AddEntry(name, currentSong, hash!);
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine($"CON: DTA Failed to parse song '{name}'. Skipping all further songs in file...");
                                Debug.WriteLine(e.Message);
                                break;
                            }
                        }
                        reader.EndNode();
                    }
                }
            });
        }

        private void LoadExtractedCONSongs()
        {
            Parallel.ForEach(extractedConGroups, node =>
            {
                string directory = node.Key;
                ExtractedConGroup group = node.Value;
                DTAFileReader? reader = group.LoadDTA();
                if (reader == null)
                    return;

                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    if (group.TryGetEntry(name, out var entryNode))
                    {
                        if (!AddEntry(entryNode!.hash, entryNode.entry))
                            group.RemoveEntry(name);
                    }
                    else
                    {
                        try
                        {
                            ConSongEntry currentSong = new(directory, name, reader);
                            if (ProcessCONEntry(name, currentSong, out SHA1Wrapper? hash))
                            {
                                if (AddEntry(hash!, currentSong))
                                    group.AddEntry(name, currentSong, hash!);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine($"CON: DTA Failed to parse song '{name}'. Skipping all further songs in file...");
                            Debug.WriteLine(e.Message);
                            break;
                        }
                    }
                    reader.EndNode();
                }
            });
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

        private void FinalizeIniEntries()
        {
            foreach (var entryList in iniEntries)
                foreach (var entry in entryList.Value)
                    entry.FinishScan();
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

        private void MarkDirectory(string directory)
        {
            lock (dirLock) preScannedDirectories.Add(directory);
        }

        private bool FindOrMarkDirectory(string directory)
        {
            lock (dirLock)
            {
                if (preScannedDirectories.Contains(directory))
                    return false;

                preScannedDirectories.Add(directory);
                return true;
            }
        }

        private void MarkFile(string file)
        {
            lock (fileLock) preScannedFiles.Add(file);
        }

        private bool FindOrMarkFile(string file)
        {
            lock (fileLock)
            {
                if (preScannedFiles.Contains(file))
                    return false;

                preScannedFiles.Add(file);
                return true;
            }
        }

        private void SaveToFile(string fileName)
        {
            using var writer = new BinaryWriter(new FileStream(fileName, FileMode.Create, FileAccess.Write), Encoding.UTF8, false);

            writer.Write(CACHE_VERSION);
            writer.Write(iniEntries.Count);
            foreach (var entryList in iniEntries)
            {
                foreach (var entry in entryList.Value)
                {
                    byte[] buffer = entry.FormatCacheData();
                    writer.Write(buffer.Length + 20);
                    writer.Write(buffer);
                    entryList.Key.Write(writer);
                }
            }

            writer.Write(updateGroups.Count);
            foreach (var group in updateGroups)
            {
                byte[] buffer = group.FormatForCache();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(upgradeGroups.Count);
            foreach (var group in upgradeGroups)
            {
                byte[] buffer = group.FormatForCache();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            List<KeyValuePair<string, PackedCONGroup>> upgradeCons = new();
            List<KeyValuePair<string, PackedCONGroup>> entryCons = new();
            foreach (var group in conGroups)
            {
                if (group.Value.UpgradeCount > 0)
                    upgradeCons.Add(group);

                if (group.Value.EntryCount > 0)
                    entryCons.Add(group);
            }

            writer.Write(upgradeCons.Count);
            foreach (var group in upgradeCons)
            {
                byte[] buffer = group.Value.FormatUpgradesForCache(group.Key);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(entryCons.Count);
            foreach (var group in entryCons)
            {
                byte[] buffer = group.Value.FormatEntriesForCache(group.Key);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(extractedConGroups.Count);
            foreach (var group in extractedConGroups)
            {
                byte[] buffer = group.Value.FormatEntriesForCache(group.Key);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }
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
