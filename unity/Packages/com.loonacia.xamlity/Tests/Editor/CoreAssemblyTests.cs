using System;
using NUnit.Framework;
using Xamlity.Layout;

namespace Xamlity.Editor.Tests
{
    public class CoreAssemblyTests
    {
        [Test]
        public void CoreAssemblyHasNoUnityReferences()
        {
            var assembly = typeof(Thickness).Assembly;

            Assert.That(assembly.GetName().Name, Is.EqualTo("Xamlity.Core"));
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                Assert.That(reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal), Is.False, reference.FullName);
                Assert.That(reference.Name.StartsWith("UnityEditor", StringComparison.Ordinal), Is.False, reference.FullName);
                Assert.That(reference.Name.StartsWith("Unity.", StringComparison.Ordinal), Is.False, reference.FullName);
            }
        }
    }
}
