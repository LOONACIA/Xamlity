using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Xamlity.Editor
{
    internal static class XamlAssemblyDependencies
    {
        internal static string GetDependencyName(string assemblyName) => "Xamlity/Assembly/" + assemblyName;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Wait until the new assemblies are loaded and the current import has finished.
            EditorApplication.delayCall += RegisterAll;
        }

        internal static void RegisterAll()
        {
            foreach (Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                if (!File.Exists(assembly.outputPath))
                    continue;
                Hash128 hash = Hash128.Compute(File.ReadAllBytes(assembly.outputPath));
                AssetDatabase.RegisterCustomDependency(GetDependencyName(assembly.name), hash);
            }
            EditorApplication.delayCall -= Refresh;
            EditorApplication.delayCall += Refresh;
        }

        private static void Refresh() => AssetDatabase.Refresh();
    }
}
