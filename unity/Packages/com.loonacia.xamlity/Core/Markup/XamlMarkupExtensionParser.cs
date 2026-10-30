using System;
using System.Collections.Generic;
using System.Text;

namespace Xamlity.Markup
{
    internal static class XamlMarkupExtensionParser
    {
        internal sealed class Extension
        {
            internal string Name;
            internal readonly List<(string Name, string Value, Extension Nested)> Arguments =
                new List<(string Name, string Value, Extension Nested)>();
        }

        internal static bool TryParse(string text, out string typeName,
            out List<(string Name, string Value, Extension Nested)> arguments, out string error)
        {
            typeName = null;
            arguments = null;
            error = null;
            int position = 0;
            try
            {
                Extension extension = ReadExtension();
                if (position != text.Length)
                    throw Syntax("unexpected text after '}'");
                typeName = extension.Name;
                arguments = extension.Arguments;
                return true;
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }

            Extension ReadExtension()
            {
                position++;
                SkipWhitespace();
                int start = position;
                while (position < text.Length && !char.IsWhiteSpace(text[position]) &&
                    text[position] != '}' && text[position] != ',' && text[position] != '=')
                    position++;
                var extension = new Extension { Name = text.Substring(start, position - start) };
                if (extension.Name.Length == 0)
                    throw Syntax("expected a type name");
                SkipWhitespace();
                bool named = false;
                if (position < text.Length && text[position] != '}')
                {
                    while (true)
                    {
                        string token = ReadToken(out bool quoted, out Extension nested);
                        string name = null;
                        if (position < text.Length && text[position] == '=')
                        {
                            if (nested != null || token.Length == 0 || quoted)
                                throw Syntax("expected an argument name before '='");
                            name = token;
                            named = true;
                            position++;
                            token = ReadToken(out _, out nested);
                        }
                        else
                        {
                            if (nested == null && token.Length == 0 && !quoted)
                                throw Syntax("expected an argument");
                            if (named)
                                throw Syntax("positional arguments must precede named arguments");
                        }
                        extension.Arguments.Add((name, token, nested));
                        if (position >= text.Length || text[position] != ',')
                            break;
                        position++;
                    }
                }
                if (position >= text.Length || text[position] != '}')
                    throw Syntax("expected closing '}'");
                position++;
                return extension;
            }

            void SkipWhitespace()
            {
                while (position < text.Length && char.IsWhiteSpace(text[position]))
                    position++;
            }

            string ReadToken(out bool quoted, out Extension nested)
            {
                SkipWhitespace();
                nested = null;
                quoted = false;
                if (position < text.Length && text[position] == '{')
                {
                    nested = ReadExtension();
                    SkipWhitespace();
                    if (position < text.Length && text[position] != ',' && text[position] != '}')
                        throw Syntax("unexpected text after a nested extension");
                    return null;
                }
                var value = new StringBuilder();
                quoted = position < text.Length && (text[position] == '\'' || text[position] == '"');
                char quote = quoted ? text[position++] : '\0';
                bool closed = !quoted;
                while (position < text.Length)
                {
                    char character = text[position];
                    if (character == '\\')
                    {
                        position++;
                        if (position == text.Length)
                            throw Syntax("expected a character after '\\'");
                        value.Append(text[position++]);
                    }
                    else if (quoted)
                    {
                        position++;
                        if (character == quote)
                        {
                            closed = true;
                            SkipWhitespace();
                            if (position < text.Length && text[position] != ',' &&
                                text[position] != '=' && text[position] != '}')
                                throw Syntax("unexpected text after a quoted value");
                            break;
                        }
                        value.Append(character);
                    }
                    else if (character == ',' || character == '=' || character == '}')
                        break;
                    else if (character == '{')
                        throw Syntax("a nested extension must enclose the entire value");
                    else if (character == '\'' || character == '"')
                        throw Syntax("quotes must enclose the entire value");
                    else
                    {
                        value.Append(character);
                        position++;
                    }
                }
                if (!closed)
                    throw Syntax("unterminated quoted value");
                string result = quoted ? value.ToString() : value.ToString().Trim();
                // Decoded literal braces must not be interpreted as another extension on conversion.
                return result.TrimStart().StartsWith("{", StringComparison.Ordinal) ? "{}" + result : result;
            }
        }

        private static FormatException Syntax(string description) =>
            new FormatException("Invalid markup extension syntax: " + description + ".");
    }
}
