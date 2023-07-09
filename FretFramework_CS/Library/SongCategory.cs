using Framework.FlatMaps;
using Framework.Library.CacheNodes;
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
        protected readonly FlatMap<Key, Element> elements = new();

        public abstract void Add(SongEntry.SongEntry entry);
        protected void Add(Key key, SongEntry.SongEntry entry) { lock (elementLock) elements[key].Add(entry); }

        public FlatMap<Key, Element>.Enumerator GetEnumerator() { return elements.GetEnumerator(); }
    }

    public class TitleCategory : SongCategory<char, TitleNode>
    {
        public override void Add(SongEntry.SongEntry entry) { Add(entry.Name.SortStr[0], entry); }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongEntry.SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> titles = new();
            int index = -1;
            foreach (var element in elements)
            {
                foreach (var entry in element.obj)
                {
                    string title = entry.Name.Str;
                    if (index == -1 || titles[index] != title)
                    {
                        titles.Add(title);
                        index++;
                    }

                    CategoryCacheWriteNode node = nodes[entry] = new();
                    node.title = index;
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(index + 1);
            foreach (string title in titles)
                writer.Write(title);

            fileWriter.Write((int)ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
        }
    }

    public class ArtistCategory : SongCategory<SortString, ArtistNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock) elements[entry.Artist].Add(entry);
        }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongEntry.SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> artists = new();
            int index = -1;
            foreach (var element in elements)
            {
                foreach (var entry in element.obj)
                {
                    string artist = entry.Artist.Str;
                    if (index == -1 || artists[index] != artist)
                    {
                        artists.Add(artist);
                        index++;
                    }
                    nodes[entry].artist = index;
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(index + 1);
            foreach (string artist in artists)
                writer.Write(artist);

            fileWriter.Write((int)ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
        }
    }

    public class AlbumCategory : SongCategory<SortString, AlbumNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock) elements[entry.Album].Add(entry);
        }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongEntry.SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> albums = new();
            int index = -1;
            foreach (var element in elements)
            {
                foreach (var entry in element.obj)
                {
                    string album = entry.Album.Str;
                    if (index == -1 || albums[index] != album)
                    {
                        albums.Add(album);
                        index++;
                    }
                    nodes[entry].album = index;
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(index + 1);
            foreach (string album in albums)
                writer.Write(album);

            fileWriter.Write((int)ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
        }
    }

    public class GenreCategory : SongCategory<SortString, GenreNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Genre].Add(entry);
        }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongEntry.SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> genres = new();
            int index = -1;
            foreach (var element in elements)
            {
                foreach (var entry in element.obj)
                {
                    string genre = entry.Genre.Str;
                    if (index == -1 || genres[index] != genre)
                    {
                        genres.Add(genre);
                        index++;
                    }
                    nodes[entry].genre = index;
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(index + 1);
            foreach (string genre in genres)
                writer.Write(genre);

            fileWriter.Write((int)ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
        }
    }

    public class YearCategory : SongCategory<SortString, YearNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Year].Add(entry);
        }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongEntry.SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> years = new();
            int index = -1;
            foreach (var element in elements)
            {
                foreach (var entry in element.obj)
                {
                    string year = entry.Year.Str;
                    if (index == -1 || years[index] != year)
                    {
                        years.Add(year);
                        index++;
                    }
                    nodes[entry].year = index;
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(index + 1);
            foreach (string year in years)
                writer.Write(year);

            fileWriter.Write((int)ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
        }
    }

    public class CharterCategory : SongCategory<SortString, CharterNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Charter].Add(entry);
        }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongEntry.SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> charters = new();
            int index = -1;
            foreach (var element in elements)
            {
                foreach (var entry in element.obj)
                {
                    string charter = entry.Charter.Str;
                    if (index == -1 || charters[index] != charter)
                    {
                        charters.Add(charter);
                        index++;
                    }
                    nodes[entry].charter = index;
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(index + 1);
            foreach (string charter in charters)
                writer.Write(charter);

            fileWriter.Write((int)ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
        }
    }

    public class PlaylistCategory : SongCategory<SortString, PlaylistNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Playlist].Add(entry);
        }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongEntry.SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> playlists = new();
            int index = -1;
            foreach (var element in elements)
            {
                foreach (var entry in element.obj)
                {
                    string playlist = entry.Playlist.Str;
                    if (index == -1 || playlists[index] != playlist)
                    {
                        playlists.Add(playlist);
                        index++;
                    }
                    nodes[entry].playlist = index;
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(index + 1);
            foreach (string playlist in playlists)
                writer.Write(playlist);

            fileWriter.Write((int)ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
        }
    }

    public class SourceCategory : SongCategory<SortString, SourceNode>
    {
        public override void Add(SongEntry.SongEntry entry)
        {
            lock (elementLock)
                elements[entry.Source].Add(entry);
        }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongEntry.SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> sources = new();
            int index = -1;
            foreach (var element in elements)
            {
                foreach (var entry in element.obj)
                {
                    string source = entry.Source.Str;
                    if (index == -1 || sources[index] != source)
                    {
                        sources.Add(source);
                        index++;
                    }
                    nodes[entry].source = index;
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(index + 1);
            foreach (string source in sources)
                writer.Write(source);
            
            fileWriter.Write((int)ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
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
