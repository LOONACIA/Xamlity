using System;
using System.Collections;
using System.Collections.Generic;
using Xamlity.Tree;

namespace Xamlity.Layout
{
    public sealed class UIElementCollection : IList<UIElement>
    {
        private readonly Panel owner;
        private readonly List<UIElement> children = new List<UIElement>();

        internal UIElementCollection(Panel owner)
        {
            this.owner = owner;
        }

        public int Count => children.Count;
        public bool IsReadOnly => false;

        public UIElement this[int index]
        {
            get => children[index];
            set
            {
                UIElement previous = children[index];
                if (ReferenceEquals(previous, value))
                    return;

                // Validate and attach first so a rejected replacement preserves the old child.
                owner.AttachChild(value);
                owner.DetachChild(previous);
                children[index] = value;
            }
        }

        public void Add(UIElement item) => Insert(Count, item);

        public void Insert(int index, UIElement item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (index < 0 || index > Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            owner.AttachChild(item);
            children.Insert(index, item);
        }

        public bool Remove(UIElement item)
        {
            int index = IndexOf(item);
            if (index < 0)
                return false;

            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            UIElement child = children[index];
            owner.DetachChild(child);
            children.RemoveAt(index);
        }

        public void Clear()
        {
            for (int index = Count - 1; index >= 0; index--)
                RemoveAt(index);
        }

        public bool Contains(UIElement item) => children.Contains(item);
        public int IndexOf(UIElement item) => children.IndexOf(item);
        public void CopyTo(UIElement[] array, int arrayIndex) => children.CopyTo(array, arrayIndex);
        public List<UIElement>.Enumerator GetEnumerator() => children.GetEnumerator();
        IEnumerator<UIElement> IEnumerable<UIElement>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
