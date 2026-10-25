using System.Reflection;
using FlyleafLib;
using FlyleafLib.Plugins;
using Xunit;

namespace SubEdit.Player.Tests;

public class PluginRegistryTests
{
    [Fact]
    public void BuiltInRegistry_MatchesOriginalDiscoveryIncludingOrder()
    {
        var registryField = typeof(PluginsEngine).GetField(
            "BuiltInPluginTypes", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(registryField);
        var registry = Assert.IsType<Type[]>(registryField.GetValue(null));
        var pluginBaseType = typeof(PluginBase);
        var originalDiscovery = pluginBaseType.Assembly.GetTypes()
            .Where(type => pluginBaseType.IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
            .ToArray();

        Assert.Equal(3, registry.Length);
        Assert.Equal(new[] { typeof(OpenDefault), typeof(OpenSubtitles), typeof(StreamSuggester) }, registry);
        Assert.Equal(originalDiscovery, registry);
        Assert.All(registry, type =>
        {
            Assert.True(pluginBaseType.IsAssignableFrom(type));
            Assert.True(type.IsClass);
            Assert.False(type.IsAbstract);
        });
    }
}
