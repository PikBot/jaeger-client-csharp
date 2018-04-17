using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Jaeger.Core.Metrics
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MetricAttribute : Attribute
    {
        // Note: Java allows to pass a list of tags, however C# doesn't allow complex types for attribute constructors
        // so we have to use simplified constructors.

        public string Name { get; }

        public ReadOnlyDictionary<string, string> Tags { get; }

        public MetricAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Tags = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        }

        public MetricAttribute(string name, string tags)
            : this(name)
        {
            Tags = ParseTags(tags);
        }

        private static ReadOnlyDictionary<string, string> ParseTags(string tags)
        {
            string[] entries = tags.Split(',');

            Dictionary<string, string> tagsAsDict = new Dictionary<string, string>(entries.Length);
            foreach (string entry in entries)
            {
                string[] keyValue = entry.Split('=');
                if (keyValue.Length == 2)
                {
                    tagsAsDict[keyValue[0].Trim()] = keyValue[1].Trim();
                }
                else
                {
                    tagsAsDict[keyValue[0].Trim()] = "";
                }
            }

            return new ReadOnlyDictionary<string, string>(tagsAsDict);
        }
    }
}
