using BenchmarkDotNet.Attributes;
using Framework.Hashes;
using Framework.Serialization;
using Framework.Serialization.XboxSTFS;
using Framework.Types;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.ConEntry
{
    public class CONEntry_Extracted : CONEntry_Base
    {
        public CONEntry_Extracted(string folder, DTAFileNode dta) : base(dta)
        {
            Directory = folder;
            string dir = Path.Combine(folder, location);
            string file = Path.Combine(dir, location);
            MidiFile = file + ".mid";
            MoggPath = file + ".mogg";

            file = Path.Combine(dir, $"gen{location}");
            string path = file + ".milo_xbox";
            if (File.Exists(path))
                MiloPath = path;

            path = file + "_keep.png_xbox";
            if (File.Exists(path))
                ImagePath = path;
            location = dir;
        }

        public override void FinishScan()
        {

        }

        public override FrameworkFile LoadMidiFile()
        {
            return new FrameworkFile_Alloc(MidiFile);
        }
    }
}
