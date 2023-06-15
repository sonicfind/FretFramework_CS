using Framework.Serialization;
using Framework.Song.Tracks.Instrument;
using Framework.Song.Tracks.Notes.Interfaces;
using Framework.SongEntry.DotChartValues;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan
{
    public static class DotChart_Scanner
    {
        public static bool Scan<T>(ref ScanValues scan, ChartFileReader reader)
            where T : IScannableFromDotChart
        {
            int index = reader.Difficulty;
            if (scan[index])
                return false;

            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE && T.IsValid(reader.ExtractLaneAndSustain().Item1))
                {
                    scan.Set(index);
                    return false;
                }
                reader.NextEvent();
            }
            return true;
        }
    }
}
