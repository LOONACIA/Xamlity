using System.Collections.Generic;

namespace Xamlity.Markup
{
    public sealed class XamlDocument
    {
        public XamlObjectNode Root { get; set; }
    }

    public sealed class XamlObjectNode
    {
        public string TypeName { get; set; }
        public string Name { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public List<XamlPropertyNode> Properties { get; } = new List<XamlPropertyNode>();
    }

    public sealed class XamlPropertyNode
    {
        public string PropertyName { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string TextValue { get; set; }
        public List<XamlObjectNode> Objects { get; } = new List<XamlObjectNode>();
    }
}
