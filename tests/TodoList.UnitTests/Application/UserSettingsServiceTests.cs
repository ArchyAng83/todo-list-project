using Shouldly;
using TodoList.Application.Common;
using TodoList.Application.Settings;

namespace TodoList.UnitTests.Application;

public sealed class UserSettingsServiceTests
{
    private static readonly UserContext User = new(Guid.NewGuid(), "UTC");

    private static readonly UserSettings Current = new(
        "User",
        "en",
        "UTC",
        "system",
        DayOfWeek.Monday,
        15);

    [Fact]
    public async Task UpdateAsync_InvalidTheme_ReturnsValidationFailureWithoutSaving()
    {
        var store = new FakeStore(Current);
        var service = CreateService(store);

        var result = await service.UpdateAsync(
            User,
            new UpdateUserSettingsRequest("User", "en", "UTC", "neon", DayOfWeek.Monday, 15));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
        store.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_NormalizesCultureAndTheme()
    {
        var store = new FakeStore(Current);
        var service = CreateService(store);

        var result = await service.UpdateAsync(
            User,
            new UpdateUserSettingsRequest(
                "  Renamed  ",
                "RU",
                "UTC",
                "DARK",
                DayOfWeek.Sunday,
                30));

        result.IsSuccess.ShouldBeTrue();
        result.Value.DisplayName.ShouldBe("Renamed");
        result.Value.Culture.ShouldBe("ru");
        result.Value.Theme.ShouldBe("dark");
        result.Value.WeekStart.ShouldBe(DayOfWeek.Sunday);
        result.Value.DefaultReminderMinutes.ShouldBe(30);

        store.SaveCount.ShouldBe(1);
        store.Saved.ShouldBe(result.Value);
    }

    [Fact]
    public async Task UpdateAsync_MissingUser_ReturnsNotFound()
    {
        var store = new FakeStore(null);
        var service = CreateService(store);

        var result = await service.UpdateAsync(
            User,
            new UpdateUserSettingsRequest(
                "User",
                "en",
                "UTC",
                "system",
                DayOfWeek.Monday,
                15));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("not_found");
    }

    private static UserSettingsService CreateService(IUserSettingsStore store)
        => new(store, new UpdateUserSettingsRequestValidator());

    private sealed class FakeStore(UserSettings? existing) : IUserSettingsStore
    {
        public int SaveCount { get; private set; }

        public UserSettings? Saved { get; private set; }

        public Task<UserSettings?> GetAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(existing);

        public Task<bool> SaveAsync(
            Guid userId,
            UserSettings settings,
            CancellationToken cancellationToken = default)
        {
            if (existing is null)
            {
                return Task.FromResult(false);
            }

            SaveCount++;
            Saved = settings;

            return Task.FromResult(true);
        }
    }
}
