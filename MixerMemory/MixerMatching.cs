using System;

namespace MixerMemory
{
    struct CategoryData
    {
        public string Name { get; set; }
        public float Volume { get; set; }
    }

    public enum MatchType
    {
        NameIs,
        NameContains,
        PathIs,
        PathContains,
        Always
    }

    struct ApplicationData
    {
        public MatchType Type { get; set; }
        public string Match { get; set; }
        public string Category { get; set; }
    }

    struct MixerMatching
    {
        public static Func<string, string, string, bool>[] Matchers = new Func<string, string, string, bool>[]
        {
            (name, path, match) => name == match,
            (name, path, match) => name.Contains(match),
            (name, path, match) => path == match,
            (name, path, match) => path.Contains(match),
            (name, path, match) => true,
        };

        public CategoryData[] Categories { get; set; }
        public ApplicationData[] Rules { get; set; }
    }
}
