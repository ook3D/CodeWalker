/*
    Copyright(c) 2015 Neodymium

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

//shamelessly stolen and mangled


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeWalker.GameFiles
{

    /// <summary>
    /// Represents a resource data reader.
    /// </summary>
    public class ResourceDataReader : DataReader
    {
        public bool IsGen9 = RpfManager.IsGen9;

        private const long SYSTEM_BASE = 0x50000000;
        private const long GRAPHICS_BASE = 0x60000000;

        private Stream systemStream;
        private Stream graphicsStream;

        public RpfResourceFileEntry? FileEntry { get; set; }

        // this is a dictionary that contains all the resource blocks
        // which were read from this resource reader
        public Dictionary<long, IResourceBlock> blockPool = new();
        public Dictionary<long, object> arrayPool = new();

        /// <summary>
        /// Gets the length of the underlying stream.
        /// </summary>
        public override long Length
        {
            get
            {
                return -1;
            }
        }

        /// <summary>
        /// Gets or sets the position within the underlying stream.
        /// </summary>
        public override long Position
        {
            get;
            set;
        }

        /// <summary>
        /// Initializes a new resource data reader for the specified system- and graphics-stream.
        /// </summary>
        public ResourceDataReader(Stream systemStream, Stream graphicsStream, Endianess endianess = Endianess.LittleEndian)
            : base(endianess)
        {
            this.systemStream = systemStream;
            this.graphicsStream = graphicsStream;
        }

        public ResourceDataReader(RpfResourceFileEntry resentry, byte[] data, Endianess endianess = Endianess.LittleEndian)
            : base(endianess)
        {
            FileEntry = resentry;
            var systemSize = resentry.SystemSize;
            var graphicsSize = resentry.GraphicsSize;

            //if (data != null)
            //{
            //    if (systemSize > data.Length)
            //    {
            //        systemSize = data.Length;
            //        graphicsSize = 0;
            //    }
            //    else if ((systemSize + graphicsSize) > data.Length)
            //    {
            //        graphicsSize = data.Length - systemSize;
            //    }
            //}

            this.systemStream = new MemoryStream(data, 0, systemSize);
            this.graphicsStream = new MemoryStream(data, systemSize, graphicsSize);
            Position = 0x50000000;
        }

        public ResourceDataReader(int systemSize, int graphicsSize, byte[] data, Endianess endianess = Endianess.LittleEndian)
            : base(endianess)
        {
            this.systemStream = new MemoryStream(data, 0, systemSize);
            this.graphicsStream = new MemoryStream(data, systemSize, graphicsSize);
            Position = 0x50000000;
        }



        /// <summary>
        /// Reads resource data through the shared span-based stream routing.
        /// </summary>
        protected override byte[] ReadFromStream(int count, bool ignoreEndianess = false)
        {
            return base.ReadFromStream(count, ignoreEndianess);
        }

        private Stream GetReadStream(out long addressBase)
        {
            Stream stream;
            if ((Position & SYSTEM_BASE) == SYSTEM_BASE)
            {
                addressBase = SYSTEM_BASE;
                stream = systemStream;
            }
            else if ((Position & GRAPHICS_BASE) == GRAPHICS_BASE)
            {
                addressBase = GRAPHICS_BASE;
                stream = graphicsStream;
            }
            else
            {
                throw new InvalidDataException($"Illegal resource position: 0x{Position:X}.");
            }

            stream.Position = Position & ~addressBase;
            return stream;
        }

        protected override void ReadFromStream(Span<byte> buffer, bool ignoreEndianess = false)
        {
            var stream = GetReadStream(out var addressBase);
            try
            {
                stream.ReadExactly(buffer);
            }
            finally
            {
                Position = stream.Position | addressBase;
            }
            if (!ignoreEndianess && Endianess == Endianess.BigEndian)
            {
                buffer.Reverse();
            }
        }

        protected override async ValueTask ReadFromStreamAsync(Memory<byte> buffer,
            bool ignoreEndianess, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stream = GetReadStream(out var addressBase);
            try
            {
                await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Position = stream.Position | addressBase;
            }
            if (!ignoreEndianess && Endianess == Endianess.BigEndian)
            {
                buffer.Span.Reverse();
            }
        }

        /// <summary>
        /// Reads a block.
        /// </summary>
        public T? ReadBlock<T>(params object[] parameters) where T : IResourceBlock, new()
        {
            var usepool = !typeof(IResourceNoCacheBlock).IsAssignableFrom(typeof(T));
            if (usepool)
            {
                // make sure to return the same object if the same
                // block is read again...
                if (blockPool.TryGetValue(Position, out var block))
                {
                    if (block is T tblk)
                    {
                        Position += block.BlockLength;
                        return tblk;
                    }
                    else
                    {
                        usepool = false;
                    }
                }
            }

            var result = new T();


            // replace with correct type...
            if (result is IResourceXXSystemBlock)
            {
                result = (T)((IResourceXXSystemBlock)result).GetType(this, parameters);
            }

            if (result == null)
            {
                return default(T);
            }

            if (usepool)
            {
                blockPool[Position] = result;
            }

            result.Read(this, parameters);

            return result;
        }

        /// <summary>Reads an embedded block that must be present in the resource.</summary>
        public T ReadRequiredBlock<T>(params object[] parameters) where T : IResourceBlock, new()
        {
            var block = ReadBlock<T>(parameters);
            if (block == null) throw new InvalidDataException($"The required {typeof(T).Name} block is missing or unsupported.");
            return block;
        }

        /// <summary>
        /// Reads a block at a specified position.
        /// </summary>
        public T? ReadBlockAt<T>(ulong position, params object[] parameters) where T : IResourceBlock, new()
        {
            if (position != 0)
            {
                var positionBackup = Position;

                Position = (long)position;
                var result = ReadBlock<T>(parameters);
                Position = positionBackup;

                return result;
            }
            else
            {
                return default(T);
            }
        }

        public T[]? ReadBlocks<T>(ulong[]? pointers) where T : IResourceBlock, new()
        {
            if (pointers == null) return null;
            var count = pointers.Length;
            var items = new T[count];
            for (int i = 0; i < count; i++)
            {
                if (ReadBlockAt<T>(pointers[i]) is { } item) items[i] = item;
            }
            return items;
        }


        public byte[]? ReadBytesAt(ulong position, uint count, bool cache = true)
        {
            long pos = (long)position;
            if ((pos <= 0) || (count == 0)) return null;
            var posbackup = Position;
            Position = pos;
            var result = ReadBytes((int)count);
            Position = posbackup;
            if (cache) arrayPool[(long)position] = result;
            return result;
        }
        public ushort[]? ReadUshortsAt(ulong position, uint count, bool cache = true) =>
            ReadPrimitiveArrayAt<ushort>(position, count, cache);

        public short[]? ReadShortsAt(ulong position, uint count, bool cache = true) =>
            ReadPrimitiveArrayAt<short>(position, count, cache);

        public uint[]? ReadUintsAt(ulong position, uint count, bool cache = true) =>
            ReadPrimitiveArrayAt<uint>(position, count, cache);

        public ulong[]? ReadUlongsAt(ulong position, uint count, bool cache = true) =>
            ReadPrimitiveArrayAt<ulong>(position, count, cache);

        public float[]? ReadFloatsAt(ulong position, uint count, bool cache = true) =>
            ReadPrimitiveArrayAt<float>(position, count, cache);

        private T[]? ReadPrimitiveArrayAt<T>(ulong position, uint count, bool cache) where T : unmanaged
        {
            if (position == 0 || count == 0) return null;

            // Validate the byte length before allocating; no temporary byte array is needed.
            _ = checked((int)count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
            var result = GC.AllocateUninitializedArray<T>((int)count);
            long positionBackup = Position;
            try
            {
                Position = checked((long)position);
                // Match the existing raw array layout: ReadBytesAt did not swap bytes.
                ReadFromStream(MemoryMarshal.AsBytes(result.AsSpan()), true);
            }
            finally
            {
                Position = positionBackup;
            }
            if (cache) arrayPool[(long)position] = result;
            return result;
        }
        public T[]? ReadStructsAt<T>(ulong position, uint count, bool cache = true) where T : struct
        {
            if (position == 0 || count == 0) return null;
            long positionBackup = Position;
            T[] result;
            try
            {
                Position = checked((long)position);
                result = ReadStructs<T>(count);
            }
            finally
            {
                Position = positionBackup;
            }
            if (cache) arrayPool[(long)position] = result;
            return result;
        }

        public T[] ReadStructs<T>(uint count) where T : struct
        {
            if (count == 0) return [];
            if (ResourceStructLayout<T>.CanCopyBytes)
            {
                _ = checked((int)count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
                var result = GC.AllocateUninitializedArray<T>((int)count);
                ReadFromStream(MemoryMarshal.AsBytes(result.AsSpan()), true);
                return result;
            }

            // Marshal each element when its wire representation differs from managed memory.
            _ = checked((int)count * Marshal.SizeOf<T>());
            var converted = new T[(int)count];
            for (int i = 0; i < converted.Length; i++) converted[i] = ReadStruct<T>();
            return converted;
        }

        public T ReadStruct<T>() where T : struct
        {
            if (ResourceStructLayout<T>.CanCopyBytes)
            {
                T result = default;
                ReadFromStream(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1)), true);
                return result;
            }

            byte[] data = ReadBytes(Marshal.SizeOf<T>());
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        public T ReadStructAt<T>(long position) where T : struct
        {
            if (position <= 0) return default;
            long positionBackup = Position;
            try
            {
                Position = position;
                return ReadStruct<T>();
            }
            finally
            {
                Position = positionBackup;
            }
        }

        public string? ReadStringAt(ulong position)
        {
            long newpos = (long)position;
            if ((newpos <= 0)) return null;
            var lastpos = Position;
            Position = newpos;
            var result = ReadString();
            Position = lastpos;
            arrayPool[newpos] = result;
            return result;
        }

    }



    /// <summary>
    /// Represents a resource data writer.
    /// </summary>
    public class ResourceDataWriter : DataWriter
    {
        public bool IsGen9 = false;//this needs to be specifically set by ResourceBuilder

        private const long SYSTEM_BASE = 0x50000000;
        private const long GRAPHICS_BASE = 0x60000000;

        private Stream systemStream;
        private Stream graphicsStream;

        /// <summary>
        /// Gets the length of the underlying stream.
        /// </summary>
        public override long Length
        {
            get
            {
                return -1;
            }
        }

        /// <summary>
        /// Gets or sets the position within the underlying stream.
        /// </summary>
        public override long Position
        {
            get;
            set;
        }

        /// <summary>
        /// Initializes a new resource data reader for the specified system- and graphics-stream.
        /// </summary>
        public ResourceDataWriter(Stream systemStream, Stream graphicsStream, Endianess endianess = Endianess.LittleEndian)
            : base(endianess)
        {
            this.systemStream = systemStream;
            this.graphicsStream = graphicsStream;
        }

        /// <summary>
        /// Writes data to the underlying stream. This is the only method that directly accesses
        /// the data in the underlying stream.
        /// </summary>
        protected override void WriteToStream(ReadOnlySpan<byte> value, bool ignoreEndianess = false)
        {
            Stream stream;
            long addressBase;
            if ((Position & SYSTEM_BASE) == SYSTEM_BASE)
            {
                stream = systemStream;
                addressBase = SYSTEM_BASE;
            }
            else if ((Position & GRAPHICS_BASE) == GRAPHICS_BASE)
            {
                stream = graphicsStream;
                addressBase = GRAPHICS_BASE;
            }
            else
            {
                throw new InvalidDataException($"Illegal resource position: 0x{Position:X}.");
            }

            stream.Position = Position & ~addressBase;
            try
            {
                WriteToStream(stream, value, ignoreEndianess);
            }
            finally
            {
                Position = stream.Position | addressBase;
            }
        }

        /// <summary>
        /// Writes a block.
        /// </summary>
        public void WriteBlock(IResourceBlock value)
        {
            value.Write(this);
        }




        public void WriteStruct<T>(T val) where T : struct
        {
            if (ResourceStructLayout<T>.CanCopyBytes)
            {
                Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref val, 1)));
                return;
            }

            int size = Marshal.SizeOf<T>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            bool initialized = false;
            try
            {
                Marshal.StructureToPtr(val, ptr, false);
                initialized = true;
                byte[] bytes = new byte[size];
                Marshal.Copy(ptr, bytes, 0, size);
                Write(bytes);
            }
            finally
            {
                if (initialized) Marshal.DestroyStructure<T>(ptr);
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void WriteStructs<T>(T[]? val) where T : struct
        {
            if (val == null || val.Length == 0) return;
            if (ResourceStructLayout<T>.CanCopyBytes)
            {
                Write(MemoryMarshal.AsBytes(val.AsSpan()));
                return;
            }
            foreach (var value in val) WriteStruct(value);
        }


        /// <summary>
        /// Write enough bytes to the stream to get to the specified alignment.
        /// </summary>
        /// <param name="alignment">value to align to</param>
        public void WritePadding(int alignment)
        {
            var pad = ((alignment - (Position % alignment)) % alignment);
            if (pad > 0) Write(new byte[pad]);
        }

        public void WriteUlongs(ulong[]? val)
        {
            if (val == null) return;
            foreach (var v in val)
            {
                Write(v);
            }
        }


    }





    /// <summary>
    /// Represents a data block in a resource file.
    /// </summary>
    public interface IResourceBlock
    {
        /// <summary>
        /// Gets or sets the position of the data block.
        /// </summary>
        long FilePosition { get; set; }

        /// <summary>
        /// Gets the length of the data block.
        /// </summary>
        long BlockLength { get; }
        long BlockLength_Gen9 { get; }

        /// <summary>
        /// Reads the data block.
        /// </summary>
        void Read(ResourceDataReader reader, params object[] parameters);

        /// <summary>
        /// Writes the data block.
        /// </summary>
        void Write(ResourceDataWriter writer, params object[] parameters);
    }

    /// <summary>
    /// Represents a data block of the system segement in a resource file.
    /// </summary>
    public interface IResourceSystemBlock : IResourceBlock
    {
        /// <summary>
        /// Returns a list of data blocks that are part of this block.
        /// </summary>
        Tuple<long, IResourceBlock>[] GetParts();

        /// <summary>
        /// Returns a list of data blocks that are referenced by this block.
        /// </summary>
        IResourceBlock[] GetReferences();
    }

    public interface IResourceXXSystemBlock : IResourceSystemBlock
    {
        IResourceSystemBlock GetType(ResourceDataReader reader, params object[] parameters);
    }

    /// <summary>
    /// Represents a data block of the graphics segmenet in a resource file.
    /// </summary>
    public interface IResourceGraphicsBlock : IResourceBlock
    { }


    /// <summary>
    /// Represents a data block that won't get cached while loading.
    /// </summary>
    public interface IResourceNoCacheBlock : IResourceBlock
    { }



    /// <summary>
    /// Represents a data block of the system segement in a resource file.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))] public abstract class ResourceSystemBlock : IResourceSystemBlock
    {
        private long position;

        /// <summary>
        /// Gets or sets the position of the data block.
        /// </summary>
        public virtual long FilePosition
        {
            get
            {
                return position;
            }
            set
            {
                position = value;
                foreach (var part in GetParts())
                {
                    part.Item2.FilePosition = value + part.Item1;
                }
            }
        }

        /// <summary>
        /// Gets the length of the data block.
        /// </summary>
        public abstract long BlockLength
        {
            get;
        }
        public virtual long BlockLength_Gen9 => BlockLength;

        /// <summary>
        /// Reads the data block.
        /// </summary>
        public abstract void Read(ResourceDataReader reader, params object[] parameters);

        /// <summary>
        /// Writes the data block.
        /// </summary>
        public abstract void Write(ResourceDataWriter writer, params object[] parameters);

        /// <summary>
        /// Returns a list of data blocks that are part of this block.
        /// </summary>
        public virtual Tuple<long, IResourceBlock>[] GetParts()
        {
            return new Tuple<long, IResourceBlock>[0];
        }

        /// <summary>
        /// Returns a list of data blocks that are referenced by this block.
        /// </summary>
        public virtual IResourceBlock[] GetReferences()
        {
            return new IResourceBlock[0];
        }
    }

    public abstract class ResourecTypedSystemBlock : ResourceSystemBlock, IResourceXXSystemBlock
    {
        public abstract IResourceSystemBlock GetType(ResourceDataReader reader, params object[] parameters);
    }

    /// <summary>
    /// Represents a data block of the graphics segmenet in a resource file.
    /// </summary>
    public abstract class ResourceGraphicsBlock : IResourceGraphicsBlock
    {
        /// <summary>
        /// Gets or sets the position of the data block.
        /// </summary>
        public virtual long FilePosition
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the length of the data block.
        /// </summary>
        public abstract long BlockLength
        {
            get;
        }
        public virtual long BlockLength_Gen9 => BlockLength;

        /// <summary>
        /// Reads the data block.
        /// </summary>
        public abstract void Read(ResourceDataReader reader, params object[] parameters);

        /// <summary>
        /// Writes the data block.
        /// </summary>
        public abstract void Write(ResourceDataWriter writer, params object[] parameters);
    }







    //public interface ResourceDataStruct
    //{
    //    void Read(ResourceDataReader reader);
    //    void Write(ResourceDataWriter writer);
    //}

}
