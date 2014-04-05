using System;
using System.Runtime.InteropServices;

namespace EventStore.Core.Index
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct IndexEntry: IComparable<IndexEntry>, IEquatable<IndexEntry>
    {
        [FieldOffset(0)] public UInt64 Key;
        [FieldOffset(0)] public fixed byte Bytes [16];
        [FieldOffset(0)] public Int32 Version;
        [FieldOffset(4)] public UInt32 Stream;
        [FieldOffset(8)] public Int64 Position;

        public IndexEntry(ulong key, long position) : this()
        {
            Key = key;
            Position = position;
        }

        public IndexEntry(uint stream, int version, long position) : this()
        {
            Stream = stream;
            Version = version;
            Position = position;
        }

        public int CompareTo(IndexEntry other)
        {
            var keyCmp = Key.CompareTo(other.Key);
            if (keyCmp != 0)
                return keyCmp;
            return Position.CompareTo(other.Position);
        }

        public bool Equals(IndexEntry other)
        {
            return Key == other.Key && Position == other.Position;
        }

        public override string ToString()
        {
            return string.Format("Key: {0}, Stream: {1}, Version: {2}, Position: {3}", Key, Stream, Version, Position);
        }
    }
}