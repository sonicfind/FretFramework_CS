using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.DotChartValues.Guitar
{
    public class FiveFretOutline : IScannableFromDotChart
    {
        public static bool IsValid(nuint lane)
        {
            return lane < 5 || lane == 7;
        }
    }
}
