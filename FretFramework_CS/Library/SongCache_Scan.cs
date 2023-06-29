using Framework.Hashes;
using Framework.Serialization;
using Framework.SongEntry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Library
{
    public partial class SongCache
    {
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

        private void AddUpdateDirectory(string directory)
        {
            if (!FindOrMarkDirectory(directory))
                return;

            FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
            if (!dta.Exists)
                return;

            UpdateGroupAdd(directory, dta);
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

        private void FinalizeIniEntries()
        {
            foreach (var entryList in iniEntries)
                foreach (var entry in entryList.Value)
                    entry.FinishScan();
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
    }
}
