using System;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Rendering
{
    public class ImageTests
    {
        private const string Namespace = "xmlns=\"https://github.com/loonacia/xamlity\"";
        private static readonly Size Viewport = new Size(200, 200);

        [TestCase(Stretch.None, 100, 50, 50, 75)]
        [TestCase(Stretch.Fill, 200, 200, 0, 0)]
        [TestCase(Stretch.Uniform, 200, 100, 0, 50)]
        [TestCase(Stretch.UniformToFill, 400, 200, 0, 0)]
        public void StretchDeterminesSizeAlignmentAndSlotClip(Stretch stretch, double width, double height,
            double x, double y)
        {
            var source = new FakeImageSource();
            var image = new Image { Source = source, Stretch = stretch };
            var root = new UIRoot(image);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(image.DesiredSize, Is.EqualTo(new Size(Math.Min(width, 200), height)));
            Assert.That(image.RenderSize, Is.EqualTo(new Size(width, height)));
            Assert.That(image.VisualOffset, Is.EqualTo(new Point(x, y)));
            Assert.That(context.Images, Is.EqualTo(new[] { ((ImageSource)source, new Rect(x, y, width, height)) }));
            if (stretch == Stretch.UniformToFill)
            {
                Assert.That(context.Calls.Select(call => call.Method),
                    Is.EqualTo(new[] { "PushClip", "DrawImage", "Pop" }));
                Assert.That(context.Calls[0].Rect, Is.EqualTo(new Rect(0, 0, 200, 200)));
            }
            else
                Assert.That(context.Calls.Select(call => call.Method), Is.EqualTo(new[] { "DrawImage" }));
        }

        [TestCase(Stretch.None, 100, 50)]
        [TestCase(Stretch.Fill, 50, 50)]
        [TestCase(Stretch.Uniform, 50, 25)]
        [TestCase(Stretch.UniformToFill, 100, 50)]
        public void SmallerConstraintAllowsDownscaling(Stretch stretch, double width, double height)
        {
            var image = new Image { Source = new FakeImageSource(), Stretch = stretch };
            new UIRoot(image).UpdateLayout(new Size(50, 50));
            Assert.That(image.RenderSize, Is.EqualTo(new Size(width, height)));
        }

        [TestCase(Stretch.None, 100, 50)]
        [TestCase(Stretch.Fill, 200, 80)]
        [TestCase(Stretch.Uniform, 160, 80)]
        [TestCase(Stretch.UniformToFill, 200, 100)]
        public void ArrangeRecomputesScaleForFinalSize(Stretch stretch, double width, double height)
        {
            var image = new Image { Source = new FakeImageSource(), Stretch = stretch };
            image.Measure(new Size(50, 50));
            image.Arrange(new Rect(0, 0, 200, 80));
            Assert.That(image.RenderSize, Is.EqualTo(new Size(width, height)));
        }

        [TestCase(Stretch.None)]
        [TestCase(Stretch.Fill)]
        [TestCase(Stretch.Uniform)]
        [TestCase(Stretch.UniformToFill)]
        public void InfiniteConstraintsPreserveNaturalSizeOrUseTheFiniteAxis(Stretch stretch)
        {
            var image = new Image { Source = new FakeImageSource(), Stretch = stretch };
            image.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.That(image.DesiredSize, Is.EqualTo(new Size(100, 50)));
            image.Measure(new Size(double.PositiveInfinity, 80));
            Assert.That(image.DesiredSize, Is.EqualTo(stretch == Stretch.None
                ? new Size(100, 50) : new Size(160, 80)));
            image.Measure(new Size(160, double.PositiveInfinity));
            Assert.That(image.DesiredSize, Is.EqualTo(stretch == Stretch.None
                ? new Size(100, 50) : new Size(160, 80)));
        }

        [Test]
        public void HorizontalStackPanelUsesFiniteHeightForUniformImage()
        {
            var image = new Image { Source = new FakeImageSource() };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Height = 80 };
            panel.Children.Add(image);
            new UIRoot(panel).UpdateLayout(Viewport);
            Assert.That(image.Stretch, Is.EqualTo(Stretch.Uniform));
            Assert.That(image.DesiredSize, Is.EqualTo(new Size(160, 80)));
            Assert.That(image.RenderSize, Is.EqualTo(new Size(160, 80)));
        }

        [TestCase(Stretch.None, 0, 50, 0, 50)]
        [TestCase(Stretch.Fill, 0, 50, 0, 200)]
        [TestCase(Stretch.Uniform, 0, 50, 0, 0)]
        [TestCase(Stretch.UniformToFill, 0, 50, 0, 200)]
        [TestCase(Stretch.None, 100, 0, 100, 0)]
        [TestCase(Stretch.Fill, 100, 0, 200, 0)]
        [TestCase(Stretch.Uniform, 100, 0, 0, 0)]
        [TestCase(Stretch.UniformToFill, 100, 0, 200, 0)]
        [TestCase(Stretch.None, 0, 0, 0, 0)]
        [TestCase(Stretch.Fill, 0, 0, 0, 0)]
        [TestCase(Stretch.Uniform, 0, 0, 0, 0)]
        [TestCase(Stretch.UniformToFill, 0, 0, 0, 0)]
        public void ZeroSourceDimensionsDoNotDivideByZero(Stretch stretch, double sourceWidth,
            double sourceHeight, double width, double height)
        {
            var image = new Image { Source = new FakeImageSource(sourceWidth, sourceHeight), Stretch = stretch };
            var root = new UIRoot(image);
            root.UpdateLayout(Viewport);
            Assert.That(image.DesiredSize, Is.EqualTo(new Size(width, height)));
            Assert.That(image.RenderSize, Is.EqualTo(new Size(width, height)));
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(context.Images.Single().Rect.Size, Is.EqualTo(new Size(width, height)));
        }

        [Test]
        public void NullSourceHasZeroSizeAndNoDrawingCommands()
        {
            var image = new Image();
            var root = new UIRoot(image);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(image.Source, Is.Null);
            Assert.That(image.DesiredSize, Is.EqualTo(new Size(0, 0)));
            Assert.That(image.RenderSize, Is.EqualTo(new Size(0, 0)));
            Assert.That(context.Calls, Is.Empty);
        }

        [Test]
        public void SourceAndStretchChangesRemeasureAndClearingSourceRemovesDrawing()
        {
            var image = new Image { Source = new FakeImageSource() };
            var border = new Border { Child = image };
            var root = new UIRoot(border);
            root.UpdateLayout(Viewport);
            root.Render(new EmptyDrawingContext());

            image.Source = new FakeImageSource(50, 100);
            Assert.That(image.IsMeasureValid, Is.False);
            Assert.That(border.IsMeasureValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
            root.UpdateLayout(Viewport);
            Assert.That(image.RenderSize, Is.EqualTo(new Size(100, 200)));
            root.Render(new EmptyDrawingContext());

            image.Stretch = Stretch.None;
            Assert.That(image.IsMeasureValid, Is.False);
            Assert.That(border.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            Assert.That(image.RenderSize, Is.EqualTo(new Size(50, 100)));
            Assert.That(root.IsRenderDirty, Is.True);
            root.Render(new EmptyDrawingContext());

            image.ClearValue(Image.SourceProperty);
            Assert.That(image.IsMeasureValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(image.RenderSize, Is.EqualTo(new Size(0, 0)));
            Assert.That(context.Calls, Is.Empty);
        }

        [Test]
        public void NestedBorderOffsetsImageIntoAbsoluteCoordinates()
        {
            var source = new FakeImageSource();
            var image = new Image { Source = source, Stretch = Stretch.Fill };
            var inner = new Border { Margin = new Thickness(3), Padding = new Thickness(2), Child = image };
            var outer = new Border { Margin = new Thickness(5, 7, 0, 0),
                BorderThickness = new Thickness(2, 3, 4, 5), Padding = new Thickness(6, 8, 10, 12), Child = inner };
            var root = new UIRoot(outer);
            root.UpdateLayout(new Size(120, 100));
            var context = new RecordingDrawingContext();
            root.Render(context);
            var command = context.Images.Single();
            Assert.That(command.Source, Is.SameAs(source));
            Assert.That(command.Rect.Location, Is.EqualTo(new Point(18, 23)));
            Assert.That(command.Rect.Width, Is.EqualTo(83).Within(1e-10));
            Assert.That(command.Rect.Height, Is.EqualTo(55).Within(1e-10));
        }

        [TestCase(Stretch.Uniform)]
        [TestCase(Stretch.UniformToFill)]
        public void UnchangedLayoutAndRenderAllocateNothing(Stretch stretch)
        {
            var root = new UIRoot(new Image { Source = new FakeImageSource(), Stretch = stretch });
            var context = new EmptyDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);
            long before = GC.GetAllocatedBytesForCurrentThread();
            root.UpdateLayout(Viewport);
            root.Render(context);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [TestCase("")]
        [TestCase("Stretch=\"None\"")]
        [TestCase("Stretch=\"Fill\"")]
        [TestCase("Stretch=\"Uniform\"")]
        [TestCase("Stretch=\"UniformToFill\"")]
        public void ImageWithoutSourceCanRoundTripThroughXaml(string attribute)
        {
            XamlParseResult result = XamlParser.Parse("<Image " + Namespace + " " + attribute + "/>", "image.xaml");
            Assert.That(result.Errors, Is.Empty);
            var document = XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(result.Document));
            var image = (Image)XamlBuilder.Build(document);
            Stretch expected = attribute.Length == 0 ? Stretch.Uniform
                : (Stretch)Enum.Parse(typeof(Stretch), attribute.Split('"')[1]);
            Assert.That(image.Stretch, Is.EqualTo(expected));
            Assert.That(image.Source, Is.Null);
        }

        [Test]
        public void UnsupportedStretchIsReported()
        {
            XamlParseResult result = XamlParser.Parse("<Image " + Namespace + " Stretch=\"Tile\"/>", "image.xaml");
            Assert.That(result.Errors.Count, Is.EqualTo(1));
            var image = new Image { Source = new FakeImageSource(), Stretch = (Stretch)99 };
            Assert.Throws<ArgumentOutOfRangeException>(() => image.Measure(Viewport));
        }

        private sealed class FakeImageSource : ImageSource
        {
            internal FakeImageSource(double width = 100, double height = 50)
            {
                Width = width;
                Height = height;
            }

            public override double Width { get; }
            public override double Height { get; }
        }
    }
}
