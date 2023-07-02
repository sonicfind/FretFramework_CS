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

    public abstract class SongCategory<Key, Element> : IEntryAddable
        where Element : IEntryAddable, new()
        where Key : IComparable<Key>, IEquatable<Key>
    {
        protected readonly object elementLock = new();
        protected readonly SimpleFlatMap<Key, Element> elements = new();

        public abstract void Add(SongEntry.SongEntry entry);
        protected void Add(Key key, SongEntry.SongEntry entry) { lock (elementLock) elements[key].Add(entry); }

        public SimpleFlatMap<Key, Element>.Enumerator GetEnumerator() { return elements.GetEnumerator(); }
    }

    public class TitleCategory : SongCategory<char, TitleNode>
    {
        public override void Add(SongEntry.SongEntry entry) { Add(entry.Name.SortStr[0], entry); }
    }

    public class ArtistCategory : SongCategory<SortString, ArtistNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock) elements[entry.Artist].Add(entry);
        }
    }

    public class AlbumCategory : SongCategory<SortString, AlbumNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock) elements[entry.Album].Add(entry);
        }
    }

    public class GenreCategory : SongCategory<SortString, GenreNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Genre].Add(entry);
        }
    }

    public class YearCategory : SongCategory<SortString, YearNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Year].Add(entry);
        }
    }

    public class CharterCategory : SongCategory<SortString, CharterNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Charter].Add(entry);
        }
    }

    public class PlaylistCategory : SongCategory<SortString, PlaylistNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Playlist].Add(entry);
        }
    }

    public class SourceCategory : SongCategory<SortString, SourceNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Source].Add(entry);
        }
    }

    public class ArtistAlbumCategory : SongCategory<SortString, AlbumCategory>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Artist].Add(entry);
        }
    }
}
