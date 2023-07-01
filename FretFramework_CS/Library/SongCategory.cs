using Framework.FlatMaps;
using Framework.SongEntry;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Framework.Library
{
    public interface IEntryAddable
    {
        public void Add(SongEntry.SongEntry entry);
    }

    public class CategoryNode : IEntryAddable
    {
        private readonly List<SongEntry.SongEntry> entries = new();
        private readonly EntryComparer comparer;

        protected CategoryNode(EntryComparer comparer)
        {
            this.comparer = comparer;
        }

        public void Add(SongEntry.SongEntry entry)
        {
            int index = entries.BinarySearch(entry, comparer);
            entries.Insert(~index, entry);
        }

        public List<SongEntry.SongEntry>.Enumerator GetEnumerator() => entries.GetEnumerator();
    }

    public class TitleNode : CategoryNode
    {
        static readonly EntryComparer TitleComparer = new(SongAttribute.TITLE);
        public TitleNode() : base(TitleComparer) {}
    }

    public class ArtistNode : CategoryNode
    {
        static readonly EntryComparer ArtistComparer = new(SongAttribute.ARTIST);
        public ArtistNode() : base(ArtistComparer) { }
    }

    public class AlbumNode : CategoryNode
    {
        static readonly EntryComparer AlbumComparer = new(SongAttribute.ALBUM);
        public AlbumNode() : base(AlbumComparer) { }
    }

    public class GenreNode : CategoryNode
    {
        static readonly EntryComparer GenreComparer = new(SongAttribute.GENRE);
        public GenreNode() : base(GenreComparer) { }
    }

    public class YearNode : CategoryNode
    {
        static readonly EntryComparer YearComparer = new(SongAttribute.YEAR);
        public YearNode() : base(YearComparer) { }
    }

    public class CharterNode : CategoryNode
    {
        static readonly EntryComparer CharterComparer = new(SongAttribute.CHARTER);
        public CharterNode() : base(CharterComparer) { }
    }

    public class PlaylistNode : CategoryNode
    {
        static readonly EntryComparer PlaylistComparer = new(SongAttribute.PLAYLIST);
        public PlaylistNode() : base(PlaylistComparer) { }
    }

    public class SourceNode : CategoryNode
    {
        static readonly EntryComparer SourceComparer = new(SongAttribute.SOURCE);
        public SourceNode() : base(SourceComparer) { }
    }

    public abstract class SongCategory<Element> : IEntryAddable
        where Element : IEntryAddable, new()
    {
        protected readonly object elementLock = new();
        protected readonly SimpleFlatMap<SortString, Element> elements = new();
        public abstract void Add(SongEntry.SongEntry entry);

        public SimpleFlatMap<SortString, Element>.Enumerator GetEnumerator() { return elements.GetEnumerator(); }
    }

    public class TitleCategory : IEntryAddable
    {
        private readonly object elementLock = new();
        private readonly SimpleFlatMap<char, TitleNode> elements = new();
        public void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock) elements[entry.Name.SortStr[0]].Add(entry);
        }
    }

    public class ArtistCategory : SongCategory<ArtistNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock) elements[entry.Artist].Add(entry);
        }
    }

    public class AlbumCategory : SongCategory<AlbumNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock) elements[entry.Album].Add(entry);
        }
    }

    public class GenreCategory : SongCategory<GenreNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Genre].Add(entry);
        }
    }

    public class YearCategory : SongCategory<YearNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Year].Add(entry);
        }
    }

    public class CharterCategory : SongCategory<CharterNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Charter].Add(entry);
        }
    }

    public class PlaylistCategory : SongCategory<PlaylistNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Playlist].Add(entry);
        }
    }

    public class SourceCategory : SongCategory<SourceNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Source].Add(entry);
        }
    }

    public class ArtistAlbumCategory : SongCategory<AlbumCategory>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Artist].Add(entry);
        }
    }
}
