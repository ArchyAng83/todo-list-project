using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using TodoList.Application.Common;
using TodoList.Application.Settings;
using TodoList.Web.Components.Layout;
using TodoList.Web.Services;

namespace TodoList.ComponentTests;

[Collection(ComponentCollection.Name)]
public sealed class MainLayoutTests : ComponentTestContext
{
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly ThemePreference _theme = new();

    public MainLayoutTests()
    {
        _currentUser.GetAsync().Returns(Task.FromResult<UserContext?>(null));

        Services.AddSingleton(_currentUser);
        Services.AddSingleton(_theme);
    }

    [Fact]
    public void MainLayout_AuthorizedUser_RendersNavigationAndSignOut()
    {
        this.AddAuthorization().SetAuthorized("Test User");

        var cut = Render<MainLayout>();

        cut.FindAll("a[href='/tasks']").ShouldNotBeEmpty();
        cut.FindAll("a[href='/projects']").ShouldNotBeEmpty();
        cut.FindAll("a[href='/tags']").ShouldNotBeEmpty();
        cut.FindAll("a[href='/settings']").ShouldNotBeEmpty();
        cut.FindAll("a[href='/account/logout']").ShouldNotBeEmpty();
        cut.FindAll("a[href='/account/login']").ShouldBeEmpty();
    }

    [Fact]
    public void MainLayout_AnonymousUser_RendersSignInWithoutNavigation()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<MainLayout>();

        cut.FindAll("a[href='/account/login']").ShouldNotBeEmpty();
        cut.FindAll("a[href='/tasks']").ShouldBeEmpty();
        cut.FindAll("a[href='/account/logout']").ShouldBeEmpty();
    }

    [Fact]
    public void MainLayout_LoadsPersistedThemePreferenceForTheCurrentUser()
    {
        var user = new UserContext(Guid.NewGuid(), "UTC");
        _currentUser.GetAsync().Returns(Task.FromResult<UserContext?>(user));

        var store = Substitute.For<IUserSettingsStore>();
        store.GetAsync(user.UserId, Arg.Any<CancellationToken>())
            .Returns(new UserSettings(
                "Test User",
                "en",
                "UTC",
                "dark",
                DayOfWeek.Monday,
                15));

        Services.AddSingleton(new UserSettingsService(
            store,
            Substitute.For<FluentValidation.IValidator<UpdateUserSettingsRequest>>()));

        this.AddAuthorization().SetAuthorized("Test User");

        Render<MainLayout>();

        _theme.Theme.ShouldBe("dark");
    }
}
