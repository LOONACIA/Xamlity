using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Data;
using Xamlity.Diagnostics;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Data
{
    public class TwoWayBindingTests
    {
        private RecordingLogger logger;

        [SetUp]
        public void SetUp() => XamlityLog.Logger = logger = new RecordingLogger();

        [TearDown]
        public void TearDown() => XamlityLog.Logger = null;

        [Test]
        public void TargetChangesWriteSourceAndKeepBindingAndNotifications()
        {
            var vm = new Model();
            var target = new RecordingElement { DataContext = vm };
            BindingExpression expression = target.SetBinding(FrameworkElement.WidthProperty,
                new Binding("Count") { Mode = BindingMode.TwoWay });
            target.Changes.Clear();
            vm.Reads = 0;
            target.Width = 42.0;
            Assert.That(vm.Count, Is.EqualTo(42));
            Assert.That(vm.Writes, Is.EqualTo(1));
            Assert.That(vm.Reads, Is.EqualTo(2)); // One refresh and the assertion above.
            Assert.That(vm.ReadsDuringWrite, Is.Zero);
            Assert.That(target.Changes, Is.EqualTo(new[] { 42.0 }));
            Assert.That(target.ReadLocalValue(FrameworkElement.WidthProperty), Is.SameAs(expression));
            vm.Count = 7;
            Assert.That(target.Width, Is.EqualTo(7.0));
            Assert.That(logger.Messages, Is.Empty);
        }

        [Test]
        public void SetterCorrectionRefreshesTargetOnceAfterWriting()
        {
            var vm = new Model { Clamp = true };
            var target = new RecordingElement { DataContext = vm };
            target.SetBinding(FrameworkElement.WidthProperty, new Binding("Count") { Mode = BindingMode.TwoWay });
            target.Changes.Clear();
            vm.Reads = 0;
            target.Width = 150;
            Assert.That(vm.Reads, Is.EqualTo(1));
            Assert.That(vm.ReadsDuringWrite, Is.Zero);
            Assert.That(vm.Writes, Is.EqualTo(1));
            Assert.That(target.Width, Is.EqualTo(100));
            Assert.That(target.Changes, Is.EqualTo(new[] { 150.0, 100.0 }));
        }

        [TestCase("context")]
        [TestCase("intermediate")]
        [TestCase("missing")]
        public void UnresolvedPathChangesOnlyTargetAndRecoversWhenSourceAppears(string failure)
        {
            var vm = new Model();
            var target = new TextBlock { DataContext = failure == "context" ? null : vm };
            string path = failure == "intermediate" ? "Child.Count" : failure == "missing" ? "Missing" : "Count";
            var converter = new RecordingConverter { Forward = v => "converted" };
            var binding = new Binding(path) { Mode = BindingMode.TwoWay, Converter = converter };
            BindingExpression expression = target.SetBinding(TextBlock.TextProperty, binding);
            target.Text = "local";
            Assert.That(target.Text, Is.EqualTo("local"));
            Assert.That(vm.Writes, Is.Zero);
            Assert.That(converter.Calls, Is.Empty);
            Assert.That(target.ReadLocalValue(TextBlock.TextProperty), Is.SameAs(expression));
            Assert.That(logger.Messages.Count, Is.EqualTo(failure == "missing" ? 1 : 0));
            if (failure == "context")
                target.DataContext = vm;
            else if (failure == "intermediate")
            {
                vm.Child = new Model();
                vm.Raise("Child");
            }
            else
                return;
            Assert.That(target.Text, Is.EqualTo("converted"));
        }

        [Test]
        public void NestedPathWritesCurrentLeafAndReleasesOldLeaf()
        {
            var oldChild = new Model();
            var vm = new Model { Child = oldChild };
            var target = new TextBlock { DataContext = vm };
            target.SetBinding(TextBlock.TextProperty, new Binding("Child.Count") { Mode = BindingMode.TwoWay });
            target.Text = "12";
            Assert.That(oldChild.Count, Is.EqualTo(12));
            vm.Child = new Model();
            vm.Raise("Child");
            target.Text = "34";
            Assert.That(vm.Child.Count, Is.EqualTo(34));
            Assert.That(vm.Writes, Is.Zero);
            oldChild.Count = 99;
            Assert.That(target.Text, Is.EqualTo("34"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearingTwoWayBindingReleasesSource(bool clearValue)
        {
            var vm = new Model();
            var target = new TextBlock { DataContext = vm };
            target.SetBinding(TextBlock.TextProperty, new Binding("Count") { Mode = BindingMode.TwoWay });
            if (clearValue)
                target.ClearValue(TextBlock.TextProperty);
            else
                BindingOperations.ClearBinding(target, TextBlock.TextProperty);
            int reads = vm.Reads;
            vm.Raise("Count");
            Assert.That(vm.Reads, Is.EqualTo(reads));
            Assert.That(target.Text, Is.Empty);
            Assert.That(BindingOperations.GetBindingExpression(target, TextBlock.TextProperty), Is.Null);
            target.Text = "5";
            Assert.That(vm.Writes, Is.Zero);
        }

        [TestCase(null)] [TestCase("")] [TestCase(".")]
        public void TwoWaySourceItselfIsRejectedWithoutReplacingExistingValue(string path)
        {
            var target = new TextBlock { Text = "local" };
            Assert.That(Assert.Throws<InvalidOperationException>(() => target.SetBinding(TextBlock.TextProperty,
                new Binding(path) { Mode = BindingMode.TwoWay })).Message,
                Is.EqualTo("Two-way binding requires a property path."));
            Assert.That(Assert.Throws<InvalidOperationException>(() => target.SetBinding(TextBlock.TextProperty,
                new Binding { Mode = BindingMode.TwoWay })).Message,
                Is.EqualTo("Two-way binding requires a property path."));
            Assert.That(target.Text, Is.EqualTo("local"));
        }

        [Test]
        public void TwoWayDataContextBindingUsesParentsContext()
        {
            var vm = new Model { Child = new Model() };
            var parent = new StackPanel { DataContext = vm };
            var target = new TextBlock();
            parent.Children.Add(target);
            BindingExpression expression = target.SetBinding(FrameworkElement.DataContextProperty,
                new Binding("Child") { Mode = BindingMode.TwoWay });
            var replacement = new Model();
            target.DataContext = replacement;
            Assert.That(vm.Child, Is.SameAs(replacement));
            Assert.That(target.DataContext, Is.SameAs(replacement));
            Assert.That(target.ReadLocalValue(FrameworkElement.DataContextProperty), Is.SameAs(expression));
            vm.Child = new Model();
            vm.Raise("Child");
            Assert.That(target.DataContext, Is.SameAs(vm.Child));
        }

        [TestCase("ReadOnly")]
        [TestCase("PrivateSetter")]
        public void NonPublicSetterWarnsWithoutWriting(string path)
        {
            var target = new TextBlock { DataContext = new Model() };
            target.SetBinding(TextBlock.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay });
            target.Text = "changed";
            Assert.That(target.Text, Is.EqualTo("changed"));
            Assert.That(logger.Messages, Is.EqualTo(new[] {
                $"Binding source error: property '{path}' on '{typeof(Model).FullName}' is read-only. Path '{path}', target 'TextBlock.Text'." }));
        }

        [Test]
        public void PathWarningsPreserveFullPathRuntimeTypesAndRepeat()
        {
            var vm = new Model { Child = new Model() };
            var target = new RecordingElement { DataContext = vm };
            target.SetBinding(FrameworkElement.WidthProperty, "Child.Missing");
            vm.Child.Raise("Missing");
            string expected = $"Binding path error: property 'Missing' not found on '{typeof(Model).FullName}'. Path 'Child.Missing', target 'RecordingElement.Width'.";
            Assert.That(logger.Messages, Is.EqualTo(new[] { expected, expected }));
            Assert.That(target.Width, Is.NaN);
        }

        [TestCase("invalid", "System.String")]
        [TestCase(null, "null")]
        public void DefaultForwardConversionFailureWarnsAndUsesDefault(object value, string type)
        {
            var vm = new Model { Value = value };
            var target = new TextBlock { DataContext = vm };
            target.SetBinding(FrameworkElement.WidthProperty, "Value");
            vm.Raise("Value");
            string expected = $"Binding conversion error: cannot convert '{value ?? "null"}' ({type}) to 'System.Double'. Path 'Value', target 'TextBlock.Width'.";
            Assert.That(logger.Messages, Is.EqualTo(new[] { expected, expected }));
            Assert.That(target.Width, Is.NaN);
        }

        [Test]
        public void DefaultReverseConversionFailureWarnsWithoutWriting()
        {
            var vm = new Model();
            var target = new TextBlock { DataContext = vm };
            target.SetBinding(TextBlock.TextProperty, new Binding("Count") { Mode = BindingMode.TwoWay });
            target.Text = "invalid";
            Assert.That(vm.Writes, Is.Zero);
            Assert.That(target.Text, Is.EqualTo("invalid"));
            Assert.That(logger.Messages, Is.EqualTo(new[] {
                "Binding conversion error: cannot convert 'invalid' (System.String) to 'System.Int32'. Path 'Count', target 'TextBlock.Text'." }));
        }

        [Test]
        public void NullLoggerDiscardsAllBindingWarnings()
        {
            XamlityLog.Logger = null;
            var target = new TextBlock { DataContext = new Model { Value = "invalid" } };
            Assert.DoesNotThrow(() => {
                target.SetBinding(TextBlock.TextProperty, "Missing");
                target.SetBinding(FrameworkElement.WidthProperty, "Value");
                target.SetBinding(TextBlock.TextProperty, new Binding("ReadOnly") { Mode = BindingMode.TwoWay });
                target.Text = "changed";
                target.SetBinding(TextBlock.TextProperty, new Binding("Count") { Mode = BindingMode.TwoWay });
                target.Text = "invalid";
            });
        }

        [Test]
        public void ConverterReceivesValueIncludingNullAndInvariantArguments()
        {
            var vm = new Model { Value = 7 };
            var parameter = new object();
            var converter = new RecordingConverter { Forward = v => v == null ? "null value" : "converted" };
            var target = new TextBlock { DataContext = vm };
            target.SetBinding(TextBlock.TextProperty,
                new Binding("Value") { Converter = converter, ConverterParameter = parameter });
            Assert.That(target.Text, Is.EqualTo("converted"));
            vm.Value = null;
            vm.Raise("Value");
            Assert.That(target.Text, Is.EqualTo("null value"));
            Assert.That(converter.Calls, Is.EqualTo(new[] {
                (false, (object)7, typeof(string), parameter, CultureInfo.InvariantCulture),
                (false, (object)null, typeof(string), parameter, CultureInfo.InvariantCulture) }));
            Assert.That(logger.Messages, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConverterResultsMustAlreadyMatchTargetType(bool nullResult)
        {
            var converter = new RecordingConverter { Forward = v => nullResult ? null : (object)42 };
            var target = new TextBlock { DataContext = new Model() };
            DependencyProperty property = nullResult ? FrameworkElement.WidthProperty : TextBlock.TextProperty;
            target.SetBinding(property, new Binding("Count") { Converter = converter });
            Assert.That(target.GetValue(property), Is.EqualTo(property.DefaultMetadata.DefaultValue));
            Assert.That(logger.Messages, Is.EqualTo(new[] { nullResult
                ? "Binding conversion error: cannot convert 'null' (null) to 'System.Double'. Path 'Count', target 'TextBlock.Width'."
                : "Binding conversion error: cannot convert '42' (System.Int32) to 'System.String'. Path 'Count', target 'TextBlock.Text'." }));
        }

        [Test]
        public void ConverterUnsetValueUsesDefaultWithoutWarning()
        {
            var target = new TextBlock { DataContext = new Model() };
            var converter = new RecordingConverter { Forward = v => DependencyProperty.UnsetValue };
            target.SetBinding(TextBlock.TextProperty, new Binding("Count") { Converter = converter });
            Assert.That(target.Text, Is.Empty);
            Assert.That(logger.Messages, Is.Empty);
        }

        [Test]
        public void ConvertBackReceivesSourceTypeParameterAndInvariantCulture()
        {
            var vm = new Model();
            var parameter = new object();
            var converter = new RecordingConverter { Forward = v => "value " + v, Backward = v => 23 };
            var target = new TextBlock { DataContext = vm };
            target.SetBinding(TextBlock.TextProperty, new Binding("Count") {
                Mode = BindingMode.TwoWay, Converter = converter, ConverterParameter = parameter });
            converter.Calls.Clear();
            target.Text = "input";
            Assert.That(vm.Count, Is.EqualTo(23));
            Assert.That(target.Text, Is.EqualTo("value 23"));
            Assert.That(converter.Calls, Is.EqualTo(new[] {
                (true, (object)"input", typeof(int), parameter, CultureInfo.InvariantCulture),
                (false, (object)23, typeof(string), parameter, CultureInfo.InvariantCulture) }));
        }

        [TestCase("unset")]
        [TestCase("23")]
        [TestCase(null)]
        public void InvalidConvertBackResultDoesNotWriteOrRefreshSource(string result)
        {
            var vm = new Model();
            var converter = new RecordingConverter { Forward = v => "initial",
                Backward = v => result == "unset" ? DependencyProperty.UnsetValue : result };
            var target = new TextBlock { DataContext = vm };
            target.SetBinding(TextBlock.TextProperty, new Binding("Count") { Mode = BindingMode.TwoWay, Converter = converter });
            vm.Reads = 0;
            converter.Calls.Clear();
            target.Text = "input";
            Assert.That(vm.Writes, Is.Zero);
            Assert.That(vm.Reads, Is.Zero);
            Assert.That(target.Text, Is.EqualTo("input"));
            Assert.That(converter.Calls.Count, Is.EqualTo(1));
            if (result == "unset")
                Assert.That(logger.Messages, Is.Empty);
            else
                Assert.That(logger.Messages, Is.EqualTo(new[] {
                    $"Binding conversion error: cannot convert '{result ?? "null"}' ({(result == null ? "null" : "System.String")}) to 'System.Int32'. Path 'Count', target 'TextBlock.Text'." }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConverterExceptionsPropagate(bool reverse)
        {
            var error = new InvalidOperationException("Converter failed.");
            var vm = new Model();
            var converter = new RecordingConverter { Forward = v => "initial", Backward = v => throw error };
            var target = new TextBlock { DataContext = vm };
            if (!reverse)
                converter.Forward = v => throw error;
            var binding = new Binding("Count") { Mode = BindingMode.TwoWay, Converter = converter };
            if (reverse)
            {
                target.SetBinding(TextBlock.TextProperty, binding);
                Assert.That(Assert.Throws<InvalidOperationException>(() => target.Text = "input"), Is.SameAs(error));
            }
            else
                Assert.That(Assert.Throws<InvalidOperationException>(() => target.SetBinding(TextBlock.TextProperty, binding)), Is.SameAs(error));
            Assert.That(vm.Writes, Is.Zero);
            Assert.That(logger.Messages, Is.Empty);
        }

        [Test]
        public void UsedBindingIsSealedAndCanBeSharedByIndependentExpressions()
        {
            var first = new TextBlock { DataContext = new Model() };
            var second = new TextBlock { DataContext = new Model() };
            var binding = new Binding("Count") { Mode = BindingMode.TwoWay };
            BindingExpression firstExpression = first.SetBinding(TextBlock.TextProperty, binding);
            foreach (TestDelegate change in new TestDelegate[] {
                () => binding.Path = new PropertyPath("Value"), () => binding.Mode = BindingMode.OneWay,
                () => binding.Converter = new RecordingConverter(), () => binding.ConverterParameter = "parameter" })
                Assert.That(Assert.Throws<InvalidOperationException>(change).Message,
                    Is.EqualTo("Binding cannot be changed after it has been used."));
            BindingExpression secondExpression = second.SetBinding(TextBlock.TextProperty, binding);
            Assert.That(secondExpression, Is.Not.SameAs(firstExpression));
            Assert.That(secondExpression.ParentBinding, Is.SameAs(binding));
            first.Text = "11";
            second.Text = "22";
            Assert.That(first.Text, Is.EqualTo("11"));
            Assert.That(second.Text, Is.EqualTo("22"));
            BindingOperations.ClearBinding(first, TextBlock.TextProperty);
            Assert.Throws<InvalidOperationException>(() => binding.Mode = BindingMode.Default);
        }

        private sealed class RecordingLogger : IXamlityLogger
        {
            public List<string> Messages { get; } = new List<string>();
            public void LogWarning(string message) => Messages.Add(message);
        }

        private sealed class RecordingElement : FrameworkElement
        {
            public List<double> Changes { get; } = new List<double>();
            protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
            {
                base.OnPropertyChanged(e);
                if (e.Property == WidthProperty)
                    Changes.Add((double)e.NewValue);
            }
        }

        private sealed class Model : INotifyPropertyChanged
        {
            private int count;
            private bool writing;
            public int Reads { get; set; }
            public int Writes { get; private set; }
            public int ReadsDuringWrite { get; private set; }
            public bool Clamp { get; set; }
            public int Count
            {
                get { Reads++; if (writing) ReadsDuringWrite++; return count; }
                set
                {
                    Writes++;
                    writing = true;
                    count = Clamp ? Math.Max(0, Math.Min(100, value)) : value;
                    Raise("Count");
                    Raise(null);
                    writing = false;
                }
            }
            public Model Child { get; set; }
            public object Value { get; set; }
            public string ReadOnly => "read only";
            public string PrivateSetter { get; private set; } = "private";
            public event PropertyChangedEventHandler PropertyChanged;
            public void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private sealed class RecordingConverter : IValueConverter
        {
            public Func<object, object> Forward { get; set; } = v => v;
            public Func<object, object> Backward { get; set; } = v => v;
            public List<(bool, object, Type, object, CultureInfo)> Calls { get; } = new List<(bool, object, Type, object, CultureInfo)>();
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Calls.Add((false, value, targetType, parameter, culture));
                return Forward(value);
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Calls.Add((true, value, targetType, parameter, culture));
                return Backward(value);
            }
        }
    }
}
