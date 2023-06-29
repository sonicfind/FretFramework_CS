using Framework.Hashes;
namespace Framework.Library
{
    public class SongLibrary
    {
        public Dictionary<SHA1Wrapper, List<SongEntry.SongEntry>> entries = new();

        public int Count
        {
            get
            {
                int count = 0;
                foreach(var node in entries)
                    count += node.Value.Count;
                return count;
            }
        }

        public Dictionary<SHA1Wrapper, List<SongEntry.SongEntry>>.Enumerator GetEnumerator() => entries.GetEnumerator();
    }
}
