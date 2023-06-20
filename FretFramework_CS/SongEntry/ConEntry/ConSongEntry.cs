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
    public class CONEntry : CONEntry_Base
    {
        private CONFile conFile;
        private int midiIndex = -1;
        private int moggIndex = -1;
        private int miloIndex = -1;
        private int imgIndex = -1;

        public CONEntry(CONFile conFile, DTAFileNode dta) : base(dta)
        {
            this.conFile = conFile;
            if (MidiFile == string.Empty)
                MidiFile = location + ".mid";

            midiIndex = conFile.GetFileIndex(MidiFile);
            if (midiIndex == -1)
                throw new Exception($"Required midi file '{MidiFile}' was not located");
            moggIndex = conFile.GetFileIndex(location + ".mogg");

            string genPAth = $"songs/{dta.NodeName}/gen/{dta.NodeName}";
            miloIndex = conFile.GetFileIndex(genPAth + ".milo_xbox");
            imgIndex = conFile.GetFileIndex(genPAth + "_keep.png_xbox");
            Directory = conFile.Filename;
        }

        public override void FinishScan()
        {

        }

        public override FrameworkFile LoadMidiFile()
        {
            return new FrameworkFile_Pointer(conFile.LoadSubFile(midiIndex)!, true);
        }

        public override FrameworkFile LoadMoggFile()
        {
            if (UsingUpdateMogg)
                return base.LoadMoggFile();
            return new FrameworkFile_Pointer(conFile.LoadSubFile(moggIndex)!, true);
        }

        public override FrameworkFile? LoadMiloFile()
        {
            if (UsingUpdateMilo)
                return base.LoadMiloFile();
            if (miloIndex != -1)
                return new FrameworkFile_Pointer(conFile.LoadSubFile(miloIndex)!, true);
            return null;
        }

        public override FrameworkFile? LoadImgFile()
        {
            if (AlternatePath)
                return base.LoadImgFile();
            if (imgIndex != -1)
                return new FrameworkFile_Pointer(conFile.LoadSubFile(imgIndex)!, true);
            return null;
        }

        public override bool IsMoggUnencrypted()
        {
            return conFile.IsMoggUnencrypted(moggIndex);
        }
    }
}
