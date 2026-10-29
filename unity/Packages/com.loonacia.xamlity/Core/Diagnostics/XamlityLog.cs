namespace Xamlity.Diagnostics
{
    public static class XamlityLog
    {
        public static IXamlityLogger Logger { get; set; }

        internal static void Warning(string message) => Logger?.LogWarning(message);
    }
}
