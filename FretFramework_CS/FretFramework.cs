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
            string? directory;
            do
            {
                Console.Write("Drag and drop song directory: ");
                directory = Console.ReadLine();
            }
            while (directory == null);
            directory = directory.Replace("\"", "");

            string? cacheFileDirectory;
            do
            {
                Console.Write("Drag and drop a cache file directory: ");
                cacheFileDirectory = Console.ReadLine();
            }
            while (cacheFileDirectory == null);
            cacheFileDirectory = cacheFileDirectory.Replace("\"", "");

            Stopwatch stopwatch = Stopwatch.StartNew();
            SongLibrary library = SongCache.ScanDirectories(new() { directory }, cacheFileDirectory, true);
            stopwatch.Stop();
            Console.WriteLine($"Time Spent: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Song Count: {library.Count}");

            //BenchmarkRunner.Run<SongBenchmarks>(); // Song directory MUST be hardcoded to run properly
        }
    }

    [MemoryDiagnoser]
    public class SongBenchmarks
    {
        SongLibrary? library;
        private readonly List<string> directories = new() { "E:\\Documents\\My Games\\Clone Hero\\CH Songs" };

        [Benchmark]
        public void Scan_Directory()
        {
            library = SongCache.ScanDirectories(directories, "E:\\Documents\\My Games\\Clone Hero\\CH Songs", false);
        }

        [IterationCleanup]
        public void IterationCleanup() {
            library = null;
        }
    }
}
