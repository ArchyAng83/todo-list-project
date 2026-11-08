using TodoList.Testing;

namespace TodoList.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<TodoListAppFixture>
{
    public const string Name = "integration";
}
