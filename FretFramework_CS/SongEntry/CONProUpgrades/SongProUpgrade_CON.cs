using Framework.Serialization.XboxSTFS;
using Framework.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.CONProUpgrades
{
    public class SongProUpgrade_CON : SongProUpgrade
    {
        private readonly CONFile? conFile;
        public int UpgradeMidiIndex { get; private set; }

        public SongProUpgrade_CON(BinaryReader reader, CONFile conFile) : base(DateTime.FromBinary(reader.ReadInt32()))
        {
            this.conFile = conFile;
            UpgradeMidiIndex = reader.ReadInt32();
        }

        public SongProUpgrade_CON(CONFile conFile, string shortName)
        {
            this.conFile = conFile;
            UpgradeMidiIndex = conFile.GetFileIndex($"songs_upgrades/{shortName}_plus.mid");
            UpgradeLastWrite = DateTime.FromBinary(conFile[UpgradeMidiIndex].LastWrite);
        }

        public bool Validate(string shortName)
        {
            string path = $"song_upgrades/{shortName}_plus.mid";
            FileListing listing = conFile![UpgradeMidiIndex];
            if (listing == null || listing.Filename != path)
            {
                UpgradeMidiIndex = conFile.GetFileIndex(path);
                if (UpgradeMidiIndex == -1)
                    return false;
                listing = conFile[UpgradeMidiIndex];
            }
            return listing.LastWrite == UpgradeLastWrite.Ticks;
        }

        public override void WriteToCache(BinaryWriter writer)
        {
            base.WriteToCache(writer);
            writer.Write(UpgradeMidiIndex);
        }

        public override FrameworkFile GetUpgradeMidi()
        {
            return new FrameworkFile_Pointer(conFile!.LoadSubFile(UpgradeMidiIndex)!, true);
        }
    }
}
