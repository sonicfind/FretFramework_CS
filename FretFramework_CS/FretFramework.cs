using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using CommandLine;
using Framework.Serialization;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Framework
{
    [SimpleJob(RuntimeMoniker.Net472, baseline: true)]
    [SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [SimpleJob(RuntimeMoniker.NativeAot70)]
    [SimpleJob(RuntimeMoniker.Mono)]
    [RPlotExporter]
    class FretFramework
    {
        static void Main(string[] args)
        {
            string path = "E:\\Documents\\My Games\\Clone Hero\\CH Songs\\Charter Application [Sonicfind]\\Mutsuhiko Izumi - L.A.RIDER (Long Version) [Sonicfind]\\notes.mid";
            SongEntry.SongEntry entry = new(new(path, Types.ChartType.MID), File.GetLastWriteTime(path));
            path = Path.Combine(Path.GetDirectoryName(path)!, "song.ini");
            entry.Load_Ini(path, File.GetLastWriteTime(path));
            if (entry.Scan(out FrameworkFile? file))
            {
                byte[] hash = file!.HASH_MD5;
                Console.WriteLine(BitConverter.ToString(hash));
                entry.FinishScan();
            }
            BenchmarkRunner.Run<SongBenchmarks>();
        }
    }

    [MemoryDiagnoser]
    public class SongBenchmarks
    {
        private Song.Song? song = null;
        private SongEntry.SongEntry? entry = null;
        private string dir = "E:\\Documents\\My Games\\Clone Hero\\CH Songs\\Charter Application [Sonicfind]\\Mutsuhiko Izumi - L.A.RIDER (Long Version) [Sonicfind]";
        private string ini = string.Empty;
        private DateTime iniLastWrite;

        [GlobalSetup]
        public void Setup()
        {
            ini = Path.Combine(dir, "song.ini");
            iniLastWrite = File.GetLastWriteTime(ini);
        }

        [Benchmark]
        public void Load_Midi()
        {
            song = new(dir);
            song.Load_Ini();
            song.Load_Midi(Path.Combine(dir, "notes.mid"), Encoding.UTF8);
        }

        [Benchmark]
        public void Load_Chart()
        {
            song = new(dir);
            song.Load_Ini();
            song.Load_Chart(Path.Combine(dir, "notes.chart"), true);
        }

        [Benchmark]
        public void Scan_Midi()
        {
            string chart = Path.Combine(dir, "notes.mid");
            entry = new(new(chart, Types.ChartType.MID), File.GetLastWriteTime(chart));
            entry.Load_Ini(ini, iniLastWrite);
            if (entry.Scan(out FrameworkFile? file))
            {
                byte[] hash = file!.HASH_SHA1;
                string h = BitConverter.ToString(hash);
                entry.FinishScan();
            }
        }

        [Benchmark]
        public void Scan_Chart()
        {
            string chart = Path.Combine(dir, "notes.chart");
            entry = new(new(chart, Types.ChartType.CHART), File.GetLastWriteTime(chart));
            entry.Load_Ini(ini, iniLastWrite);
            if (entry.Scan(out FrameworkFile? file))
            {
                byte[] hash = file!.HASH_SHA1;
                string h = BitConverter.ToString(hash);
                entry.FinishScan();
            }
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            song = null;
            entry = null;
        }
    }
}
