using Microsoft.CodeAnalysis;

namespace ILGPU.Analyzers;

public static class ResourceUtil
{
    public static LocalizableResourceString GetLocalized(string name)
    {
        return new LocalizableResourceString(name,
            Resources.ResourceManager,
            typeof(Resources));
    }
}
