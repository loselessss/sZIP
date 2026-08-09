using sZIP.Application;

namespace sZIP.Tests;

public sealed class UpdateCheckScheduleTests
{
    [Fact]
    public void AutomaticCheckIsDueOnlyOncePer24Hours()
    {
        var schedule = new UpdateCheckSchedule();
        var now = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

        Assert.True(schedule.IsDue(string.Empty, now));
        var marked = UpdateCheckSchedule.MarkChecked(now);
        Assert.False(schedule.IsDue(marked, now.AddHours(23).AddMinutes(59)));
        Assert.True(schedule.IsDue(marked, now.AddDays(1)));
    }

    [Fact]
    public void FutureOrInvalidTimestampDoesNotDisableChecksForever()
    {
        var schedule = new UpdateCheckSchedule();
        var now = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

        Assert.True(schedule.IsDue("invalid", now));
        Assert.True(schedule.IsDue(UpdateCheckSchedule.MarkChecked(now.AddDays(1)), now));
    }

    [Fact]
    public void SkippedVersionMatchesOnlyExactVersion()
    {
        Assert.True(UpdateCheckSchedule.IsSkipped("1.4.0", new ReleaseVersion(1, 4, 0)));
        Assert.False(UpdateCheckSchedule.IsSkipped("1.4.0", new ReleaseVersion(1, 4, 1)));
    }
}
