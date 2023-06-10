using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.DotChartValues.Drums
{
    public class Drums4_ProOutline : IScannableFromDotChart
    {
        public static bool IsValid(nuint lane)
        {
            return lane < 5;
        }
    }
}
