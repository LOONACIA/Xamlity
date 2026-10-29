using System;
using System.Collections;
using System.Collections.Generic;

namespace Xamlity.Layout
{
    public sealed class RowDefinitionCollection : IList<RowDefinition>
    {
        private readonly Grid owner;
        private readonly List<RowDefinition> definitions = new List<RowDefinition>();

        internal RowDefinitionCollection(Grid owner)
        {
            this.owner = owner;
        }

        public int Count => definitions.Count;
        public bool IsReadOnly => false;

        public RowDefinition this[int index]
        {
            get => definitions[index];
            set
            {
                RowDefinition previous = definitions[index];
                if (ReferenceEquals(previous, value))
                    return;
                Validate(value);
                previous.Owner = null;
                value.Owner = owner;
                definitions[index] = value;
                owner.InvalidateMeasure();
            }
        }

        public void Add(RowDefinition item) => Insert(Count, item);

        public void Insert(int index, RowDefinition item)
        {
            if (index < 0 || index > Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            Validate(item);
            definitions.Insert(index, item);
            item.Owner = owner;
            owner.InvalidateMeasure();
        }

        private static void Validate(RowDefinition item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (item.Owner != null)
                throw new InvalidOperationException("The definition already belongs to a Grid.");
        }

        public bool Remove(RowDefinition item)
        {
            int index = IndexOf(item);
            if (index < 0)
                return false;
            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            RowDefinition item = definitions[index];
            definitions.RemoveAt(index);
            item.Owner = null;
            owner.InvalidateMeasure();
        }

        public void Clear()
        {
            for (int index = Count - 1; index >= 0; index--)
                RemoveAt(index);
        }

        public bool Contains(RowDefinition item) => definitions.Contains(item);
        public int IndexOf(RowDefinition item) => definitions.IndexOf(item);
        public void CopyTo(RowDefinition[] array, int arrayIndex) => definitions.CopyTo(array, arrayIndex);
        public List<RowDefinition>.Enumerator GetEnumerator() => definitions.GetEnumerator();
        IEnumerator<RowDefinition> IEnumerable<RowDefinition>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
