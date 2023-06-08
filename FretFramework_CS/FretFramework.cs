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
            Song.Song song = new();
            song.Load_Midi("E:\\Documents\\My Games\\Clone Hero\\CH Songs\\CrashTest5.5\\notes.mid", Encoding.UTF8);
            GC.Collect();
            //song = new();
            //song.Load_Chart("E:\\Documents\\My Games\\Clone Hero\\CH Songs\\CrashTest5.5\\notees.chart", true);
            BenchmarkRunner.Run<SongBenchmarks>();
        }
    }

    [MemoryDiagnoser]
    public class SongBenchmarks
    {
        private Song.Song? song;
        [Benchmark]
        public void Load_Midi()
        {
            song = new();
            song.Load_Midi("E:\\Documents\\My Games\\Clone Hero\\CH Songs\\CrashTest5.5\\notes.mid", Encoding.UTF8);
        }

        [Benchmark]
        public void Load_Chart()
        {
            song = new();
            song.Load_Chart("E:\\Documents\\My Games\\Clone Hero\\CH Songs\\CrashTest5.5\\notees.chart", true);
        }

        [IterationCleanup]
        public void Cleanup()
        {
            song = null;
            GC.Collect();
        }
    }
}
