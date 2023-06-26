using Framework.Hashes;
using Framework.Serialization;
using Framework.SongEntry;
using Framework.SongEntry.CONProUpgrades;
using Framework.Types;
using System.Diagnostics;
using System.Text;

namespace Framework.Library
{
    public class SongCache
    {
        public static SongLibrary ScanDirectories(List<string> baseDirectories, bool writeCache)
        {
            SongCache cache = new();
            Parallel.For(0, baseDirectories.Count, i => cache!.ScanDirectory(new(baseDirectories[i])));
            Task cons = Task.Run(cache.LoadCONSongs);
            Task extractedCons = Task.Run(cache.LoadExtractedCONSongs);
            Task.WaitAll(cons, extractedCons);
            cache.FinalizeIniEntries();

            if (writeCache)
                cache.SaveToFile("songcache_CS.bin");

            return cache.library;
        }

        private const int CACHE_VERSION = 23_06_25_01;
        private readonly object dirLock = new();
        private readonly object iniLock = new();
        private readonly object CONlock = new();
        private readonly object updatelock = new();
        private readonly object upgradelock = new();
        private readonly object entryLock = new();

        private readonly List<UpdateGroup> updateGroups = new();
        private readonly Dictionary<string, List<(string, DTAFileReader)>> updates = new();
        private readonly List<UpgradeGroup> upgradeGroups = new();
        
        private readonly List<CONGroup> conGroups = new();
        private readonly Dictionary<string, (DTAFileReader, SongProUpgrade)> upgrades = new();
        private readonly List<ExtractedConGroup> extractedConGroups = new();
        private readonly List<SongEntry.IniSongEntry> iniEntries = new();

        private readonly SongLibrary library = new();
        private readonly HashSet<string> preScannedDirectories = new();

        internal readonly (string, ChartType)[] CHARTTYPES =
        {
            new("notes.mid",   ChartType.MID),
            new("notes.midi",  ChartType.MID),
            new("notes.chart", ChartType.CHART),
        };

