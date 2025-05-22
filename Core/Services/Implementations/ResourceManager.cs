using System.Collections.ObjectModel;
using Core.Services.Interfaces;

namespace Core.Services.Implementations;

public class ResourceManager(ReadOnlyDictionary<string, string> resources) : IResourceManager
{
    public string Get(string name, params object[] format)
    {
        if (resources.TryGetValue(name, out string? text))
            return string.Format(text, format);
        throw new InvalidOperationException($"Key not found: {name}");
    }
}
