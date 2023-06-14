using Framework.Hashes;
using Framework.Serialization;
using Framework.SongEntry;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Library
{
    public class SongLibrary
    {
        private readonly SortedDictionary<SHA1Wrapper, List<SongEntry.SongEntry>> m_songlist = new();
        private readonly HashSet<string> m_preScannedDirectories = new();
        private readonly object dirLock = new();
        private readonly object entryLock = new();

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
            Parallel.For(0, baseDirectories.Count, i => ScanDirectory(new(baseDirectories[i])));
            FinishScans();
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
            if (!FindOrMarkDirectory(directory.FullName))
                return;

            (FileInfo?, ChartType) [] charts = { new(null, ChartType.MID), new(null, ChartType.MID), new(null, ChartType.CHART) };
            FileInfo? ini = null;
            List<DirectoryInfo> subDirectories = new();
            try
            {
                foreach (FileSystemInfo file in directory.EnumerateFileSystemInfos())
                {
                    if ((file.Attributes & FileAttributes.Directory) > 0)
                    {
                        subDirectories.Add((file as DirectoryInfo)!);
                        continue;
                    }

                    string filename = file.Name;
                    if (filename == "song.ini")
                    {
                        ini = file as FileInfo;
                        continue;
                    }

                    for (int i = 0; i < 3; ++i)
                    {
                        if (filename == CHARTTYPES[i].Item1)
                        {
                            charts[i].Item1 = file as FileInfo;
                            break;
                        }
                    }

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
                    SongEntry.SongEntry entry = new();
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
                            AddEntry(new SHA1Wrapper(file.CalcSHA1()), entry);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(chart.Item1!.FullName);
                    }
                    return;
                }
            }

            Parallel.For(0, subDirectories.Count, i => ScanDirectory(subDirectories[i]));
        }

        private void FinishScans()
        {
            m_preScannedDirectories.Clear();
            foreach (var node in m_songlist)
                foreach (var entry in node.Value)
                    entry.FinishScan();
        }

        private void AddEntry(SHA1Wrapper hash, SongEntry.SongEntry entry)
        {
            lock (entryLock)
            {
                if (m_songlist.TryGetValue(hash, out List<SongEntry.SongEntry>? list))
                    list.Add(entry);
                else
                    m_songlist.Add(hash, new() { entry });
            }
        }

        private void MarkDirectory(string directory)
        {
            lock (dirLock)
                m_preScannedDirectories.Add(directory);
        }

        private bool FindOrMarkDirectory(string directory)
        {
            lock (dirLock)
            {
                if (m_preScannedDirectories.Contains(directory))
                    return false;

                m_preScannedDirectories.Add(directory);
                return true;
            }
        }
    }
}
