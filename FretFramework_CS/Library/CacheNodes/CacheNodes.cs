using Framework.Serialization;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Library.CacheNodes
{
    public class CategoryCacheWriteNode
    {
        public int title;
        public int artist;
        public int album;
        public int genre;
        public int year;
        public int charter;
        public int playlist;
        public int source;
    }

    public class CategoryCacheStrings
    {
        public string[] titles = Array.Empty<string>();
        public string[] artists = Array.Empty<string>();
        public string[] albums = Array.Empty<string>();
        public string[] genres = Array.Empty<string>();
        public string[] years = Array.Empty<string>();
        public string[] charters = Array.Empty<string>();
        public string[] playlists = Array.Empty<string>();
        public string[] sources = Array.Empty<string>();

        public CategoryCacheStrings(BinaryFileReader reader, bool multithreaded)
        {
            static string[] ReadStrings(BinaryFileReader reader, bool multithreaded)
            {
                int count = reader.ReadInt32();
                string[] strings = new string[count];
                for (int i = 0; i < count; ++i)
                    strings[i] = reader.ReadLEBString();

                if (multithreaded)
                    reader.Dispose();
                return strings;
            }

            if (multithreaded)
            {
                Task[] tasks = new Task[8];

                int bytes = reader.ReadInt32();
                BinaryFileReader titleReader = reader.CreateReaderFromCurrentPosition(bytes);
                tasks[0] = Task.Run(() => { titles = ReadStrings(titleReader, true); });

                bytes = reader.ReadInt32();
                BinaryFileReader artistReader = reader.CreateReaderFromCurrentPosition(bytes);
                tasks[1] = Task.Run(() => { artists = ReadStrings(artistReader, true); });

                bytes = reader.ReadInt32();
                BinaryFileReader albumReader = reader.CreateReaderFromCurrentPosition(bytes);
                tasks[2] = Task.Run(() => { albums = ReadStrings(albumReader, true); });

                bytes = reader.ReadInt32();
                BinaryFileReader genreReader = reader.CreateReaderFromCurrentPosition(bytes);
                tasks[3] = Task.Run(() => { genres = ReadStrings(genreReader, true); });

                bytes = reader.ReadInt32();
                BinaryFileReader yearReader = reader.CreateReaderFromCurrentPosition(bytes);
                tasks[4] = Task.Run(() => { years = ReadStrings(yearReader, true); });

                bytes = reader.ReadInt32();
                BinaryFileReader charterReader = reader.CreateReaderFromCurrentPosition(bytes);
                tasks[5] = Task.Run(() => { charters = ReadStrings(charterReader, true); });

                bytes = reader.ReadInt32();
                BinaryFileReader playlistReader = reader.CreateReaderFromCurrentPosition(bytes);
                tasks[6] = Task.Run(() => { playlists = ReadStrings(playlistReader, true); });

                bytes = reader.ReadInt32();
                BinaryFileReader sourceReader = reader.CreateReaderFromCurrentPosition(bytes);
                tasks[7] = Task.Run(() => { sources = ReadStrings(sourceReader, true); });
                Task.WaitAll(tasks);
            }
            else
            {
                int bytes = reader.ReadInt32();
                reader.EnterSection(bytes);
                titles = ReadStrings(reader, false);
                reader.ExitSection();

                bytes = reader.ReadInt32();
                reader.EnterSection(bytes);
                artists = ReadStrings(reader, false);
                reader.ExitSection();

                bytes = reader.ReadInt32();
                reader.EnterSection(bytes);
                albums = ReadStrings(reader, false);
                reader.ExitSection();

                bytes = reader.ReadInt32();
                reader.EnterSection(bytes);
                genres = ReadStrings(reader, false);
                reader.ExitSection();

                bytes = reader.ReadInt32();
                reader.EnterSection(bytes);
                years = ReadStrings(reader, false);
                reader.ExitSection();

                bytes = reader.ReadInt32();
                reader.EnterSection(bytes);
                charters = ReadStrings(reader, false);
                reader.ExitSection();

                bytes = reader.ReadInt32();
                reader.EnterSection(bytes);
                playlists = ReadStrings(reader, false);
                reader.ExitSection();

                bytes = reader.ReadInt32();
                reader.EnterSection(bytes);
                sources = ReadStrings(reader, false);
                reader.ExitSection();
            }
        }
    }
}
