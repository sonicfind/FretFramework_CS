using Framework.Song.Tracks.Notes.Keys;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Framework.FlatMaps
{
    internal unsafe static class Copier
    {
        [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr MemCpy(void* dest, void* src, UIntPtr count);

        [DllImport("msvcrt.dll", EntryPoint = "memmove", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr MemMove(void* dest, void* src, UIntPtr count);
    }

    public unsafe class SimpleFlatMap<Key, T> : IDisposable
       where T : new()
       where Key : IComparable<Key>, IEquatable<Key>
    {
        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        public struct Node
        {
            public Key key;
            public T obj;

            public static bool operator <(Node node, Key key) { return node.key.CompareTo(key) < 0; }

            public static bool operator >(Node node, Key key) { return node.key.CompareTo(key) < 0; }

            private string GetDebuggerDisplay() { return $"{key} | {obj}"; }
        }

        internal static T BASE = new();
        internal static readonly int DEFAULTCAPACITY = 16;
        internal static readonly bool ISVALUETYPE = !RuntimeHelpers.IsReferenceOrContainsReferences<T>();

        internal static readonly int NODESIZE;

        static SimpleFlatMap()
        {
            if (!ISVALUETYPE)
                return;

            var dm = new DynamicMethod("SizeOfType", typeof(int), new Type[] { });
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Sizeof, typeof(Node));
            il.Emit(OpCodes.Ret);
            NODESIZE = (int)dm.Invoke(null, null);
        }

        Node[] _buffer = Array.Empty<Node>();
        Node* _buffer_valueType = null;
        int _capacity;
        int _count;
        int _version;
        bool _disposed;


        public SimpleFlatMap() { }
        public SimpleFlatMap(int capacity) { Capacity = capacity; }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (ISVALUETYPE)
            {
                if (_buffer_valueType != null)
                {
                    Marshal.FreeHGlobal((IntPtr)_buffer_valueType);
                    _buffer_valueType = null;
                }
            }
            else if (disposing)
            {
                _buffer = Array.Empty<Node>();
            }

            _disposed = true;
        }

        ~SimpleFlatMap()
        {
            Dispose(false);
        }

        public void Clear()
        {
            if (!ISVALUETYPE)
            {
                fixed (Node* arr = _buffer)
                {
                    for (uint i = 0; i < _count; ++i)
                        arr[i] = default;
                }
            }
            else
            {
                for (uint i = 0; i < _count; ++i)
                    _buffer_valueType[i] = default;
            }
            if (_count > 0)
                _version++;
            _count = 0;
        }

        public int Count { get { return _count; } }

        public int Capacity
        {
            get => _capacity;
            set
            {
                if (value < _count)
                    throw new ArgumentOutOfRangeException("value");

                if (value != _capacity)
                {
                    if (value > 0)
                    {
                        if (value > Array.MaxLength)
                            value = Array.MaxLength;

                        if (!ISVALUETYPE)
                        {
                            Node[] newItems = new Node[value];
                            if (_count > 0)
                                Array.Copy(_buffer, 0, newItems, 0, _count);
                            _buffer = newItems;
                        }
                        else
                        {
                            int newSize = value * NODESIZE;
                            void* newItems = (void*)Marshal.AllocHGlobal(newSize);

                            if (_count > 0)
                                Copier.MemCpy(newItems, _buffer_valueType, (uint)_count * (uint)NODESIZE);

                            Marshal.FreeHGlobal((IntPtr)_buffer_valueType);
                            _buffer_valueType = (Node*)newItems;
                        }
                    }
                    else if(!ISVALUETYPE)
                        _buffer = Array.Empty<Node>();
                    else
                    {
                        if (_capacity > 0)
                            Marshal.FreeHGlobal((IntPtr)_buffer_valueType);
                        _buffer_valueType = null;
                    }
                    _capacity = value;
                    ++_version;
                }
            }
        }

        public void TrimExcess()
        {
            if (_count < _capacity)
                Capacity = _count;
        }

        public bool IsEmpty() { return _count == 0; }

        public ref T Add_Back(Key key, T obj)
        {
            if (_count == Array.MaxLength)
                throw new OverflowException("Element limit reached");

            if (_count == _capacity)
                Grow();

            ref Node node = ref At_index(_count);
            node.key = key;
            node.obj = obj;
            ++_count;
            return ref node.obj;
        }

        public ref T Add_Back(Key key, ref T obj)
        {
            if (_count == Array.MaxLength)
                throw new OverflowException("Element limit reached");

            if (_count == _capacity)
                Grow();

            ref Node node = ref At_index(_count);
            node.key = key;
            node.obj = obj;
            ++_count;
            return ref node.obj;
        }

        public ref T Add_Back(Key key)
        {
            if (_count == Array.MaxLength)
                throw new OverflowException("Element limit reached");

            if (_count == _capacity)
                Grow();

            ref Node node = ref At_index(_count);
            node.key = key;
            node.obj = ISVALUETYPE ? BASE : new();
            ++_count;
            return ref node.obj;
        }

        public void Add_Back_NoReturn(Key key)
        {
            if (_count == Array.MaxLength)
                throw new OverflowException("Element limit reached");

            if (_count == _capacity)
                Grow();

            ref Node node = ref At_index(_count);
            node.key = key;
            node.obj = ISVALUETYPE ? BASE : new();
            ++_count;
        }

        public ref T Get_Or_Add_Back(Key key)
        {
            if (_count == 0)
                return ref Add_Back(key);

            ref Node node = ref At_index(_count - 1);
            if (node < key)
                return ref Add_Back(key);
            return ref node.obj;
        }

        public ref T Traverse_Backwards_Until(Key key)
        {
            int index = _count;
            if (ISVALUETYPE)
            {
                while (index > 0)
                    if (_buffer_valueType[--index].key.CompareTo(key) <= 0)
                        break;
                return ref _buffer_valueType[index].obj;
            }
            else
            {
                fixed (Node* arr = _buffer)
                {
                    while (index > 0)
                        if (arr[--index].key.CompareTo(key) <= 0)
                            break;
                    return ref arr[index].obj;
                }
            }
        }

        public void Pop()
        {
            if (_count == 0)
                throw new Exception("Pop on emtpy map");

            --_count;
            At_index(_count) = default;
            ++_version;
        }

        public int Find_Or_Add_index(int searchIndex, Key key) { return Find_or_emplace_index(searchIndex, key); }

        public ref T Find_Or_Add(int searchIndex, Key key)
        {
            int index = Find_or_emplace_index(searchIndex, key);
            return ref At_index(index).obj;
        }

        public ref T this[Key key] { get { return ref Find_Or_Add(0, key); } }

        public ref Node At_index(int index)
        {
            if (ISVALUETYPE)
                return ref _buffer_valueType[index];
            return ref _buffer[index];
        }

        public ref T At(Key key)
        {
            int index = BinarySearch(0, key);
            if (index < 0)
                throw new KeyNotFoundException();
            return ref At_index(index).obj;
        }

        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count)
                throw new IndexOutOfRangeException();

            At_index(index) = default;
            if (index < --_count)
            {
                if (!ISVALUETYPE)
                    Array.Copy(_buffer, index + 1, _buffer, index, _count - index);
                else
                    Copier.MemMove(_buffer_valueType + index, _buffer_valueType + index + 1, (nuint)((_count - index) * NODESIZE));
            }
            ++_version;
        }

        public ref T Last() { return ref At_index(_count - 1).obj; }

        public bool ValidateLastKey(Key key)
        {
            return _count > 0 && At_index(_count - 1).key.Equals(key);
        }

        public bool Contains(Key key) { return BinarySearch(0, key) >= 0; }

        public Enumerator GetEnumerator() { return new Enumerator(this); }

        private int Find_or_emplace_index(int searchIndex, Key key)
        {
            int index = BinarySearch(searchIndex, key);
            if (index < 0)
            {
                index = ~index;
                if (_count == Array.MaxLength)
                    throw new OverflowException("Element limit reached");

                if (_count == _buffer.Length)
                    Grow();

                if (index < _count)
                {
                    if (!ISVALUETYPE)
                        Array.Copy(_buffer, index, _buffer, index + 1, _count - index);
                    else
                        Copier.MemMove(_buffer_valueType + index + 1, _buffer_valueType + index, (nuint)((_count - index) * NODESIZE));
                }

                ref Node node = ref At_index(index);
                node.key = key;
                node.obj = BASE;
                ++_count;
                ++_version;
            }
            return index;
        }

        private int BinarySearch(int searchIndex, Key key)
        {
            int lo = searchIndex;
            int hi = Count - (searchIndex + 1);
            if (ISVALUETYPE)
            {
                while (lo <= hi)
                {
                    int curr = lo + ((hi - lo) >> 1);
                    int order = _buffer_valueType[curr].key.CompareTo(key);

                    if (order == 0) return curr;
                    if (order < 0)
                        lo = curr + 1;
                    else
                        hi = curr - 1;
                }
            }
            else
            {
                fixed (Node* arr = _buffer)
                {
                    while (lo <= hi)
                    {
                        int curr = lo + ((hi - lo) >> 1);
                        int order = arr[curr].key.CompareTo(key);

                        if (order == 0) return curr;
                        if (order < 0)
                            lo = curr + 1;
                        else
                            hi = curr - 1;
                    }
                }
            }
            
            
            return ~lo;
        }

        private string GetDebuggerDisplay() { return $"Count: {Count}"; }

        private void Grow()
        {
            int newcapacity = _capacity == 0 ? DEFAULTCAPACITY : 2 * _capacity;
            if ((uint)newcapacity > Array.MaxLength) newcapacity = Array.MaxLength;
            Capacity = newcapacity;
        }

        public struct Enumerator : IEnumerator<Node>, IEnumerator
        {
            private readonly SimpleFlatMap<Key, T> _map;
            private int _index;
            private readonly int _version;
            private Node _current;

            internal Enumerator(SimpleFlatMap<Key, T> map)
            {
                _map = map;
                _index = 0;
                _version = map._version;
                _current = default;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                SimpleFlatMap<Key, T> localMap = _map;

                if (_version == localMap._version && ((uint)_index < (uint)localMap._count))
                {
                    _current = localMap.At_index(_index);
                    _index++;
                    return true;
                }
                return MoveNextRare();
            }

            private bool MoveNextRare()
            {
                if (_version != _map._version)
                    throw new InvalidOperationException("Enum failed - Map was updated");

                _index = _map._count + 1;
                _current = default;
                return false;
            }

            public Node Current => _current;

            object? IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || _index == _map._count + 1)
                        throw new InvalidOperationException("Enum Operation not possible");
                    return Current;
                }
            }

            void IEnumerator.Reset()
            {
                if (_version != _map._version)
                    throw new InvalidOperationException("Enum failed - Map was updated");

                _index = 0;
                _current = default;
            }
        }
    }

    public class TimedFlatMap<T> : SimpleFlatMap<ulong, T>
        where T : new()
    { }
}
