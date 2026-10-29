using UnityEngine;
using Xamlity.Diagnostics;

namespace Xamlity.Unity
{
    internal sealed class UnityXamlityLogger : IXamlityLogger
    {
        public void LogWarning(string message) => Debug.LogWarning(message);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Install() => XamlityLog.Logger = new UnityXamlityLogger();
    }
}
