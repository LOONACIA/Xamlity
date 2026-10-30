namespace Xamlity.Styling
{
    public abstract class TriggerBase
    {
        internal TriggerBase() { }

        public bool IsSealed { get; private set; }

        internal virtual void Seal() => IsSealed = true;
    }
}
