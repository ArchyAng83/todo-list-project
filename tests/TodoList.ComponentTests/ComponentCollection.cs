namespace TodoList.ComponentTests;

/// <summary>
/// Component tests share the process-wide UI culture, so they run serially.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ComponentCollection
{
    public const string Name = "component";
}
