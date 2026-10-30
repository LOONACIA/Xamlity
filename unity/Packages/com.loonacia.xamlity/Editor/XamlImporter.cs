using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Xamlity.Markup;
using Xamlity.Unity;
using Object = UnityEngine.Object;

namespace Xamlity.Editor
{
    [ScriptedImporter(6, "xaml")]
    public sealed class XamlImporter : ScriptedImporter
    {
        private static string[] GatherDependenciesFromSourceFile(string path)
        {
            XamlParseResult result = XamlParser.Parse(File.ReadAllText(path, Encoding.UTF8), path);
            var dependencies = new List<string>();
            foreach (XamlAssetReference reference in result.AssetReferences)
                if (reference.Kind == XamlAssetReferenceKind.ResourceDictionary &&
                    TryResolveImagePath(path, reference.Value, out string dependency, out _) &&
                    Path.GetExtension(dependency).Equals(".xaml", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(dependency))
                    dependencies.Add(dependency);
            return dependencies.ToArray();
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath, Encoding.UTF8);
            XamlParseResult result = XamlParser.Parse(text, ctx.assetPath);
            foreach (string assemblyName in result.ReferencedAssemblies)
                ctx.DependsOnCustomDependency(XamlAssemblyDependencies.GetDependencyName(assemblyName));
            var asset = ScriptableObject.CreateInstance<XamlAsset>();
            asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            if (result.Errors.Count == 0)
            {
                var keys = new List<string>();
                var assets = new List<Object>();
                bool hasErrors = false;
                foreach (XamlAssetReference reference in result.AssetReferences)
                {
                    if (reference.Kind == XamlAssetReferenceKind.ResourceDictionary)
                    {
                        if (!TryResolveResourceDictionary(ctx, reference.Value, out XamlAsset dictionary, out string dictionaryError))
                        {
                            ctx.LogImportError(new XamlError(ctx.assetPath, reference.Line, reference.Column,
                                $"Resource dictionary '{reference.Value}': {dictionaryError}").ToString());
                            hasErrors = true;
                        }
                        else if (!keys.Contains(reference.Value))
                        {
                            keys.Add(reference.Value);
                            assets.Add(dictionary);
                        }
                        continue;
                    }
                    if (!TryResolveImagePath(ctx.assetPath, reference.Value, out string path, out string error))
                    {
                        ctx.LogImportError(new XamlError(ctx.assetPath, reference.Line, reference.Column, error).ToString());
                        hasErrors = true;
                        continue;
                    }
                    Object[] loaded = AssetDatabase.LoadAllAssetsAtPath(path);
                    if (loaded.Length != 0)
                        ctx.DependsOnArtifact(path);
                    else
                        ctx.DependsOnSourceAsset(path);
                    if (!TrySelectImageAsset(loaded, AssetDatabase.LoadMainAssetAtPath(path),
                        out Object selected, out error))
                    {
                        ctx.LogImportError(new XamlError(ctx.assetPath, reference.Line, reference.Column,
                            $"Image asset '{reference.Value}': {error}").ToString());
                        hasErrors = true;
                    }
                    else if (!keys.Contains(reference.Value))
                    {
                        keys.Add(reference.Value);
                        assets.Add(selected);
                    }
                }
                if (!hasErrors)
                    asset.Initialize(XamlIrSerializer.Serialize(result.Document), keys.ToArray(), assets.ToArray());
            }
            else
            {
                foreach (XamlError error in result.Errors)
                    ctx.LogImportError(error.ToString());
            }
            ctx.AddObjectToAsset("XamlAsset", asset);
            ctx.SetMainObject(asset);
        }

        private static bool TryResolveResourceDictionary(AssetImportContext ctx, string value,
            out XamlAsset dictionary, out string error)
        {
            dictionary = null;
            if (!TryResolveImagePath(ctx.assetPath, value, out string path, out _))
            {
                error = "Invalid asset path; use a project path or a relative path with '/' separators.";
                return false;
            }
            bool exists = File.Exists(path);
            if (exists)
                ctx.DependsOnArtifact(path);
            else
                ctx.DependsOnSourceAsset(path);
            if (!Path.GetExtension(path).Equals(".xaml", StringComparison.OrdinalIgnoreCase))
                error = "Asset must be a .xaml file.";
            else if (!exists)
                error = "Asset was not found.";
            else
            {
                dictionary = ctx.GetReferenceToAssetMainObject(path) as XamlAsset;
                if (dictionary == null || !dictionary.HasIr)
                    error = "XAML asset has import errors.";
                else if (!dictionary.HasResourceDictionaryRoot)
                    error = "XAML asset must have a ResourceDictionary root.";
                else
                {
                    error = null;
                    return true;
                }
            }
            return false;
        }

        internal static bool TrySelectImageAsset(Object[] assets, Object mainAsset,
            out Object selected, out string error)
        {
            selected = null;
            error = null;
            foreach (Object asset in assets)
            {
                if (!(asset is Sprite sprite) || sprite == null)
                    continue;
                if (selected != null)
                {
                    selected = null;
                    error = "Multiple Sprites are not supported; use a single Sprite.";
                    return false;
                }
                selected = sprite;
            }
            if (selected != null)
                return true;
            if (mainAsset is Texture2D texture && texture != null)
            {
                selected = texture;
                return true;
            }
            error = assets.Length == 0 ? "Asset was not found." : "Asset must be a Texture2D or a single Sprite.";
            return false;
        }

        internal static bool TryResolveImagePath(string xamlPath, string value, out string path, out string error)
        {
            path = null;
            error = $"Invalid image asset path '{value}'; use a project path or a relative path with '/' separators.";
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("/", StringComparison.Ordinal) ||
                value.IndexOf(':') >= 0 || value.IndexOf('\\') >= 0)
                return false;
            string combined = value.StartsWith("Assets/", StringComparison.Ordinal) ||
                value.StartsWith("Packages/", StringComparison.Ordinal)
                ? value : xamlPath.Substring(0, xamlPath.LastIndexOf('/') + 1) + value;
            var segments = new List<string>();
            foreach (string segment in combined.Split('/'))
            {
                if (segment.Length == 0 || segment == ".")
                    continue;
                if (segment == "..")
                {
                    if (segments.Count == 0)
                        return false;
                    segments.RemoveAt(segments.Count - 1);
                }
                else
                    segments.Add(segment);
            }
            if (segments.Count < 2 || (segments[0] != "Assets" && segments[0] != "Packages"))
                return false;
            path = string.Join("/", segments);
            error = null;
            return true;
        }
    }
}
