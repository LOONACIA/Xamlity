using System;
using System.Collections.Generic;
using System.Text;

namespace Xamlity.Markup
{
    internal static class XamlMarkupExtensionParser
    {
        internal static bool TryParse(string text, out string typeName,
            out List<(string Name, string Value)> arguments, out string error)
        {
            typeName = null;
            arguments = new List<(string Name, string Value)>();
            error = null;
            int position = 1;
            try
            {
                SkipWhitespace();
                int start = position;
                while (position < text.Length && !char.IsWhiteSpace(text[position]) &&
                    text[position] != '}' && text[position] != ',' && text[position] != '=')
                    position++;
                typeName = text.Substring(start, position - start);
                if (typeName.Length == 0)
                    throw Syntax("expected a type name");
                SkipWhitespace();
                bool named = false;
                if (position < text.Length && text[position] != '}')
                {
                    while (true)
                    {
                        string token = ReadToken(out bool quoted);
                        string name = null;
                        if (position < text.Length && text[position] == '=')
                        {
                            if (token.Length == 0 || quoted)
                                throw Syntax("expected an argument name before '='");
                            name = token;
                            named = true;
                            position++;
                            token = ReadToken(out _);
                        }
                        else
                        {
                            if (token.Length == 0 && !quoted)
                                throw Syntax("expected an argument");
                            if (named)
                                throw Syntax("positional arguments must precede named arguments");
                        }
                        arguments.Add((name, token));
                        if (position >= text.Length || text[position] != ',')
                            break;
                        position++;
                    }
                }
                if (position >= text.Length || text[position] != '}')
                    throw Syntax("expected closing '}'");
                position++;
                if (position != text.Length)
                    throw Syntax("unexpected text after '}'");
                return true;
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }

            void SkipWhitespace()
            {
                while (position < text.Length && char.IsWhiteSpace(text[position]))
                    position++;
            }

            string ReadToken(out bool quoted)
            {
                SkipWhitespace();
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
                        throw new FormatException("Nested markup extensions are not supported.");
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
