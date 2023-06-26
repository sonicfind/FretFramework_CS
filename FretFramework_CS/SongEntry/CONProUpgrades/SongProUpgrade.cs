using Framework.Serialization;
using Framework.Serialization.XboxSTFS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.CONProUpgrades
{
    public abstract class SongProUpgrade
    {
        public DateTime UpgradeLastWrite { get; protected set; }

        protected SongProUpgrade() { }
        protected SongProUpgrade(DateTime upgradeLastWrite) { UpgradeLastWrite = upgradeLastWrite; }

        public virtual void WriteToCache(BinaryWriter writer) { writer.Write(UpgradeLastWrite.ToBinary()); }
        public abstract FrameworkFile GetUpgradeMidi();
    }
}
