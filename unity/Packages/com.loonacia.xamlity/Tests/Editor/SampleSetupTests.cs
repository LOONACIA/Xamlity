using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Xamlity.Controls;
using Xamlity.Data;
using Xamlity.Layout;
using Xamlity.Rendering;
using Xamlity.Styling;
using Xamlity.Tree;
using Xamlity.Unity;
using Canvas = Xamlity.Layout.Canvas;
using Grid = Xamlity.Layout.Grid;

namespace Xamlity.Editor.Tests
{
    // These tests depend on sample assets in the development repository only.
    public class SampleSetupTests
    {
        private const string HelloPath = "Assets/Samples/Hello/Hello.xaml";
        private const string HudPath = "Assets/Samples/Hud/Hud.xaml";
        private const string PanelPath = "Assets/Samples/Hud/Panel.png";
        private const string PlayerStatusPath = "Assets/Samples/Binding/PlayerStatus.xaml";
        private const string AppPath = "Assets/App.xaml";
        private const string PalettePath = "Assets/Samples/Styles/Palette.xaml";
        private const string StyleShowcasePath = "Assets/Samples/Styles/StyleShowcase.xaml";

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

        [Test]
        public void HudBuildsGridCanvasAndNineSliceSpriteImage()
        {
            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(HudPath);

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.True);
            var elements = DescendantsAndSelf(asset.Build()).ToList();
            Assert.That(elements.OfType<Grid>(), Is.Not.Empty);
            Assert.That(elements.OfType<Canvas>(), Is.Not.Empty);
            var image = elements.OfType<Image>().Single();
            Assert.That(image.Stretch, Is.EqualTo(Stretch.Fill));
            Assert.That(image.Source, Is.TypeOf<UnityImageSource>());
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(((UnityImageSource)image.Source).Sprite, Is.SameAs(sprite));
            Assert.That(sprite.border, Is.EqualTo(new Vector4(12, 12, 12, 12)));
            var importer = (TextureImporter)AssetImporter.GetAtPath(PanelPath);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.mipmapEnabled, Is.False);
        }

        [Test]
        public void HudSampleSceneReferencesHudWithFullHdReferenceResolution()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/HudSample.unity", OpenSceneMode.Additive);
            try
            {
                var roots = new List<XamlityRoot>();
                foreach (var gameObject in scene.GetRootGameObjects())
                    roots.AddRange(gameObject.GetComponentsInChildren<XamlityRoot>(true));
                Assert.That(roots.Count, Is.EqualTo(1));
                var root = roots[0];
                var serialized = new SerializedObject(root);
                Assert.That(serialized.FindProperty("xaml").objectReferenceValue,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<XamlAsset>(HudPath)));
                Assert.That(serialized.FindProperty("referenceResolution").vector2IntValue,
                    Is.EqualTo(new Vector2Int(1920, 1080)));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void PlayerStatusBuildsAndUpdatesTextAndConvertedHpWidth()
        {
            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(PlayerStatusPath);
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.True);
            var root = (FrameworkElement)asset.Build();
            var elements = DescendantsAndSelf(root).OfType<FrameworkElement>()
                .Where(element => !string.IsNullOrEmpty(element.Name)).ToDictionary(element => element.Name);
            var viewModelType = System.Type.GetType("Xamlity.Samples.Binding.PlayerStatusViewModel, Assembly-CSharp", true);
            var playerType = System.Type.GetType("Xamlity.Samples.Binding.PlayerModel, Assembly-CSharp", true);
            var viewModel = System.Activator.CreateInstance(viewModelType);
            var player = System.Activator.CreateInstance(playerType);
            playerType.GetProperty("Name").SetValue(player, "달빛 탐험가");
            playerType.GetProperty("MaxHp").SetValue(player, 200);
            playerType.GetProperty("Hp").SetValue(player, 150);
            viewModelType.GetProperty("Player").SetValue(viewModel, player);
            viewModelType.GetProperty("Gold").SetValue(viewModel, 321);
            try
            {
                root.DataContext = viewModel;
                Assert.That(((TextBlock)elements["PlayerName"]).Text, Is.EqualTo("달빛 탐험가"));
                Assert.That(((TextBlock)elements["CurrentHp"]).Text, Is.EqualTo("150"));
                Assert.That(((TextBlock)elements["MaximumHp"]).Text, Is.EqualTo("200"));
                Assert.That(((TextBlock)elements["GoldAmount"]).Text, Is.EqualTo("321"));
                Assert.That(((TextBlock)elements["BindingLiteral"]).Text, Is.EqualTo("{Binding}"));
                Assert.That(elements["HpFill"].Width, Is.EqualTo(480));

                playerType.GetProperty("Hp").SetValue(player, 50);
                Assert.That(((TextBlock)elements["CurrentHp"]).Text, Is.EqualTo("50"));
                Assert.That(elements["HpFill"].Width, Is.EqualTo(160));
                playerType.GetProperty("MaxHp").SetValue(player, 100);
                Assert.That(((TextBlock)elements["MaximumHp"]).Text, Is.EqualTo("100"));
                Assert.That(elements["HpFill"].Width, Is.EqualTo(320));
                playerType.GetProperty("Name").SetValue(player, "새벽 탐험가");
                viewModelType.GetProperty("Gold").SetValue(viewModel, 331);
                Assert.That(((TextBlock)elements["PlayerName"]).Text, Is.EqualTo("새벽 탐험가"));
                Assert.That(((TextBlock)elements["GoldAmount"]).Text, Is.EqualTo("331"));

                var replacement = System.Activator.CreateInstance(playerType);
                playerType.GetProperty("Name").SetValue(replacement, "별빛 탐험가");
                playerType.GetProperty("MaxHp").SetValue(replacement, 150);
                playerType.GetProperty("Hp").SetValue(replacement, 150);
                viewModelType.GetProperty("Player").SetValue(viewModel, replacement);
                playerType.GetProperty("Hp").SetValue(player, 0);
                Assert.That(((TextBlock)elements["PlayerName"]).Text, Is.EqualTo("별빛 탐험가"));
                Assert.That(((TextBlock)elements["CurrentHp"]).Text, Is.EqualTo("150"));
                Assert.That(((TextBlock)elements["MaximumHp"]).Text, Is.EqualTo("150"));
                Assert.That(elements["HpFill"].Width, Is.EqualTo(640));
                playerType.GetProperty("Hp").SetValue(replacement, 0);
                Assert.That(((TextBlock)elements["CurrentHp"]).Text, Is.EqualTo("0"));
                Assert.That(elements["HpFill"].Width, Is.Zero);
            }
            finally
            {
                root.DataContext = null;
            }
        }

        [Test]
        public void RatioToWidthConverterUsesInvariantParameterAndRejectsConvertBack()
        {
            var converterType = System.Type.GetType("Xamlity.Samples.Binding.RatioToWidthConverter, Assembly-CSharp", true);
            var converter = (IValueConverter)System.Activator.CreateInstance(converterType);
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(converter.Convert(0.5, typeof(double), "640.5", CultureInfo.CurrentCulture), Is.EqualTo(320.25));
                Assert.Throws<System.NotSupportedException>(() =>
                    converter.ConvertBack(320.25, typeof(double), "640.5", CultureInfo.CurrentCulture));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Test]
        public void BindingSampleSceneConnectsDemoAndCyclesPlayerStatus()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/BindingSample.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects()
                    .SelectMany(gameObject => gameObject.GetComponentsInChildren<XamlityRoot>(true)).ToList();
                Assert.That(roots.Count, Is.EqualTo(1));
                var root = roots[0];
                var serialized = new SerializedObject(root);
                Assert.That(serialized.FindProperty("xaml").objectReferenceValue,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<XamlAsset>(PlayerStatusPath)));
                Assert.That(serialized.FindProperty("referenceResolution").vector2IntValue,
                    Is.EqualTo(new Vector2Int(1920, 1080)));
                var demoType = System.Type.GetType("Xamlity.Samples.Binding.PlayerStatusDemo, Assembly-CSharp", true);
                var demos = scene.GetRootGameObjects()
                    .SelectMany(gameObject => gameObject.GetComponentsInChildren(demoType, true)).ToList();
                Assert.That(demos.Count, Is.EqualTo(1));
                var demo = demos[0];
                Assert.That(new SerializedObject(demo).FindProperty("root").objectReferenceValue, Is.SameAs(root));
                var camera = scene.GetRootGameObjects().SelectMany(gameObject => gameObject.GetComponentsInChildren<Camera>()).Single();
                Assert.That(camera.name, Is.EqualTo("Main Camera"));
                Assert.That(camera.CompareTag("MainCamera"), Is.True);
                Assert.That(camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(), Is.Not.Null);

                typeof(XamlityRoot).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(root, null);
                demoType.GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(demo, null);
                var viewModel = root.DataContext;
                Assert.That(viewModel, Is.Not.Null);
                var player = viewModel.GetType().GetProperty("Player").GetValue(viewModel);
                var hp = player.GetType().GetProperty("Hp");
                var gold = viewModel.GetType().GetProperty("Gold");
                Assert.That(hp.GetValue(player), Is.EqualTo(150));
                Assert.That(gold.GetValue(viewModel), Is.EqualTo(1200));
                for (int tick = 1; tick <= 11; tick++)
                {
                    demoType.GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(demo, 1f);
                    demoType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(demo, null);
                    Assert.That(hp.GetValue(player), Is.EqualTo(tick <= 10 ? 150 - tick * 15 : 150));
                    Assert.That(gold.GetValue(viewModel), Is.EqualTo(1200 + tick * 10));
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase(AppPath)]
        [TestCase(PalettePath)]
        [TestCase(StyleShowcasePath)]
        public void StyleSampleAssetsHaveImportedIr(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(path);
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.True);
        }

        [Test]
        public void StyleSampleUsesApplicationPaletteImplicitStylesAndTheme()
        {
            var appAsset = AssetDatabase.LoadAssetAtPath<XamlAsset>(AppPath);
            Assert.That(XamlitySettings.Load().ApplicationResources, Is.SameAs(appAsset));
            var theme = Resources.Load<XamlAsset>("Xamlity/Generic").BuildResourceDictionary(null);
            var app = appAsset.BuildResourceDictionary(theme);
            Assert.That(app.Keys.Cast<object>(), Is.EquivalentTo(new object[] { "CardStyle" }));
            Assert.That(app.MergedDictionaries.Count, Is.EqualTo(1));
            var palette = app.MergedDictionaries[0];
            Assert.That(palette.Source.OriginalString, Is.EqualTo("Samples/Styles/Palette.xaml"));
            Assert.That(palette.Count, Is.EqualTo(5));
            var root = (FrameworkElement)AssetDatabase.LoadAssetAtPath<XamlAsset>(StyleShowcasePath).Build(app, theme);
            var tree = new UIRoot(root, null, default, app, theme);
            tree.UpdateLayout(new Size(1920, 1080));
            var elements = DescendantsAndSelf(root).OfType<FrameworkElement>()
                .Where(element => !string.IsNullOrEmpty(element.Name)).ToDictionary(element => element.Name);
            var introduction = (TextBlock)elements["Introduction"];
            Assert.That(introduction.Style, Is.SameAs(root.Resources[typeof(TextBlock)]));
            Assert.That(introduction.Foreground, Is.SameAs(palette["TextBrush"]));
            Assert.That(introduction.FontSize, Is.EqualTo(root.Resources["BodyFontSize"]));
            Assert.That(((Style)root.Resources["HeadingStyle"]).BasedOn, Is.SameAs(introduction.Style));
            var card = (Border)elements["ResourceCard"];
            Assert.That(card.Style.BasedOn, Is.SameAs(app["CardStyle"]));
            Assert.That(card.Background, Is.SameAs(palette["PanelBrush"]));
            Assert.That(card.BorderBrush, Is.SameAs(palette["AccentBrush"]));
            Assert.That(card.Padding, Is.EqualTo(new Thickness(40)));
            var userControl = (UserControl)elements["ThemeCard"];
            var themeStyle = (Style)theme[typeof(UserControl)];
            Assert.That(userControl.Template, Is.SameAs(themeStyle.Setters.Cast<Setter>()
                .Single(setter => setter.Property == Control.TemplateProperty).Value));
            Assert.That(userControl.ApplyTemplate(), Is.False);
            var border = (Border)userControl.GetInheritanceChild(0);
            Assert.That(border.TemplatedParent, Is.SameAs(userControl));
            Assert.That(border.Background, Is.SameAs(palette["BackgroundBrush"]));
            Assert.That(border.BorderBrush, Is.SameAs(palette["AccentBrush"]));
            Assert.That(border.BorderThickness, Is.EqualTo(new Thickness(2)));
            Assert.That(border.Padding, Is.EqualTo(new Thickness(28)));
            Assert.That(border.Child, Is.TypeOf<ContentPresenter>());
        }

        [Test]
        public void StyleSampleStatusUpdatesStyleAndNamedTemplateTriggersAndRestoresValues()
        {
            var theme = Resources.Load<XamlAsset>("Xamlity/Generic").BuildResourceDictionary(null);
            var app = AssetDatabase.LoadAssetAtPath<XamlAsset>(AppPath).BuildResourceDictionary(theme);
            var root = (FrameworkElement)AssetDatabase.LoadAssetAtPath<XamlAsset>(StyleShowcasePath).Build(app, theme);
            var tree = new UIRoot(root, null, default, app, theme);
            var badge = DescendantsAndSelf(root).OfType<ContentControl>().Single(element => element.Name == "StatusBadge");
            var viewModelType = System.Type.GetType("Xamlity.Samples.Styles.StyleShowcaseViewModel, Assembly-CSharp", true);
            var viewModel = System.Activator.CreateInstance(viewModelType);
            var status = viewModelType.GetProperty("Status");
            try
            {
                root.DataContext = viewModel;
                tree.UpdateLayout(new Size(1920, 1080));
                var border = (Border)badge.GetTemplateChild("BadgeBorder");
                var presenter = (ContentPresenter)border.Child;
                foreach (string value in new[] { "정상", "위험", "정상" })
                {
                    status.SetValue(viewModel, value);
                    tree.UpdateLayout(new Size(1920, 1080));
                    Assert.That(badge.Content, Is.EqualTo(value));
                    Assert.That(badge.Background, Is.SameAs(app[value == "위험" ? "WarningBrush" : "AccentBrush"]));
                    Assert.That(border.Background, Is.SameAs(badge.Background));
                    Assert.That(border.Padding, Is.EqualTo(new Thickness(40, 24, 40, 24)));
                    Assert.That(border.BorderThickness, Is.EqualTo(new Thickness(value == "위험" ? 3 : 0)));
                    Assert.That(presenter.Content, Is.EqualTo(value));
                    var text = (TextBlock)presenter.GetInheritanceChild(0);
                    Assert.That(text.Text, Is.EqualTo(value));
                    Assert.That(text.Foreground, Is.SameAs(app["TextBrush"]));
                    Assert.That(text.FontSize, Is.EqualTo(26d));
                }
            }
            finally
            {
                root.DataContext = null;
            }
        }

        [Test]
        public void StyleSampleSceneConnectsDemoAndCyclesStatusEveryThreeSeconds()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/StyleSample.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects()
                    .SelectMany(gameObject => gameObject.GetComponentsInChildren<XamlityRoot>(true)).ToList();
                Assert.That(roots.Count, Is.EqualTo(1));
                var root = roots[0];
                var serialized = new SerializedObject(root);
                Assert.That(serialized.FindProperty("xaml").objectReferenceValue,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<XamlAsset>(StyleShowcasePath)));
                Assert.That(serialized.FindProperty("referenceResolution").vector2IntValue,
                    Is.EqualTo(new Vector2Int(1920, 1080)));
                var demoType = System.Type.GetType("Xamlity.Samples.Styles.StyleShowcaseDemo, Assembly-CSharp", true);
                var demos = scene.GetRootGameObjects()
                    .SelectMany(gameObject => gameObject.GetComponentsInChildren(demoType, true)).ToList();
                Assert.That(demos.Count, Is.EqualTo(1));
                var demo = demos[0];
                Assert.That(new SerializedObject(demo).FindProperty("root").objectReferenceValue, Is.SameAs(root));
                var camera = scene.GetRootGameObjects().SelectMany(gameObject => gameObject.GetComponentsInChildren<Camera>()).Single();
                Assert.That(camera.name, Is.EqualTo("Main Camera"));
                Assert.That(camera.CompareTag("MainCamera"), Is.True);
                var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                Assert.That(cameraData, Is.Not.Null);
                Assert.That(cameraData.renderPostProcessing, Is.True);

                typeof(XamlityRoot).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(root, null);
                demoType.GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(demo, null);
                Assert.That(root.Tree, Is.Not.Null);
                var viewModel = root.DataContext;
                var status = viewModel.GetType().GetProperty("Status");
                Assert.That(status.GetValue(viewModel), Is.EqualTo("정상"));
                var elapsed = demoType.GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
                var update = demoType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                elapsed.SetValue(demo, 0f);
                update.Invoke(demo, null);
                Assert.That(status.GetValue(viewModel), Is.EqualTo("정상"));
                foreach (string value in new[] { "위험", "정상", "위험" })
                {
                    elapsed.SetValue(demo, 3f);
                    update.Invoke(demo, null);
                    Assert.That(status.GetValue(viewModel), Is.EqualTo(value));
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static IEnumerable<UIElement> DescendantsAndSelf(UIElement element)
        {
            yield return element;
            if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                    foreach (var descendant in DescendantsAndSelf(child))
                        yield return descendant;
            }
            else if (element is Border border && border.Child != null)
            {
                foreach (var descendant in DescendantsAndSelf(border.Child))
                    yield return descendant;
            }
        }
    }
}
