﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Xunit;

namespace Bit.Test.Common
{
    public static class TestHelper
    {
        public static void AssertPropertyEqual(object expected, object actual, params string[] excludedPropertyStrings)
        {
            var relevantExcludedProperties = excludedPropertyStrings.Where(name => !name.Contains('.')).ToList();
            if (expected == null)
            {
                Assert.Null(actual);
                return;
            }

            if (actual == null)
            {
                throw new Exception("Expected object is null but actual is not");
            }

            foreach (var expectedPi in expected.GetType().GetProperties().Where(pi => !relevantExcludedProperties.Contains(pi.Name)))
            {
                var actualPi = actual.GetType().GetProperty(expectedPi.Name);

                if (actualPi == null)
                {
                    var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
                    throw new Exception(string.Concat($"Expected actual object to contain a property named {expectedPi.Name}, but it does not\n",
                    $"Expected:\n{JsonConvert.SerializeObject(expected, settings)}\n",
                    $"Actual:\n{JsonConvert.SerializeObject(actual, new JsonSerializerSettings { Formatting = Formatting.Indented })}"));
                }

                if (expectedPi.PropertyType == typeof(string) || expectedPi.PropertyType.IsValueType)
                {
                    Assert.Equal(expectedPi.GetValue(expected), actualPi.GetValue(actual));
                }
                else
                {
                    var prefix = $"{expectedPi.PropertyType.Name}.";
                    var nextExcludedProperties = excludedPropertyStrings.Where(name => name.StartsWith(prefix))
                        .Select(name => name[prefix.Length..]).ToArray();
                    AssertPropertyEqual(expectedPi.GetValue(expected), actualPi.GetValue(actual), nextExcludedProperties);
                }
            }
        }

        public static Predicate<T> AssertEqualExpectedPredicate<T>(T expected) => (actual) =>
        {
            Assert.Equal(expected, actual);
            return true;
        };
    }
}
