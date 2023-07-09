using Framework.Hashes;
using Framework.Library.CacheNodes;
using Framework.Serialization;
using Framework.SongEntry;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Library
{
    public partial class SongCache
    {
        public static SongLibrary QuickScan(string cacheFileDirectory, bool multithreaded = true)
        {
            using SongCache cache = new();

            string cacheFile = Path.Combine(cacheFileDirectory, "songcache_CS.bin");
            if (multithreaded)
                cache.LoadCacheFile_Quick(cacheFile);
            else
                cache.LoadCacheFile_Quick_Serial(cacheFile);

            if (multithreaded)
                cache.MapCategories();
            else
                cache.MapCategories_Serial();
            return cache.library;
        }

        private void LoadCacheFile_Quick(string cacheFile)
        {
            {
                FileInfo info = new(cacheFile);
                if (!info.Exists || info.Length < 28)
                    return;
            }

            using FileStream fs = new(cacheFile, FileMode.Open, FileAccess.Read);
            {
                byte[] version = new byte[4];
                fs.Read(version, 0, 4);
                if (BinaryPrimitives.ReadInt32LittleEndian(version) != CACHE_VERSION)
                    return;
            }

            using BinaryFileReader reader = new(fs);
            CategoryCacheStrings strings = new(reader, true);

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    QuickReadIniEntry(sectionReader, strings);
                    sectionReader.Dispose();
                }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.Position += length;
            }

            List<Task> conTasks = new();
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                conTasks.Add(Task.Run(() =>
                {
                    QuickReadUpgradeDirectory(sectionReader);
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
                    QuickReadUpgradeCON(sectionReader);
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
                    QuickReadCONGroup(sectionReader, strings);
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
                    QuickReadExtractedCONGroup(sectionReader, strings);
                    sectionReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void LoadCacheFile_Quick_Serial(string cacheFile)
        {
            {
                FileInfo info = new(cacheFile);
                if (!info.Exists || info.Length < 28)
                    return;
            }

            using FileStream fs = new(cacheFile, FileMode.Open, FileAccess.Read);
            {
                byte[] version = new byte[4];
                fs.Read(version, 0, 4);
                if (BinaryPrimitives.ReadInt32LittleEndian(version) != CACHE_VERSION)
                    return;
            }

            using BinaryFileReader reader = new(fs);
            CategoryCacheStrings strings = new(reader, false);

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadIniEntry(reader, strings);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.Position += length;
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadUpgradeDirectory(reader);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadUpgradeCON(reader);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadCONGroup_Serial(reader, strings);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadExtractedCONGroup_Serial(reader, strings);
                reader.ExitSection();
            }
        }

        private void QuickReadIniEntry(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= CHARTTYPES.Length)
                return;
            reader.Position += 8;

            ref var chartType = ref CHARTTYPES[chartTypeIndex];
            FileInfo chartFile = new(Path.Combine(directory, chartType.Item1));
            FileInfo? iniFile = null;
            if (reader.ReadBoolean())
            {
                iniFile = new(Path.Combine(directory, "song.ini"));
                reader.Position += 8;
            }
            IniSongEntry entry = new(directory, chartFile, iniFile, ref chartType, reader, strings);
            SHA1Wrapper hash = new(reader);
            AddEntry(hash, entry);
        }

        private void QuickReadUpgradeDirectory(BinaryFileReader reader)
        {
            string directory = reader.ReadLEBString();
            DateTime dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            UpgradeGroup group = new(directory, dtaLastWrite);
            lock (upgradeGroupLock)
                upgradeGroups.Add(group);

            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadLEBString();
                DateTime lastWrite = DateTime.FromBinary(reader.ReadInt64());
                string filename = Path.Combine(directory, $"{name}_plus.mid");

                SongProUpgrade upgrade = new(filename, lastWrite);
                group.upgrades.Add(name, upgrade);
                AddUpgrade(name, null, upgrade);
            }
        }

        private void QuickReadUpgradeCON(BinaryFileReader reader)
        {
            string filename = reader.ReadLEBString();
            reader.Position += 12;
            int count = reader.ReadInt32();

            if (CreateCONGroup(filename, out PackedCONGroup? group))
            {
                CONFile file = group!.file;
                AddCONGroup(filename, group);

                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadLEBString();
                    DateTime lastWrite = DateTime.FromBinary(reader.ReadInt64());
                    FileListing? listing = file[$"songs_upgrades/{name}_plus.mid"];

                    SongProUpgrade upgrade = new(file, listing, lastWrite);
                    AddUpgrade(name, null, upgrade);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadLEBString();
                    DateTime lastWrite = DateTime.FromBinary(reader.ReadInt64());

                    SongProUpgrade upgrade = new(null, null, lastWrite);
                    AddUpgrade(name, null, upgrade);
                }
            }
        }

        private void QuickReadCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string filename = reader.ReadLEBString();
            reader.Position += 4;
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
                string name = reader.ReadLEBString();
                reader.Position += 4;
                int length = reader.ReadInt32();

                BinaryFileReader entryReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    QuickReadCONEntry(group!.file, name, entryReader, strings);
                    entryReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadCONGroup_Serial(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string filename = reader.ReadLEBString();
            reader.Position += 4;
            if (!FindCONGroup(filename, out PackedCONGroup? group))
            {
                if (!CreateCONGroup(filename, out group))
                    return;
                AddCONGroup(filename, group!);
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                reader.Position += 4;
                int length = reader.ReadInt32();

                reader.EnterSection(length);
                QuickReadCONEntry(group!.file, name, reader, strings);
                reader.ExitSection();
            }
        }

        private void QuickReadCONEntry(CONFile file, string nodeName, BinaryFileReader reader, CategoryCacheStrings strings)
        {
            FileListing? midiListing = file[reader.ReadLEBString()];
            reader.Position += 4;

            FileListing? moggListing = null;
            FileInfo? moggInfo = null;
            if (reader.ReadBoolean())
            {
                moggListing = file[reader.ReadLEBString()];
                reader.Position += 4;
            }
            else
            {
                moggInfo = new FileInfo(reader.ReadLEBString());
                reader.Position += 8;
            }

            FileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = new FileInfo(reader.ReadLEBString());
                reader.Position += 8;
            }

            ConSongEntry currentSong = new(file, nodeName, midiListing, moggListing, moggInfo, updateInfo, reader, strings);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                currentSong.Upgrade = upgrade.Item2;

            SHA1Wrapper hash = new(reader);
            AddEntry(hash, currentSong);
        }

        private void QuickReadExtractedCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            reader.Position += 8;
            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                reader.Position += 4;
                int length = reader.ReadInt32();

                BinaryFileReader entryReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    QuickReadExtractedCONEntry(name, entryReader, strings);
                    entryReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadExtractedCONGroup_Serial(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            reader.Position += 8;
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                reader.Position += 4;
                int length = reader.ReadInt32();

                reader.EnterSection(length);
                QuickReadExtractedCONEntry(name, reader, strings);
                reader.ExitSection();
            }
        }

        private void QuickReadExtractedCONEntry(string nodeName, BinaryFileReader reader, CategoryCacheStrings strings)
        {
            FileInfo midiInfo = new(reader.ReadLEBString());
            reader.Position += 8;

            FileInfo moggInfo = new(reader.ReadLEBString());
            reader.Position += 8;

            FileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = new FileInfo(reader.ReadLEBString());
                reader.Position += 8;
            }

            ConSongEntry currentSong = new(midiInfo, moggInfo, updateInfo, reader, strings);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                currentSong.Upgrade = upgrade.Item2;

            SHA1Wrapper hash = new(reader);
            AddEntry(hash, currentSong);
        }
    }
}
