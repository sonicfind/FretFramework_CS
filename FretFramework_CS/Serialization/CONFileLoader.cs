﻿using Framework.Types;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Serialization
{
    public class FileListing
    {
        public string Filename { get; private set; } = string.Empty;
        public byte Flags { get; init; }
        public int NumBlocks { get; init; }
        public int FirstBlock { get; init; }
        public short PathIndex { get; init; }
        public int Size { get; init; }
        public int LastWrite { get; init; }

        public unsafe FileListing(byte* data)
        {
            Filename = Encoding.UTF8.GetString(data, 0x28).TrimEnd('\0');
            Flags = data[0x28];

            NumBlocks = BitConverter.ToInt32(new byte[4] { data[0x29], data[0x2A], data[0x2B], 0x00 });
            FirstBlock = BitConverter.ToInt32(new byte[4] { data[0x2F], data[0x30], data[0x31], 0x00 });
            PathIndex = BitConverter.ToInt16(new byte[2] { data[0x33], data[0x32] });
            Size = BitConverter.ToInt32(new byte[4] { data[0x37], data[0x36], data[0x35], data[0x34] });
            LastWrite = BitConverter.ToInt32(new byte[4] { data[0x3B], data[0x3A], data[0x39], data[0x38] });
        }

        public void SetParentDirectory(string parentDirectory)
        {
            Filename = parentDirectory + "/" + Filename;
        }

        public FileListing() { }

        public override string ToString() => $"STFS File Listing: {Filename}";
        public bool IsDirectory() { return (Flags & 0x80) > 0; }
        public bool IsContiguous() { return (Flags & 0x40) > 0; }
    }

    public unsafe class CONFile
    {
        public string Filename { get; init; }
        private readonly FileStream stream;
        private readonly byte shift = 0;
        private readonly List<FileListing> files = new();
        private readonly object fileLock = new();

        static public CONFile? LoadCON(string filename)
        {
            byte[] buffer = new byte[4];
            using FileStream stream = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (stream.Read(buffer) != 4)
                return null;

            string tag = Encoding.Default.GetString(buffer, 0, buffer.Length);
            if (tag != "CON " && tag != "LIVE" && tag != "PIRS")
                return null;

            stream.Seek(0x0340, SeekOrigin.Begin);
            if (stream.Read(buffer) != 4)
                return null;

            byte shift = 0;
            int entryID = buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3];
            if ((entryID + 0xFFF & 0xF000) >> 0xC != 0xB)
                shift = 1;

            stream.Seek(0x37C, SeekOrigin.Begin);
            if (stream.Read(buffer, 0, 2) != 2)
                return null;

            int length = 0x1000 * (buffer[0] << 8 | buffer[1]);

            stream.Seek(0x37E, SeekOrigin.Begin);
            if (stream.Read(buffer, 0, 3) != 3)
                return null;

            int firstBlock = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
            try
            {
                
                return new(filename, shift, firstBlock, length);
            }
            catch
            {
                return null;
            }
        }

        private CONFile(string filename, byte shift, int firstBlock, int length)
        {
            Filename = filename;
            stream = new(filename, FileMode.Open, FileAccess.Read);
            this.shift = shift;
            ParseFileList(firstBlock, length);
        }

        ~CONFile()
        {
            stream.Dispose();
        }

        private void ParseFileList(int firstBlock, int length)
        {
            using PointerHandler fileListingBuffer = ReadContiguousBlocks(firstBlock, length);
            byte* ptr = fileListingBuffer.Data;
            for (int i = 0; i < length; i += 0x40)
            {
                FileListing listing = new(ptr + i);
                if (listing.Filename.Length == 0)
                    break;

                if (listing.PathIndex != -1)
                    listing.SetParentDirectory(files[listing.PathIndex].Filename);
                files.Add(listing);
            }
        }

        public FileListing this[int index] { get { return files[index]; } }
        public FileListing? this[string filename]
        {
            get
            {
                for (int i = 0; i < files.Count; ++i)
                {
                    FileListing listing = files[i];
                    if (filename == listing.Filename)
                        return listing;
                }
                return null;
            }
        }

        public int GetMoggVersion(FileListing listing)
        {
            Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");

            long blockLocation = 0xC000 + (long)CalculateBlockNum(listing.FirstBlock) * 0x1000;

            lock (fileLock)
            {
                if (stream.Seek(blockLocation, SeekOrigin.Begin) != blockLocation)
                    throw new Exception("Seek error in CON-like subfile for Mogg");

                byte[] bytes = new byte[4];
                stream.Read(bytes, 0, 4);
                return BinaryPrimitives.ReadInt32LittleEndian(bytes);
            }
        }

        public PointerHandler LoadSubFile(FileListing listing)
        {
            Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");
            try
            {
                if (listing.IsContiguous())
                    return ReadContiguousBlocks(listing.FirstBlock, listing.Size);
                else
                    return ReadSplitBlocks(listing.FirstBlock, listing.Size);
            }
            catch (Exception e)
            {
                throw new Exception(Filename + ": " + e.Message);
            }
        }

        internal const int BYTES_PER_BLOCK = 0x1000;
        internal const int BLOCKS_PER_SECTION = 170;
        internal const int BYTES_PER_SECTION = BLOCKS_PER_SECTION * BYTES_PER_BLOCK;
        internal const int NUM_BLOCKS_SQUARED = BLOCKS_PER_SECTION * BLOCKS_PER_SECTION;

        private PointerHandler ReadContiguousBlocks(int blockNum, int fileSize)
        {
            PointerHandler ptr = new(fileSize);
            byte* data = ptr.Data;
            long skipVal = BYTES_PER_BLOCK << shift;
            int threshold = blockNum - blockNum % NUM_BLOCKS_SQUARED + NUM_BLOCKS_SQUARED;
            int numBlocks = BLOCKS_PER_SECTION - blockNum % BLOCKS_PER_SECTION;
            int readSize = BYTES_PER_BLOCK * numBlocks;
            int offset = 0;

            lock (fileLock)
            {
                stream.Seek(0xC000 + (long)CalculateBlockNum(blockNum) * BYTES_PER_BLOCK, SeekOrigin.Begin);
                while (true)
                {
                    if (readSize > fileSize - offset)
                        readSize = fileSize - offset;

                    if (stream.Read(new Span<byte>(data + offset, readSize)) != readSize)
                        throw new Exception("Read error in CON-like subfile - Type: Contiguous");

                    offset += readSize;
                    if (offset == fileSize)
                        break;

                    blockNum += numBlocks;
                    numBlocks = BLOCKS_PER_SECTION;
                    readSize = BYTES_PER_SECTION;

                    int seekCount = 1;
                    if (blockNum == BLOCKS_PER_SECTION)
                        seekCount = 2;
                    else if (blockNum == threshold)
                    {
                        if (blockNum == NUM_BLOCKS_SQUARED)
                            seekCount = 2;
                        ++seekCount;
                        threshold += NUM_BLOCKS_SQUARED;
                    }

                    stream.Seek(seekCount * skipVal, SeekOrigin.Current);
                }
            }
            return ptr;
        }

        private PointerHandler ReadSplitBlocks(int blockNum, int fileSize)
        {
            PointerHandler ptr = new(fileSize);
            byte* data = ptr.Data;
            byte[] buffer = new byte[3];

            int offset = 0;
            while (true)
            {
                int block = CalculateBlockNum(blockNum);
                long blockLocation = 0xC000 + (long)block * BYTES_PER_BLOCK;
                int readSize = BYTES_PER_BLOCK;
                if (readSize > fileSize - offset)
                    readSize = fileSize - offset;

                lock (fileLock)
                {
                    stream.Seek(blockLocation, SeekOrigin.Begin);
                    if (stream.Read(new Span<byte>(data + offset, readSize)) != readSize)
                        throw new Exception("Pre-Read error in CON-like subfile - Type: Split");
                }

                offset += readSize;
                if (offset == fileSize)
                    break;

                long hashlocation = blockLocation - ((long)(blockNum % BLOCKS_PER_SECTION) * 4072 + 4075);
                lock (fileLock)
                {
                    stream.Seek(hashlocation, SeekOrigin.Begin);
                    if (stream.Read(buffer, 0, 3) != 3)
                        throw new Exception("Post-Read error in CON-like subfile - Type: Split");
                }

                blockNum = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
            }
            return ptr;
        }

        private int CalculateBlockNum(int blocknum)
        {
            int blockAdjust = 0;
            if (blocknum >= BLOCKS_PER_SECTION)
            {
                blockAdjust += blocknum / BLOCKS_PER_SECTION + 1 << shift;
                if (blocknum >= NUM_BLOCKS_SQUARED)
                    blockAdjust += blocknum / NUM_BLOCKS_SQUARED + 1 << shift;
            }
            return blockAdjust + blocknum;
        }
    }
}
