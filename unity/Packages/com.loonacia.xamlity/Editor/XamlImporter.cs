using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;
using Xamlity.Markup;
using Xamlity.Unity;

namespace Xamlity.Editor
{
    [ScriptedImporter(2, "xaml")]
    public sealed class XamlImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath, Encoding.UTF8);
            XamlParseResult result = XamlParser.Parse(text, ctx.assetPath);
            var asset = ScriptableObject.CreateInstance<XamlAsset>();
            asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            if (result.Errors.Count == 0)
                asset.Initialize(XamlIrSerializer.Serialize(result.Document));
            else
            {
                foreach (XamlError error in result.Errors)
                    ctx.LogImportError(error.ToString());
            }
            ctx.AddObjectToAsset("XamlAsset", asset);
            ctx.SetMainObject(asset);
        }
    }
}
