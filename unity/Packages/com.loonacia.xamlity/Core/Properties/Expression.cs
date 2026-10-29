namespace Xamlity.Properties
{
    public abstract class Expression
    {
        internal Expression() { }

        internal abstract void Attach();
        internal abstract void Detach();
        internal abstract object GetValue();
        internal abstract bool TrySetValue(object value);
    }
}
