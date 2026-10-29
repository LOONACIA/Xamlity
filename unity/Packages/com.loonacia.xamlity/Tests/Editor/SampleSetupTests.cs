using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Unity;

namespace Xamlity.Editor.Tests
{
    // These tests depend on sample assets in the development repository only.
    public class SampleSetupTests
    {
        private const string HelloPath = "Assets/Samples/Hello/Hello.xaml";

        [Test]
        public void SettingsLoadCachedDynamicFontWithSourceAndEmptyAtlas()
        {
            var settings = XamlitySettings.Load();

            Assert.That(settings, Is.Not.Null);
            Assert.That(XamlitySettings.Load(), Is.SameAs(settings));
            var font = settings.DefaultFont;
            Assert.That(font, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(font), Is.EqualTo("Assets/Fonts/NotoSansKR-Regular SDF.asset"));
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(font.atlasRenderMode, Is.EqualTo(GlyphRenderMode.SDFAA));
            Assert.That(font.sourceFontFile, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(font.sourceFontFile), Is.EqualTo("Assets/Fonts/NotoSansKR-Regular.otf"));
            Assert.That(font.glyphTable, Is.Empty);
            Assert.That(font.characterTable, Is.Empty);
            Assert.That(font.atlasTexture.width, Is.EqualTo(1));
            Assert.That(font.atlasTexture.height, Is.EqualTo(1));
        }

        [Test]
        public void HelloBuildsCenteredPanelWithKoreanBorderAndEnglishText()
        {
            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(HelloPath);

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.True);
            var panel = (StackPanel)asset.Build();
            Assert.That(panel.HorizontalAlignment, Is.EqualTo(HorizontalAlignment.Center));
            Assert.That(panel.VerticalAlignment, Is.EqualTo(VerticalAlignment.Center));
            Assert.That(panel.Children.Count, Is.EqualTo(2));
            var border = (Border)panel.Children[0];
            Assert.That(border.Background, Is.Not.Null);
            Assert.That(border.BorderBrush, Is.Not.Null);
            Assert.That(border.BorderThickness, Is.EqualTo(new Thickness(2)));
            Assert.That(border.Padding, Is.EqualTo(new Thickness(24, 16, 24, 16)));
            var korean = (TextBlock)border.Child;
            Assert.That(korean.Text, Is.EqualTo("안녕하세요, Xamlity!"));
            Assert.That(korean.FontSize, Is.EqualTo(32));
            Assert.That(((TextBlock)panel.Children[1]).Text, Is.EqualTo("Hello from XAML in Unity."));
        }

        [Test]
        public void SampleSceneReferencesHelloAndAwakeInjectsDefaultFont()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var roots = new List<XamlityRoot>();
                foreach (var gameObject in scene.GetRootGameObjects())
                    roots.AddRange(gameObject.GetComponentsInChildren<XamlityRoot>(true));
                Assert.That(roots.Count, Is.EqualTo(1));
                var root = roots[0];
                var serialized = new SerializedObject(root);
                Assert.That(serialized.FindProperty("xaml").objectReferenceValue,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<XamlAsset>(HelloPath)));

                typeof(XamlityRoot).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(root, null);

                Assert.That(root.Tree, Is.Not.Null);
                Assert.That(root.Tree.DefaultFont.Source, Is.SameAs(XamlitySettings.Load().DefaultFont));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