        private void ScanDirectory(DirectoryInfo directory)
        {
            if (!FindOrMarkDirectory(directory.FullName))
                return;

            (FileInfo?, ChartType)[] charts = { new(null, ChartType.MID), new(null, ChartType.MID), new(null, ChartType.CHART) };
            FileInfo? ini = null;
            List<DirectoryInfo> subDirectories = new();
            DirectoryInfo? songs = null;

            List<FileInfo> files = new();

            try
            {
                foreach (FileSystemInfo info in directory.EnumerateFileSystemInfos())
                {
                    string filename = info.Name;
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
                            charts[i].Item1 = file;
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
                charts[0].Item1 = null;
                charts[1].Item1 = null;
            }

            for (int i = 0; i < 3; ++i)
            {
                ref var chart = ref charts[i];
                if (chart.Item1 != null)
                {
                    IniSongEntry entry = new();
                    if (ini != null)
                    {
                        try
                        {
                            entry.Load_Ini(ref ini);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                            Console.WriteLine(ini.FullName);
                            return;
                        }
                    }

                    try
                    {
                        using FrameworkFile_Alloc file = new(chart.Item1!.FullName);
                        if (entry.Scan(file, ref chart))
                        {
                            if (AddEntry(new SHA1Wrapper(file.CalcSHA1()), entry))
                                lock (iniLock) iniEntries.Add(entry);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(chart.Item1!.FullName);
                    }
                    return;
                }
            }

            if (songs != null && AddExtractedCONDirectory(songs.FullName))
                return;

            Parallel.For(0, files.Count, i => AddPossibleCON(files[i]));
            Parallel.For(0, subDirectories.Count, i => ScanDirectory(subDirectories[i]));
        }

        private void AddUpdateDirectory(string directory)
        {
            if (!FindOrMarkDirectory(directory))
                return;

            if (!GetDTAReaderForGroup(Path.Combine(directory, "songs_updates.dta"), out DateTime dtaTime, out var reader))
                return;

            UpdateGroup group = new(directory, dtaTime);
            while (reader!.StartNode())
            {
                string name = reader.GetNameOfNode();
                DateTime lastWrite = File.GetLastWriteTime(Path.Combine(directory, name, $"{name}_update.mid"));
                lock (updatelock)
                {
                    if (AddToUpdateGroup(name, ref lastWrite))
                    {
                        DTAFileReader clone = reader.Clone();
                        group!.updates[name] = (clone, lastWrite);
                        List<(string, DTAFileReader)> updateList;
                        if (updates.TryGetValue(name, out var list))
                            updateList = list;
                        else
                            updates[name] = updateList = new();
                        updateList.Add(new(directory, clone));
                    }
                }
                reader.EndNode();
            }

            if (group!.updates.Count > 0)
                updateGroups.Add(group);
        }

        private void AddUpgradeDirectory(string directory)
        {
            if (!FindOrMarkDirectory(directory))
                return;

            if (!GetDTAReaderForGroup(Path.Combine(directory, "upgrades.dta"), out DateTime dtaTime, out var reader))
                return;

            UpgradeGroup group = new(directory, dtaTime);
            while (reader!.StartNode())
            {
                string name = reader.GetNameOfNode();
                FileInfo file = new(Path.Combine(directory, $"{name}_plus.mid"));
                if (file.Exists)
                {
                    DateTime lastWrite = file.LastWriteTime;
                    lock (upgradelock)
                    {
                        if (AddToUpgradeGroup(name, ref lastWrite))
                        {
                            SongProUpgrade_Extracted upgrade = new(directory, name);
                            DTAFileReader clone = reader.Clone();
                            group!.upgrades[name] = (clone, upgrade);
                            upgrades[name] = new(clone, upgrade);
                        }
                    }
                }

                reader.EndNode();
            }

            if (group!.upgrades.Count > 0)
                upgradeGroups.Add(group);
        }

        private void AddPossibleCON(FileInfo info)
        {
            CONFile? file = CONFile.LoadCON(info.FullName);
            if (file == null)
                return;

            CONGroup group = new(info, file);
            if (group.LoadUpgrades(out var reader))
            {
                while (reader!.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    int index = file.GetFileIndex($"songs_upgrades/{name}_plus.mid");

                    if (index != -1)
                    {
                        DateTime lastWrite = DateTime.FromBinary(file[index].LastWrite);
                        if (AddUpgradeToCONGroup(name, ref lastWrite))
                        {
                            SongProUpgrade_CON upgrade = new(file, name);
                            group.upgrades[name] = upgrade;

                            DTAFileReader clone = reader.Clone();
                            lock (upgradelock)
                                upgrades[name] = new(clone, upgrade);
                        }
                    }

                    reader.EndNode();
                }
            }

            lock (CONlock)
                conGroups.Add(group);
        }

        private bool AddExtractedCONDirectory(string dir)
        {
            if (!FindOrMarkDirectory(dir))
                return false;

            if (!GetDTAReaderForGroup(Path.Combine(dir, "songs.dta"), out DateTime dtaTime, out var reader))
                return false;

            extractedConGroups.Add(new(dir, dtaTime, reader!));
            return true;
        }

        private void LoadCONSongs()
        {
            Parallel.ForEach(conGroups, group => {
                if (group.LoadSongs(out var reader))
                {
                    while (reader!.StartNode())
                    {
                        string name = reader.GetNameOfNode();
                        try
                        {
                            ConSongEntry currentSong = new(name, group.file, reader);
                            if (updates.TryGetValue(name, out var updateList))
                            {
                                foreach (var update in updateList!)
                                    currentSong.Update(update.Item1, update.Item2.Clone());
                            }

                            if (upgrades.TryGetValue(name, out var upgrade))
                            {
                                currentSong.Upgrade = upgrade.Item2;
                                currentSong.SetFromDTA(upgrade.Item1.Clone());
                            }

                            if (currentSong.Scan(out byte[] hash))
                            {
                                if (AddEntry(new SHA1Wrapper(hash), currentSong))
                                    group.AddEntry(currentSong);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine($"CON: DTA Failed to parse song '{name}'. Skipping all further songs in file...");
                            Debug.WriteLine(e.Message);
                            break;
                        }
                        reader.EndNode();
                    }
                }
            });
        }

        private void LoadExtractedCONSongs()
        {
            Parallel.ForEach(extractedConGroups, group =>
            {
                DTAFileReader reader = group.reader!;
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    try
                    {
                        ConSongEntry currentSong = new(name, group.directory, reader);

                        if (updates.TryGetValue(name, out var updateList))
                        {
                            foreach (var update in updateList!)
                                currentSong.Update(update.Item1, update.Item2.Clone());
                        }

                        if (upgrades.TryGetValue(name, out var upgrade))
                        {
                            currentSong.Upgrade = upgrade.Item2;
                            currentSong.SetFromDTA(upgrade.Item1.Clone());
                        }

                        if (currentSong.Scan(out byte[] hash))
                        {
                            if (AddEntry(new SHA1Wrapper(hash), currentSong))
                                group.AddEntry(currentSong);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Extracted CON: DTA Failed to parse song '{name}'. Skipping all further songs in file...");
                        Debug.WriteLine(e.Message);
                        break;
                    }

                    reader.EndNode();
                }
            });
        }

        private void FinalizeIniEntries()
        {
            foreach (var entry in iniEntries)
                entry.FinishScan();
        }

        static private bool GetDTAReaderForGroup(string dtaFile, out DateTime lastWrite, out DTAFileReader? reader)
        {
            lastWrite = new();
            reader = null;
            FileInfo dta = new(dtaFile);
            if (!dta.Exists)
                return false;

            lastWrite = dta.LastWriteTime;
            reader = new(dta.FullName);
            return true;
        }

        private bool AddToUpdateGroup(string shortname, ref DateTime lastWrite)
        {
            foreach (var group in updateGroups)
            {
                if (group.updates.TryGetValue(shortname, out var currTime))
                {
                    if (currTime.Item2 >= lastWrite)
                        return false;
                    group.updates.Remove(shortname);
                    break;
                }
            }
            return true;
        }

        private bool AddToUpgradeGroup(string shortname, ref DateTime lastWrite)
        {
            foreach (var group in upgradeGroups)
            {
                if (group.upgrades.TryGetValue(shortname, out var currUpgrade))
                {
                    if (currUpgrade.Item2.UpgradeLastWrite >= lastWrite)
                        return false;
                    group.upgrades.Remove(shortname);
                    break;
                }
            }
            return true;
        }

        private bool AddUpgradeToCONGroup(string shortname, ref DateTime lastWrite)
        {
            foreach (var group in conGroups)
            {
                if (group.upgrades.TryGetValue(shortname, out SongProUpgrade_CON? currUpgrade))
                {
                    if (currUpgrade!.UpgradeLastWrite >= lastWrite)
                        return false;
                    group.upgrades.Remove(shortname);
                    return true;
                }
            }

            lock (upgradelock)
                return AddToUpgradeGroup(shortname, ref lastWrite);
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

        private void SaveToFile(string fileName)
        {
            using FileStream fs = new(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(fs, Encoding.UTF8, false);

            writer.Write(CACHE_VERSION);
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

            List<CONGroup> upgradeCons = new();
            List<CONGroup> entryCons = new();
            foreach (var group in conGroups)
            {
                if (group.UpgradeCount > 0)
                    upgradeCons.Add(group);

                if (group.EntryCount > 0)
                    entryCons.Add(group);
            }

            writer.Write(upgradeCons.Count);
            foreach (var group in upgradeCons)
            {
                byte[] buffer = group.FormatUpgradesForCache();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(entryCons.Count);
            foreach (var group in entryCons)
            {
                byte[] buffer = group.FormatEntriesForCache();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(extractedConGroups.Count);
            foreach (var group in extractedConGroups)
            {
                byte[] buffer = group.FormatEntriesForCache();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(iniEntries.Count);
            foreach (IniSongEntry entry in iniEntries)
            {
                byte[] buffer = entry.FormatCacheData();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }
        }
    }

    public class CONGroup
    {
        public readonly CONFile file;
        private readonly List<ConSongEntry> entries = new();
        public readonly Dictionary<string, SongProUpgrade_CON> upgrades = new();

        public int EntryCount => entries.Count;
        public int UpgradeCount => upgrades.Count;

        private readonly CONSongFileInfo info;
        private int songDtaIndex = -1;
        private int upgradeDtaIndex = -1;
        private readonly object entryLock = new();
        private readonly object upgradeLock = new();

        public int Count => entries.Count;

        public CONGroup(CONSongFileInfo info, CONFile file)
        {
            this.info = info;
            this.file = file;
        }

        public void AddUpgrade(string name, SongProUpgrade_CON upgrade) { lock (upgradeLock) upgrades[name] = upgrade; }
        public void AddEntry(ConSongEntry entry) { lock (entryLock) entries.Add(entry); }

        internal const string SongsFilePath = "songs/songs.dta";
        internal const string UpgradesFilePath = "songs_upgrades/upgrades.dta";

        public bool LoadUpgrades(out DTAFileReader? reader)
        {
            reader = null;
            return Load(ref upgradeDtaIndex, UpgradesFilePath, ref reader);
        }

        public bool LoadSongs(out DTAFileReader? reader)
        {
            reader = null;
            return Load(ref songDtaIndex, SongsFilePath, ref reader);
        }

        private bool Load(ref int index, string dtaPath, ref DTAFileReader? reader)
        {
            index = file.GetFileIndex(dtaPath);
            if (index == -1)
                return false;

            reader = new(file.LoadSubFile(index)!, true);
            return true;
        }

        public byte[] FormatUpgradesForCache()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(file.Filename);
            writer.Write(info.LastWriteTime.Ticks);
            writer.Write(upgradeDtaIndex);
            writer.Write(file[upgradeDtaIndex].LastWrite);
            writer.Write(upgrades.Count);
            foreach (var upgrade in upgrades)
                upgrade.Value.WriteToCache(writer);
            return ms.ToArray();
        }

        public byte[] FormatEntriesForCache()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(file.Filename);
            writer.Write(info.LastWriteTime.Ticks);
            writer.Write(songDtaIndex);
            writer.Write(file[songDtaIndex].LastWrite);
            writer.Write(entries.Count);
            foreach (var entry in entries)
            {
                byte[] data = entry.FormatCacheData();
                writer.Write(data.Length);
                writer.Write(data);
            }
            return ms.ToArray();
        }
    }

    public class ExtractedConGroup
    {
        public readonly string directory;
        private readonly DateTime dtaLastWrite;
        private readonly List<ConSongEntry> entries = new();
        private readonly object entryLock = new();
        public readonly DTAFileReader? reader;

        public ExtractedConGroup(string directory, DateTime dtaLastWrite, DTAFileReader reader)
        {
            this.directory = directory;
            this.dtaLastWrite = dtaLastWrite;
            this.reader = reader;
        }

        public void AddEntry(ConSongEntry entry) { lock (entryLock) entries.Add(entry); }

        public byte[] FormatEntriesForCache()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(dtaLastWrite.ToBinary());
            writer.Write(entries.Count);
            foreach (var entry in entries)
            {
                byte[] data = entry.FormatCacheData();
                writer.Write(data.Length);
                writer.Write(data);
            }
            return ms.ToArray();
        }
    }

    internal class UpdateGroup
    {
        public readonly string directory;
        private readonly DateTime dtaLastWrite;
        public readonly Dictionary<string, (DTAFileReader?, DateTime)> updates = new();

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
            foreach (var update in updates)
            {
                writer.Write(update.Key);
                writer.Write(update.Value.Item2.ToBinary());
            }
            return ms.ToArray();
        }
    }

    internal class UpgradeGroup
    {
        public readonly string directory;
        private readonly DateTime dtaLastWrite;
        public readonly Dictionary<string, (DTAFileReader?, SongProUpgrade_Extracted)> upgrades = new();

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
                upgrade.Value.Item2.WriteToCache(writer);
            }
            return ms.ToArray();
        }
    }
}
