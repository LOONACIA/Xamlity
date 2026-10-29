using System;
using System.IO;
using System.Text;

namespace Xamlity.Markup
{
    public static class XamlIrSerializer
    {
        private const int FormatVersion = 2;
        private static readonly byte[] Magic = { (byte)'X', (byte)'A', (byte)'I', (byte)'R' };

        public static byte[] Serialize(XamlDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(Magic);
                    writer.Write(FormatVersion);
                    WriteObject(writer, document.Root);
                }
                return stream.ToArray();
            }
        }

        public static XamlDocument Deserialize(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            int? version = null;
            try
            {
                using (var stream = new MemoryStream(data, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    byte[] magic = reader.ReadBytes(Magic.Length);
                    version = reader.ReadInt32();
                    for (int index = 0; index < Magic.Length; index++)
                    {
                        if (magic[index] != Magic[index])
                            throw new InvalidDataException($"Invalid XAML IR magic; expected XAIR. Expected version {FormatVersion}, actual version {version}.");
                    }
                    if (version != FormatVersion)
                        throw new InvalidDataException($"Unsupported XAML IR version. Expected version {FormatVersion}, actual version {version}.");
                    return new XamlDocument { Root = ReadObject(reader) };
                }
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException($"Truncated XAML IR data. Expected version {FormatVersion}, actual version {version?.ToString() ?? "unavailable"}.", exception);
            }
        }

        private static void WriteObject(BinaryWriter writer, XamlObjectNode node)
        {
            WriteString(writer, node.TypeName);
            WriteString(writer, node.Name);
            writer.Write(node.Line);
            writer.Write(node.Column);
            writer.Write(node.Properties.Count);
            foreach (XamlPropertyNode property in node.Properties)
            {
                WriteString(writer, property.PropertyName);
                WriteString(writer, property.OwnerTypeName);
                writer.Write(property.Line);
                writer.Write(property.Column);
                WriteString(writer, property.TextValue);
                writer.Write(property.Objects.Count);
                foreach (XamlObjectNode child in property.Objects)
                    WriteObject(writer, child);
            }
        }

        private static XamlObjectNode ReadObject(BinaryReader reader)
        {
            var node = new XamlObjectNode
            {
                TypeName = ReadString(reader), Name = ReadString(reader),
                Line = reader.ReadInt32(), Column = reader.ReadInt32()
            };
            int propertyCount = ReadCount(reader);
            for (int index = 0; index < propertyCount; index++)
            {
                var property = new XamlPropertyNode
                {
                    PropertyName = ReadString(reader), OwnerTypeName = ReadString(reader), Line = reader.ReadInt32(),
                    Column = reader.ReadInt32(), TextValue = ReadString(reader)
                };
                int objectCount = ReadCount(reader);
                for (int child = 0; child < objectCount; child++)
                    property.Objects.Add(ReadObject(reader));
                node.Properties.Add(property);
            }
            return node;
        }

        private static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0)
                throw new InvalidDataException("XAML IR node count cannot be negative.");
            return count;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            writer.Write(value != null);
            if (value != null)
                writer.Write(value);
        }

        private static string ReadString(BinaryReader reader) => reader.ReadBoolean() ? reader.ReadString() : null;
    }
}
