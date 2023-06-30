using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Library
{
    public partial class SongCache
    {
        private void SaveToFile(string fileName)
        {
            using var writer = new BinaryWriter(new FileStream(fileName, FileMode.Create, FileAccess.Write));

            writer.Write(CACHE_VERSION);
            writer.Write(IniCount);
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
}
