using Framework.Hashes;
using Framework.Serialization;
using Framework.SongEntry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Library
{
    public partial class SongCache
    {
        public static SongLibrary QuickScan(string cacheFileDirectory)
        {
            using SongCache cache = new();
            cache.LoadCacheFile_Quick(Path.Combine(cacheFileDirectory, "songcache_CS.bin"));
            return cache.library;
        }

        private void LoadCacheFile_Quick(string cacheFile)
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
                entryTasks.Add(Task.Run(() => QuickReadIniEntry(buffer)));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.BaseStream.Position += length;
            }

            List<Task> conTasks = new();
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(length);
                conTasks.Add(Task.Run(() => QuickReadUpgradeDirectory(buffer)));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(length);
                conTasks.Add(Task.Run(() => QuickReadUpgradeCON(buffer)));
            }

            Task.WaitAll(conTasks.ToArray());

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(length);
                entryTasks.Add(Task.Run(() => QuickReadCONGroup(buffer)));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(length);
                entryTasks.Add(Task.Run(() => QuickReadExtractedCONGroup(buffer)));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadIniEntry(byte[] buffer)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            string directory = reader.ReadString();
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= CHARTTYPES.Length)
                return;
            reader.BaseStream.Position += 8;

            ref var chartType = ref CHARTTYPES[chartTypeIndex];
            FileInfo chartFile = new(Path.Combine(directory, chartType.Item1));
            FileInfo? iniFile = null;
            if (reader.ReadBoolean())
            {
                iniFile = new(Path.Combine(directory, "song.ini"));
                reader.BaseStream.Position += 8;
            }
            IniSongEntry entry = new(directory, chartFile, iniFile, ref chartType, reader);
            SHA1Wrapper hash = new(reader);
            AddEntry(hash, entry);
        }

        private void QuickReadUpgradeDirectory(byte[] buffer)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            string directory = reader.ReadString();
            DateTime dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
            UpgradeGroup group = new(directory, dtaLastWrite);
            lock (upgradeGroupLock)
                upgradeGroups.Add(group);

            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                DateTime lastWrite = DateTime.FromBinary(reader.ReadInt64());
                string filename = Path.Combine(directory, $"{name}_plus.mid");
                SongProUpgrade upgrade = new(filename, lastWrite);
                group.upgrades.Add(name, upgrade);
                AddUpgrade(name, null, upgrade);
            }
        }

        private void QuickReadUpgradeCON(byte[] buffer)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            string filename = reader.ReadString();
            DateTime conLastWrite = DateTime.FromBinary(reader.ReadInt64());
            reader.BaseStream.Position += 4;
            int count = reader.ReadInt32();

            if (CreateCONGroup(filename, out PackedCONGroup? group))
            {
                CONFile file = group!.file;
                AddCONGroup(filename, group);

                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadString();
                    DateTime lastWrite = DateTime.FromBinary(reader.ReadInt64());
                    FileListing? listing = file[$"songs_upgrades/{name}_plus.mid"];

                    SongProUpgrade upgrade = new(file, listing, lastWrite);
                    group.AddUpgrade(name, upgrade);
                    AddUpgrade(name, null, upgrade);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    AddInvalidSong(reader.ReadString());
                    reader.BaseStream.Position += 4;
                }
            }
        }

        private void QuickReadCONGroup(byte[] buffer)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            string filename = reader.ReadString();
            reader.BaseStream.Position += 4;
            if (!FindCONGroup(filename, out PackedCONGroup? group))
            {
                if (!CreateCONGroup(filename, out group))
                    return;
                AddCONGroup(filename, group!);
            }

            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadString();
                reader.BaseStream.Position += 4;
                int length = reader.ReadInt32();

                if (invalidSongsInCache.Contains(name))
                {
                    reader.BaseStream.Position += length;
                    continue;
                }

                byte[] entryData = reader.ReadBytes(length);
                entryTasks.Add(Task.Run(() => QuickReadCONEntry(group!.file, name, entryData)));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadCONEntry(CONFile file, string nodeName, byte[] buffer)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            FileListing? midiListing = file[reader.ReadString()];
            reader.BaseStream.Position += 4;

            FileListing? moggListing = null;
            FileInfo? moggInfo = null;
            if (reader.ReadBoolean())
            {
                moggListing = file[reader.ReadString()];
                reader.BaseStream.Position += 4;
            }
            else
            {
                moggInfo = new FileInfo(reader.ReadString());
                reader.BaseStream.Position += 8;
            }

            FileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = new FileInfo(reader.ReadString());
                reader.BaseStream.Position += 8;
            }

            ConSongEntry currentSong = new(file, nodeName, midiListing, moggListing, moggInfo, updateInfo, reader);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                currentSong.Upgrade = upgrade.Item2;

            SHA1Wrapper hash = new(reader);
            AddEntry(hash, currentSong);
        }

        private void QuickReadExtractedCONGroup(byte[] buffer)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            string directory = reader.ReadString();
            reader.BaseStream.Position += 8;
            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadString();
                reader.BaseStream.Position += 4;
                int length = reader.ReadInt32();

                if (invalidSongsInCache.Contains(name))
                {
                    reader.BaseStream.Position += length;
                    continue;
                }

                byte[] entryData = reader.ReadBytes(length);
                entryTasks.Add(Task.Run(() => QuickReadExtractedCONEntry(name, entryData)));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadExtractedCONEntry(string nodeName, byte[] buffer)
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms, Encoding.UTF8, false);

            FileInfo midiInfo = new(reader.ReadString());
            reader.BaseStream.Position += 8;

            FileInfo moggInfo = new(reader.ReadString());
            reader.BaseStream.Position += 8;

            FileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = new FileInfo(reader.ReadString());
                reader.BaseStream.Position += 8;
            }

            ConSongEntry currentSong = new(midiInfo, moggInfo, updateInfo, reader);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                currentSong.Upgrade = upgrade.Item2;

            SHA1Wrapper hash = new(reader);
            AddEntry(hash, currentSong);
        }
    }
}
