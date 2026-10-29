using UnityEditor;
using Xamlity.Unity;

namespace Xamlity.Editor
{
    internal static class XamlityEditorStartup
    {
        [InitializeOnLoadMethod]
        private static void Initialize() => UnityXamlityLogger.Install();
    }
}
