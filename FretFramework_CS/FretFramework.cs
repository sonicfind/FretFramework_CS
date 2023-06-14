using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using CommandLine;
using Framework.Library;
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
            string? directory = string.Empty;
            do
            {
                Console.Write("Drag and drop song directory: ");
                directory = Console.ReadLine();
            }
            while (directory == null);
            directory = directory.Replace("\"", "");

            SongLibrary library = new();
            Stopwatch stopwatch = Stopwatch.StartNew();
            library.RunFullScan(new() { directory });
            stopwatch.Stop();
            Console.WriteLine($"Time Spent: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Song Count: {library.Count}");
            BenchmarkRunner.Run<SongBenchmarks>();
        }
    }

    [MemoryDiagnoser]
    public class SongBenchmarks
    {
        private SongLibrary? library;
        private readonly List<string> directories = new() { "E:\\Documents\\My Games\\Clone Hero\\CH Songs" };

        [IterationSetup]
        public void Setup()
        {
            library = new SongLibrary();
        }

        [Benchmark]
        public void Scan_Directory()
        {
            library!.RunFullScan(directories);
        }
    }
}
