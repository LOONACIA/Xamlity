using System;
using System.Collections.ObjectModel;

namespace Xamlity.Styling
{
    public sealed class TriggerCollection : Collection<TriggerBase>
    {
        private bool isSealed;

        internal void Seal()
        {
            foreach (TriggerBase trigger in this)
                trigger.Seal();
            isSealed = true;
        }

        protected override void InsertItem(int index, TriggerBase item)
        {
            CheckSealed();
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, TriggerBase item)
        {
            CheckSealed();
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            base.SetItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            CheckSealed();
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            CheckSealed();
            base.ClearItems();
        }

        private void CheckSealed()
        {
            if (isSealed)
                throw new InvalidOperationException("Cannot modify a sealed TriggerCollection.");
        }
    }
}
