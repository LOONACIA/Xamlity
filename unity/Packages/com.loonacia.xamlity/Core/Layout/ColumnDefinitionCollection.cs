using System;
using System.Collections;
using System.Collections.Generic;

namespace Xamlity.Layout
{
    public sealed class ColumnDefinitionCollection : IList<ColumnDefinition>
    {
        private readonly Grid owner;
        private readonly List<ColumnDefinition> definitions = new List<ColumnDefinition>();

        internal ColumnDefinitionCollection(Grid owner)
        {
            this.owner = owner;
        }

        public int Count => definitions.Count;
        public bool IsReadOnly => false;

        public ColumnDefinition this[int index]
        {
            get => definitions[index];
            set
            {
                ColumnDefinition previous = definitions[index];
                if (ReferenceEquals(previous, value))
                    return;
                Validate(value);
                previous.Owner = null;
                value.Owner = owner;
                definitions[index] = value;
                owner.InvalidateMeasure();
            }
        }

        public void Add(ColumnDefinition item) => Insert(Count, item);

        public void Insert(int index, ColumnDefinition item)
        {
            if (index < 0 || index > Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            Validate(item);
            definitions.Insert(index, item);
            item.Owner = owner;
            owner.InvalidateMeasure();
        }

        private static void Validate(ColumnDefinition item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (item.Owner != null)
                throw new InvalidOperationException("The definition already belongs to a Grid.");
        }

        public bool Remove(ColumnDefinition item)
        {
            int index = IndexOf(item);
            if (index < 0)
                return false;
            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            ColumnDefinition item = definitions[index];
            definitions.RemoveAt(index);
            item.Owner = null;
            owner.InvalidateMeasure();
        }

        public void Clear()
        {
            for (int index = Count - 1; index >= 0; index--)
                RemoveAt(index);
        }

        public bool Contains(ColumnDefinition item) => definitions.Contains(item);
        public int IndexOf(ColumnDefinition item) => definitions.IndexOf(item);
        public void CopyTo(ColumnDefinition[] array, int arrayIndex) => definitions.CopyTo(array, arrayIndex);
        public List<ColumnDefinition>.Enumerator GetEnumerator() => definitions.GetEnumerator();
        IEnumerator<ColumnDefinition> IEnumerable<ColumnDefinition>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
