using System.Collections.Concurrent;
using backend.Models;

namespace backend.Services;

public class OpcStore
{
    private readonly ConcurrentDictionary<string, TagData> _tags = new();

    public void Set(TagData tag)
    {
        _tags[tag.TagName] = tag;
    }

    public TagData? Get(string tagName)
    {
        _tags.TryGetValue(tagName, out var value);
        return value;
    }

    public List<TagData> GetAll()
    {
        return _tags.Values.OrderBy(x => x.TagName).ToList();
    }
}