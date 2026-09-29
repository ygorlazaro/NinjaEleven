using Xunit;

namespace NinjaEleven.IntegrationTests;

[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection : ICollectionFixture<SequentialFixture>
{
}

public class SequentialFixture { }