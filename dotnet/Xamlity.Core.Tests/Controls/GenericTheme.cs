using System.IO;
using NUnit.Framework;
using Xamlity.Core.Tests.Rendering;
using Xamlity.Markup;
using Xamlity.Rendering;
using Xamlity.Styling;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Controls
{
    internal static class GenericTheme
    {
        internal static ResourceDictionary Load()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                directory != null; directory = directory.Parent)
            {
                string package = Path.Combine(directory.FullName, "unity", "Packages", "com.loonacia.xamlity");
                if (!Directory.Exists(package))
                    continue;
                string path = Path.Combine(package, "Themes", "Resources", "Xamlity", "Generic.xaml");
                var result = XamlParser.Parse(File.ReadAllText(path), path);
                Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
                return (ResourceDictionary)XamlBuilder.Build(
                    XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(result.Document)));
            }
            throw new DirectoryNotFoundException("Cannot find the Xamlity package from the test directory.");
        }

        internal static UIRoot CreateRoot(UIElement element, ResourceDictionary applicationResources = null) =>
            new UIRoot(element, new TextBlockTests.FakeTextMeasurer(), new FontHandle(new object()),
                applicationResources, Load());
    }
}
