using Framework.Hashes;
using Framework.Serialization;
using Framework.SongEntry;
using System;
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
        public static SongLibrary QuickScan(string cacheFileDirectory)
        {
            using SongCache cache = new();
            cache.LoadCacheFile_Quick(Path.Combine(cacheFileDirectory, "songcache_CS.bin"));
            cache.MapCategories();

            return cache.library;
        }

        private void LoadCacheFile_Quick(string cacheFile)
        {
            {
                FileInfo info = new(cacheFile);
                if (!info.Exists || info.Length < 28)
                    return;
            }

            using BinaryFileReader reader = new(cacheFile);

            if (reader.ReadInt32() != CACHE_VERSION)
                return;

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    QuickReadIniEntry(sectionReader);
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
                conTasks.Add(Task.Run(() => { QuickReadUpgradeCON(sectionReader); sectionReader.Dispose(); }));
            }

            Task.WaitAll(conTasks.ToArray());

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() => { QuickReadCONGroup(sectionReader); sectionReader.Dispose(); }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                BinaryFileReader sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() => { QuickReadExtractedCONGroup(sectionReader); sectionReader.Dispose(); }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadIniEntry(BinaryFileReader reader)
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
            IniSongEntry entry = new(directory, chartFile, iniFile, ref chartType, reader);
            SHA1Wrapper hash = new(reader);
            AddEntry(hash, entry);
        }

        private void QuickReadUpgradeDirectory(BinaryFileReader reader)
        {
            string directory = reader.ReadLEBString();
            DateTime dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
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
            DateTime conLastWrite = DateTime.FromBinary(reader.ReadInt64());
            reader.Position += 4;
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

        private void QuickReadCONGroup(BinaryFileReader reader)
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
                entryTasks.Add(Task.Run(() => QuickReadCONEntry(group!.file, name, entryReader)));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadCONEntry(CONFile file, string nodeName, BinaryFileReader reader)
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

            ConSongEntry currentSong = new(file, nodeName, midiListing, moggListing, moggInfo, updateInfo, reader);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                currentSong.Upgrade = upgrade.Item2;

            SHA1Wrapper hash = new(reader);
            AddEntry(hash, currentSong);
        }

        private void QuickReadExtractedCONGroup(BinaryFileReader reader)
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
                entryTasks.Add(Task.Run(() => QuickReadExtractedCONEntry(name, entryReader)));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadExtractedCONEntry(string nodeName, BinaryFileReader reader)
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

            ConSongEntry currentSong = new(midiInfo, moggInfo, updateInfo, reader);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                currentSong.Upgrade = upgrade.Item2;

            SHA1Wrapper hash = new(reader);
            AddEntry(hash, currentSong);
        }
    }
}
