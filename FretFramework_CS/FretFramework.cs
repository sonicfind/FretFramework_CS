using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using CommandLine;
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
            SongEntry.SongEntry entry = new();
            entry.Scan_Midi("E:\\Documents\\My Games\\Clone Hero\\CH Songs\\Charter Application [Sonicfind]\\Mutsuhiko Izumi - L.A.RIDER (Long Version) [Sonicfind]\\notes.mid");
            entry = new();
            entry.Scan_Chart("E:\\Documents\\My Games\\Clone Hero\\CH Songs\\Charter Application [Sonicfind]\\Mutsuhiko Izumi - L.A.RIDER (Long Version) [Sonicfind]\\notes.chart");
            BenchmarkRunner.Run<SongBenchmarks>();
        }
    }

    [MemoryDiagnoser]
    public class SongBenchmarks
    {
        private Song.Song? song = null;
        private SongEntry.SongEntry? entry = null;

        [Benchmark]
        public void Load_Midi()
        {
            song = new();
            song.Load_Midi("E:\\Documents\\My Games\\Clone Hero\\CH Songs\\Charter Application [Sonicfind]\\Mutsuhiko Izumi - L.A.RIDER (Long Version) [Sonicfind]\\notes.mid", Encoding.UTF8);
        }

        [Benchmark]
        public void Load_Chart()
        {
            song = new();
            song.Load_Chart("E:\\Documents\\My Games\\Clone Hero\\CH Songs\\Charter Application [Sonicfind]\\Mutsuhiko Izumi - L.A.RIDER (Long Version) [Sonicfind]\\notes.chart", true);
        }

        [Benchmark]
        public void Scan_Midi()
        {
            entry = new();
            entry.Scan_Midi("E:\\Documents\\My Games\\Clone Hero\\CH Songs\\Charter Application [Sonicfind]\\Mutsuhiko Izumi - L.A.RIDER (Long Version) [Sonicfind]\\notes.mid");
        }

        [Benchmark]
        public void Scan_Chart()
        {
            entry = new();
            entry.Scan_Chart("E:\\Documents\\My Games\\Clone Hero\\CH Songs\\Charter Application [Sonicfind]\\Mutsuhiko Izumi - L.A.RIDER (Long Version) [Sonicfind]\\notes.chart");
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            song = null;
            entry = null;
        }
    }
}
