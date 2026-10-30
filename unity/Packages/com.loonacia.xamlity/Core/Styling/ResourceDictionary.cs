using System;
using System.Collections;
using System.Collections.ObjectModel;

namespace Xamlity.Styling
{
    public class ResourceDictionary : IDictionary
    {
        private readonly Hashtable entries = new Hashtable();

        public Uri Source { get; internal set; }

        public Collection<ResourceDictionary> MergedDictionaries { get; } = new DictionaryCollection();

        public object this[object key]
        {
            get
            {
                if (entries.Contains(key))
                    return entries[key];
                for (int i = MergedDictionaries.Count - 1; i >= 0; i--)
                    if (MergedDictionaries[i].Contains(key))
                        return MergedDictionaries[i][key];
                return null;
            }
            set => entries[key] = value;
        }

        public bool Contains(object key)
        {
            if (entries.Contains(key))
                return true;
            for (int i = MergedDictionaries.Count - 1; i >= 0; i--)
                if (MergedDictionaries[i].Contains(key))
                    return true;
            return false;
        }

        public void Add(object key, object value) => entries.Add(key, value);
        public void Remove(object key) => entries.Remove(key);
        public void Clear() => entries.Clear();
        public ICollection Keys => entries.Keys;
        public ICollection Values => entries.Values;
        public int Count => entries.Count;
        public bool IsReadOnly => false;
        public bool IsFixedSize => false;
        public bool IsSynchronized => false;
        public object SyncRoot => entries.SyncRoot;
        public void CopyTo(Array array, int index) => entries.CopyTo(array, index);
        public IDictionaryEnumerator GetEnumerator() => entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class DictionaryCollection : Collection<ResourceDictionary>
        {
            protected override void InsertItem(int index, ResourceDictionary item)
            {
                if (item == null)
                    throw new ArgumentNullException(nameof(item));
                base.InsertItem(index, item);
            }

            protected override void SetItem(int index, ResourceDictionary item)
            {
                if (item == null)
                    throw new ArgumentNullException(nameof(item));
                base.SetItem(index, item);
            }
        }
    }
}
