using Framework.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.CONProUpgrades
{
    public class SongProUpgrade_Extracted : SongProUpgrade
    {
        public string UpgradeMidiPath { get; private set; } = string.Empty;

        public SongProUpgrade_Extracted(BinaryReader reader, string midiPath) : base(DateTime.FromBinary(reader.ReadInt64()))
        {
            UpgradeMidiPath = midiPath;
        }

        public SongProUpgrade_Extracted(string folder, string shortName)
        {
            UpgradeMidiPath = Path.Combine(folder, $"{shortName}_plus.mid");
            FileInfo info = new(UpgradeMidiPath);
            if (!info.Exists)
                throw new FileNotFoundException(UpgradeMidiPath);
            UpgradeLastWrite = info.LastWriteTime;
        }

        public bool Validate()
        {
            FileInfo info = new(UpgradeMidiPath);
            return info.Exists && info.LastWriteTime.Ticks == UpgradeLastWrite.Ticks;
        }

        public override FrameworkFile GetUpgradeMidi()
        {
            return new FrameworkFile_Alloc(UpgradeMidiPath);
        }
    }
}
