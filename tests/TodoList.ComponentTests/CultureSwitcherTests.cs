using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TodoList.Web.Components.Shared;

namespace TodoList.ComponentTests;

[Collection(ComponentCollection.Name)]
public sealed class CultureSwitcherTests : ComponentTestContext
{
    [Fact]
    public void CultureSwitcher_EnglishUi_LinksBothCulturesAndMarksEnglish()
    {
        var cut = Render<CultureSwitcher>();

        var english = cut.Find("a[href*='culture=en']");
        var russian = cut.Find("a[href*='culture=ru']");

        english.TextContent.ShouldBe("EN");
        russian.TextContent.ShouldBe("RU");
        english.ClassList.ShouldContain("is-active");
        russian.ClassList.ShouldNotContain("is-active");
    }

    [Fact]
    public void CultureSwitcher_RussianUi_MarksRussian()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru");

        var cut = Render<CultureSwitcher>();

        cut.Find("a[href*='culture=ru']").ClassList.ShouldContain("is-active");
        cut.Find("a[href*='culture=en']").ClassList.ShouldNotContain("is-active");
    }

    [Fact]
    public void CultureSwitcher_IncludesCurrentPathInRedirect()
    {
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/projects");

        var cut = Render<CultureSwitcher>();

        cut.Find("a[href*='culture=en']")
            .GetAttribute("href")
            .ShouldBe("/culture/set?culture=en&redirect=%2Fprojects");
    }
}
