using Shouldly;
using TodoList.Domain.Projects;

namespace TodoList.UnitTests.Domain;

public sealed class ProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_TrimsNameAndIsActiveByDefault()
    {
        var project = Project.Create(UserId, "  Work  ", Now);

        project.Name.ShouldBe("Work");
        project.IsArchived.ShouldBeFalse();
        project.SortOrder.ShouldBe(0);
        project.UserId.ShouldBe(UserId);
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        Should.Throw<ArgumentException>(() => Project.Create(UserId, " ", Now));
    }

    [Fact]
    public void Rename_WithTooLongName_Throws()
    {
        var project = Project.Create(UserId, "Work", Now);
        var name = new string('a', Project.NameMaxLength + 1);

        Should.Throw<ArgumentException>(() => project.Rename(name, Now));
    }

    [Fact]
    public void Archive_SetsArchivedAndUpdatesTimestamp()
    {
        var project = Project.Create(UserId, "Work", Now);
        var later = Now.AddHours(1);

        project.Archive(later);

        project.IsArchived.ShouldBeTrue();
        project.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void Archive_IsIdempotent()
    {
        var project = Project.Create(UserId, "Work", Now);
        project.Archive(Now);
        var later = Now.AddHours(1);

        project.Archive(later);

        project.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void SetSortOrder_WithNegativeValue_Throws()
    {
        var project = Project.Create(UserId, "Work", Now);

        Should.Throw<ArgumentOutOfRangeException>(() => project.SetSortOrder(-1, Now));
    }
}
