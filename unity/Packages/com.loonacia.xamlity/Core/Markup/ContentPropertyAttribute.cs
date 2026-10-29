using System;

namespace Xamlity.Markup
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class ContentPropertyAttribute : Attribute
    {
        public ContentPropertyAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
