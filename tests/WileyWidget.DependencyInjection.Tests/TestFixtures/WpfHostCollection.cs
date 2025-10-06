using Xunit;

namespace WileyWidget.DependencyInjection.Tests.TestFixtures;

/// <summary>
/// Collection definition for tests that share the WpfHostFixture.
/// All test classes marked with [Collection("WpfHost")] will share the same fixture instance.
/// </summary>
[CollectionDefinition("WpfHost")]
public class WpfHostCollection : ICollectionFixture<WpfHostFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
