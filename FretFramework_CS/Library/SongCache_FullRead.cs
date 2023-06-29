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
                            if (group.upgrades[name].UpgradeLastWrite != DateTime.FromBinary(cacheReader.ReadInt64()))
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
                                    if (group.upgrades[name].UpgradeLastWrite != DateTime.FromBinary(cacheReader.ReadInt64()))
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

        private static void ReadCONEntry(PackedCONGroup group, string nodeName, byte[] buffer)
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

        private void MarkDirectory(string directory)
        {
            lock (dirLock) preScannedDirectories.Add(directory);
        }

        private void MarkFile(string file)
        {
            lock (fileLock) preScannedFiles.Add(file);
        }
    }
}
