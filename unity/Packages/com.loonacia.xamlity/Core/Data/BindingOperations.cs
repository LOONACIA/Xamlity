using System;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Data
{
    public static class BindingOperations
    {
        public static BindingExpression SetBinding(DependencyObject target, DependencyProperty dp, Binding binding)
        {
            ValidateTarget(target, dp);
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));
            if (binding.Mode == BindingMode.TwoWay && (binding.Path == null || binding.Path.Segments.Length == 0))
                throw new InvalidOperationException("Two-way binding requires a property path.");
            binding.Seal();
            var expression = new BindingExpression((FrameworkElement)target, dp, binding);
            target.SetExpression(dp, expression);
            return expression;
        }

        public static BindingExpression GetBindingExpression(DependencyObject target, DependencyProperty dp)
        {
            ValidateTarget(target, dp);
            return target.ReadLocalValue(dp) as BindingExpression;
        }

        public static void ClearBinding(DependencyObject target, DependencyProperty dp)
        {
            ValidateTarget(target, dp);
            if (target.ReadLocalValue(dp) is BindingExpression)
                target.ClearValue(dp);
        }

        private static void ValidateTarget(DependencyObject target, DependencyProperty dp)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (dp == null)
                throw new ArgumentNullException(nameof(dp));
            if (!(target is FrameworkElement))
                throw new InvalidOperationException("Bindings are only supported on FrameworkElement targets.");
        }
    }
}
