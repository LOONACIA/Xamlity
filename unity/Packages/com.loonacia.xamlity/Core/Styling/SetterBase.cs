namespace Xamlity.Styling
{
    public abstract class SetterBase
    {
        internal SetterBase() { }

        public bool IsSealed { get; private set; }

        internal virtual void Seal() => IsSealed = true;
    }
}
