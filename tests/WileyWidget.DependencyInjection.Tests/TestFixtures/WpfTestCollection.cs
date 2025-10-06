using Xunit;

namespace WileyWidget.DependencyInjection.Tests.TestFixtures;

/// <summary>
/// Collection definition for tests that share the WpfTestFactory.
/// All test classes marked with [Collection("WpfTest")] will share the same factory instance.
/// Follows Microsoft's WebApplicationFactory pattern for integration testing.
/// </summary>
[CollectionDefinition("WpfTest")]
public class WpfTestCollection : ICollectionFixture<WpfTestFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}