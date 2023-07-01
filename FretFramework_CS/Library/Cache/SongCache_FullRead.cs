using Framework.Hashes;
using Framework.Serialization;
using Framework.SongEntry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Library
{
    public partial class SongCache
    {
        private readonly HashSet<string> invalidSongsInCache = new();
        private static readonly object invalidLock = new();
        private void LoadCacheFile(string cacheFile, List<string> baseDirectories)
        {
            {
                FileInfo info = new(cacheFile);
                if (!info.Exists || info.Length < 28)
                    return;
            }

            using FileStream fs = new(cacheFile, FileMode.Open, FileAccess.Read);
            if (fs.ReadInt32LE() != CACHE_VERSION)
                return;

            using BinaryFileReader reader = new(fs.ReadBytes((int)fs.Length - 4));

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    ReadIniEntry(sectionReader, baseDirectories);
                    sectionReader.Dispose();
                }));
            }

            List<Task> conTasks = new();
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                conTasks.Add(Task.Run(() =>
                {
                    ReadUpdateDirectory(sectionReader, baseDirectories);
                    sectionReader.Dispose();
                }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                conTasks.Add(Task.Run(() => 
                {
                    ReadUpgradeDirectory(sectionReader, baseDirectories);
                    sectionReader.Dispose();
                }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                conTasks.Add(Task.Run(() =>
                {
                    ReadUpgradeCON(sectionReader, baseDirectories);
                    sectionReader.Dispose();
                }));
            }

            Task.WaitAll(conTasks.ToArray());

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    ReadCONGroup(sectionReader, baseDirectories);
                    sectionReader.Dispose();
                }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    ReadExtractedCONGroup(sectionReader, baseDirectories);
                    sectionReader.Dispose();
                }));
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

        private void ReadIniEntry(BinaryFileReader reader, List<string> baseDirectories)
        {
            string directory = reader.ReadLEBString();
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

        private void ReadUpdateDirectory(BinaryFileReader reader, List<string> baseDirectories)
        {
            string directory = reader.ReadLEBString();
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
                AddInvalidSong(reader.ReadLEBString());
        }

        private void AddInvalidSong(string name)
        {
            lock (invalidLock) invalidSongsInCache.Add(name);
        }

        private void ReadUpgradeDirectory(BinaryFileReader reader, List<string> baseDirectories)
        {
            string directory = reader.ReadLEBString();
            DateTime dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

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
                            string name = reader.ReadLEBString();
                            DateTime lastWrite = DateTime.FromBinary(reader.ReadInt64());
                            if (!group.upgrades.TryGetValue(name, out SongProUpgrade? upgrade) || upgrade!.UpgradeLastWrite != lastWrite)
                                AddInvalidSong(name);
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(reader.ReadLEBString());
                reader.Position += 4;
            }
        }

        private void ReadUpgradeCON(BinaryFileReader cacheReader, List<string> baseDirectories)
        {
            string filename = cacheReader.ReadLEBString();
            DateTime conLastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
            int dtaLastWrite = cacheReader.ReadInt32();
            int count = cacheReader.ReadInt32();

            if (StartsWithBaseDirectory(filename, baseDirectories))
            {
                if (CreateCONGroup(filename, out PackedCONGroup? group))
                {
                    AddCONGroup(filename, group!);

                    if (group!.LoadUpgrades(out var reader))
                    {
                        AddCONUpgrades(group, reader!);

                        if (group.UpgradeDTALastWrite == dtaLastWrite)
                        {
                            if (group.lastWrite != conLastWrite)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    string name = cacheReader.ReadLEBString();
                                    if (group.upgrades[name].UpgradeLastWrite != DateTime.FromBinary(cacheReader.ReadInt64()))
                                        AddInvalidSong(name);
                                }
                            }
                            return;
                        }
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(cacheReader.ReadLEBString());
                cacheReader.Position += 4;
            }
        }

        private void ReadCONGroup(BinaryFileReader reader, List<string> baseDirectories)
        {
            string filename = reader.ReadLEBString();
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
                string name = reader.ReadLEBString();
                int index = reader.ReadInt32();
                int length = reader.ReadInt32();
                if (invalidSongsInCache.Contains(name))
                {
                    reader.Position += length;
                    continue;
                }

                BinaryFileReader entryReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    ReadCONEntry(group, name, index, entryReader);
                    entryReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private static void ReadCONEntry(PackedCONGroup group, string nodeName, int index, BinaryFileReader reader)
        {
            FileListing? midiListing = group.file[reader.ReadLEBString()];
            if (midiListing == null || midiListing.LastWrite != reader.ReadInt32())
                return;

            FileListing? moggListing = null;
            FileInfo? moggInfo = null;
            if (reader.ReadBoolean())
            {
                moggListing = group.file[reader.ReadLEBString()];
                if (moggListing == null || moggListing.LastWrite != reader.ReadInt32())
                    return;
            }
            else
            {
                moggInfo = new FileInfo(reader.ReadLEBString());
                if (!moggInfo.Exists || moggInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
            }

            FileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = new FileInfo(reader.ReadLEBString());
                if (!updateInfo.Exists || updateInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
            }

            ConSongEntry currentSong = new(group.file, nodeName, midiListing, moggListing, moggInfo, updateInfo, reader);
            SHA1Wrapper hash = new(reader);
            group.AddEntry(nodeName, index, currentSong, hash);
        }

        private void ReadExtractedCONGroup(BinaryFileReader reader, List<string> baseDirectories)
        {
            string directory = reader.ReadLEBString();
            if (!StartsWithBaseDirectory(directory, baseDirectories))
                return;

            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists)
                return;

            ExtractedConGroup group = new(dtaInfo.FullName, dtaInfo.LastWriteTime);
            MarkDirectory(directory);
            AddExtractedCONGroup(directory, group);

            if (dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return;

            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                int index = reader.ReadInt32();
                int length = reader.ReadInt32();

                if (invalidSongsInCache.Contains(name))
                {
                    reader.Position += length;
                    continue;
                }

                BinaryFileReader entryReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    ReadExtractedCONEntry(group, name, index, entryReader);
                    entryReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private static void ReadExtractedCONEntry(ExtractedConGroup group, string nodeName, int index, BinaryFileReader reader)
        {
            FileInfo midiInfo = new(reader.ReadLEBString());
            if (!midiInfo.Exists || midiInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return;

            FileInfo moggInfo = new(reader.ReadLEBString());
            if (!moggInfo.Exists || moggInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return;

            FileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = new FileInfo(reader.ReadLEBString());
                if (!updateInfo.Exists || updateInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
            }

            ConSongEntry currentSong = new(midiInfo, moggInfo, updateInfo, reader);
            SHA1Wrapper hash = new(reader);
            group.AddEntry(nodeName, index, currentSong, hash);
        }
    }
}
