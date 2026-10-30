using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Xamlity.Styling;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Styling
{
    public class ResourceDictionaryTests
    {
        [Test]
        public void LookupUsesOwnEntriesThenMergedDictionariesInReverseRecursively()
        {
            var first = new ResourceDictionary { ["key"] = 1, ["first"] = 10 };
            var nested = new ResourceDictionary { ["key"] = 2 };
            var last = new ResourceDictionary();
            last.MergedDictionaries.Add(nested);
            var dictionary = new ResourceDictionary();
            dictionary.MergedDictionaries.Add(first);
            dictionary.MergedDictionaries.Add(last);
            Assert.That(dictionary.Contains("key"), Is.True);
            Assert.That(dictionary["key"], Is.EqualTo(2));
            Assert.That(dictionary["first"], Is.EqualTo(10));
            dictionary["key"] = 3;
            Assert.That(dictionary["key"], Is.EqualTo(3));
            dictionary["key"] = null;
            Assert.That(dictionary["key"], Is.Null);
            Assert.That(dictionary.Contains("key"), Is.True);
            Assert.That(dictionary["missing"], Is.Null);
            Assert.That(dictionary.Contains("missing"), Is.False);
        }

        [Test]
        public void DictionaryOperationsOnlyAffectOwnEntries()
        {
            IDictionary dictionary = new ResourceDictionary { ["own"] = 1 };
            var merged = new ResourceDictionary { ["merged"] = 2, ["own"] = 3 };
            ((ResourceDictionary)dictionary).MergedDictionaries.Add(merged);
            Assert.That(dictionary.Count, Is.EqualTo(1));
            Assert.That(dictionary.Keys, Is.EquivalentTo(new[] { "own" }));
            Assert.That(dictionary.Values, Is.EquivalentTo(new[] { 1 }));
            Assert.That(dictionary.Cast<DictionaryEntry>().Single().Key, Is.EqualTo("own"));
            var copy = new DictionaryEntry[1];
            dictionary.CopyTo(copy, 0);
            Assert.That(copy[0].Value, Is.EqualTo(1));
            dictionary.Remove("merged");
            Assert.That(dictionary["merged"], Is.EqualTo(2));
            dictionary.Remove("own");
            Assert.That(dictionary["own"], Is.EqualTo(3));
            dictionary.Add("own", 4);
            dictionary.Clear();
            Assert.That(dictionary.Count, Is.Zero);
            Assert.That(dictionary.Contains("merged"), Is.True);
            Assert.That(dictionary.IsFixedSize || dictionary.IsReadOnly || dictionary.IsSynchronized, Is.False);
            Assert.That(dictionary.SyncRoot, Is.Not.Null);
        }

        [Test]
        public void AddUsesEqualityAndRejectsDuplicateOwnKeys()
        {
            var dictionary = new ResourceDictionary();
            dictionary.Add(new string('a', 2), 1);
            Assert.Throws<ArgumentException>(() => dictionary.Add(new string('a', 2), 2));
            Assert.That(dictionary[new string('a', 2)], Is.EqualTo(1));
            dictionary.Add(typeof(FrameworkElement), 3);
            Assert.Throws<ArgumentException>(() => dictionary.Add(typeof(FrameworkElement), 4));
        }

        [Test]
        public void NullKeysAndNullMergedDictionariesAreRejected()
        {
            var dictionary = new ResourceDictionary();
            Assert.Throws<ArgumentNullException>(() => _ = dictionary[null]);
            Assert.Throws<ArgumentNullException>(() => dictionary[null] = 1);
            Assert.Throws<ArgumentNullException>(() => dictionary.Add(null, 1));
            Assert.Throws<ArgumentNullException>(() => dictionary.Contains(null));
            Assert.Throws<ArgumentNullException>(() => dictionary.Remove(null));
            Assert.Throws<ArgumentNullException>(() => dictionary.MergedDictionaries.Add(null));
            dictionary.MergedDictionaries.Add(new ResourceDictionary());
            Assert.Throws<ArgumentNullException>(() => dictionary.MergedDictionaries[0] = null);
        }

        [Test]
        public void ElementResourcesAreLazyAndReplaceable()
        {
            var element = new FrameworkElement();
            ResourceDictionary initial = element.Resources;
            Assert.That(element.Resources, Is.SameAs(initial));
            var replacement = new ResourceDictionary();
            element.Resources = replacement;
            Assert.That(element.Resources, Is.SameAs(replacement));
            element.Resources = null;
            Assert.That(element.Resources, Is.Not.Null.And.Not.SameAs(replacement));
        }
    }
}
