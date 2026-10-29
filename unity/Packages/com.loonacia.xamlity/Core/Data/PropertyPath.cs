using System;

namespace Xamlity.Data
{
    public sealed class PropertyPath
    {
        public PropertyPath(string path)
        {
            Path = path;
            if (string.IsNullOrEmpty(path) || path == ".")
            {
                Segments = Array.Empty<string>();
                return;
            }

            bool first = true;
            foreach (char character in path)
            {
                if (character == '.' && !first)
                {
                    first = true;
                    continue;
                }
                if (character != '_' && !char.IsLetter(character) && (first || !char.IsDigit(character)))
                    throw Unsupported(path);
                first = false;
            }
            if (first)
                throw Unsupported(path);
            Segments = path.Split('.');
        }

        public string Path { get; }
        internal string[] Segments { get; }

        private static ArgumentException Unsupported(string path) => new ArgumentException(
            $"Property path '{path}' is not supported; use dot-separated property names.");
    }
}
