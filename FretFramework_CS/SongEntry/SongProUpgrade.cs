using Framework.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry
{
    public class SongProUpgrade
    {
        public DateTime UpgradeLastWrite { get; protected set; }

        private readonly CONFile? conFile;
        public int UpgradeMidiIndex { get; private set; }
        public string UpgradeMidiPath { get; private set; } = string.Empty;

        public SongProUpgrade(CONFile conFile, int index, DateTime lastWrite)
        {
            this.conFile = conFile;
            UpgradeMidiIndex = index;
            UpgradeLastWrite = lastWrite;
        }

        public SongProUpgrade(FileInfo info)
        {
            UpgradeMidiPath = info.FullName;
            UpgradeLastWrite = info.LastWriteTime;
        }

        public void WriteToCache(BinaryWriter writer)
        {
            writer.Write(UpgradeLastWrite.ToBinary());
        }

        public FrameworkFile GetUpgradeMidi()
        {
            if (conFile != null)
                return new FrameworkFile_Pointer(conFile!.LoadSubFile(UpgradeMidiIndex)!, true);
            return new FrameworkFile_Alloc(UpgradeMidiPath);
        }
    }
}
