using Shouldly;
using TodoList.Domain.Tags;

namespace TodoList.UnitTests.Domain;

public sealed class TagTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_WithNoColor_UsesDefaultColor()
    {
        var tag = Tag.Create(UserId, "work", Now);

        tag.Color.ShouldBe(Tag.DefaultColor);
    }

    [Fact]
    public void Create_NormalizesColorToUppercase()
    {
        var tag = Tag.Create(UserId, "work", Now, "#a1b2c3");

        tag.Color.ShouldBe("#A1B2C3");
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void Create_WithInvalidColor_Throws(string color)
    {
        Should.Throw<ArgumentException>(() => Tag.Create(UserId, "work", Now, color));
    }

    [Fact]
    public void Create_WithTooLongName_Throws()
    {
        var name = new string('a', Tag.NameMaxLength + 1);

        Should.Throw<ArgumentException>(() => Tag.Create(UserId, name, Now));
    }
}
