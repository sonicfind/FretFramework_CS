using Framework.Hashes;
using Framework.Serialization;
using Framework.Serialization.XboxSTFS;
using Framework.SongEntry;
using Framework.SongEntry.ConEntry;
using Framework.Types;
using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Library
{
    
    public class SongLibrary
    {
        private readonly SortedDictionary<SHA1Wrapper, List<SongEntry.SongEntry>> m_songlist = new();
        private readonly object entryLock = new();
        private SongCache? cache;

        public int Count
        {
            get
            {
                int count = 0;
                foreach(var node in m_songlist)
                    count += node.Value.Count;
                return count;
            }
        }

        public void Clear() { m_songlist.Clear(); }

        public void RunFullScan(List<string> baseDirectories)
        {
            cache = new();
            Parallel.For(0, baseDirectories.Count, i => ScanDirectory(new(baseDirectories[i])));
            FinishScans();
            cache = null;
        }

        public SortedDictionary<SHA1Wrapper, List<SongEntry.SongEntry>>.Enumerator GetEnumerator() => m_songlist.GetEnumerator();

        internal readonly (string, ChartType)[] CHARTTYPES =
        {
            new("notes.mid",   ChartType.MID),
            new("notes.midi",  ChartType.MID),
            new("notes.chart", ChartType.CHART),
        };

        private void ScanDirectory(DirectoryInfo directory)
        {
            if (!cache!.FindOrMarkDirectory(directory.FullName))
                return;

            (FileInfo?, ChartType) [] charts = { new(null, ChartType.MID), new(null, ChartType.MID), new(null, ChartType.CHART) };
            FileInfo? ini = null;
            List<DirectoryInfo> subDirectories = new();
            List<FileInfo> files = new();

            try
            {
                foreach (FileSystemInfo info in directory.EnumerateFileSystemInfos())
                {
                    string filename = info.Name;
                    if ((info.Attributes & FileAttributes.Directory) > 0)
                    {
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
                                cache.AddIniEntry(entry);
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

            Parallel.For(0, files.Count, i => ScanPossibleCON(files[i]));
            Parallel.For(0, subDirectories.Count, i => ScanDirectory(subDirectories[i]));
        }

        private void ScanPossibleCON(FileInfo info)
        {
            if (!CONEntryGroup.TryLoadCon(info, out CONEntryGroup? group))
                return;

            if (!group!.LoadSongs(out List<DTAFileNode>? nodes))
                return;

            CONFile file = group.File;
            Parallel.For(0, nodes!.Count, i =>
            {
                try
                {
                    var node = nodes[i];
                    CONEntry currentSong = new(file, node);
                    if (currentSong.Scan(out SHA1Wrapper hash))
                    {
                        if (AddEntry(hash, currentSong))
                            group.AddEntry(currentSong);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Failed to load song, skipping...");
                    Debug.WriteLine(e.Message);
                }
            });

            if (group.Count > 0)
                cache!.AddConGroup(group);
        }

        private void FinishScans()
        {
            foreach (var node in m_songlist)
                foreach (var entry in node.Value)
                    entry.FinishScan();
        }

        private bool AddEntry(SHA1Wrapper hash, SongEntry.SongEntry entry)
        {
            lock (entryLock)
            {
                if (m_songlist.TryGetValue(hash, out List<SongEntry.SongEntry>? list))
                    list.Add(entry);
                else
                    m_songlist.Add(hash, new() { entry });
            }
            return true;
        }

        
    }
}
