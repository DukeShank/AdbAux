using System;
using Xunit;

namespace AdbAux.Tests
{
    /// <summary>The automatic update check: when it runs, and what a failure shows (rules in UpdateSchedule).</summary>
    public class UpdateScheduleTests
    {
        private static readonly DateTime Start = new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc);

        private static UpdateState Enabled()
        {
            var s = new UpdateState();
            s.Enable(Start);
            return s;
        }

        [Fact]
        public void DueAtOnceAfterTurningOnThenDaily()
        {
            UpdateState s = Enabled();
            Assert.True(UpdateSchedule.IsDue(s, Start, true));

            UpdateSchedule.RecordSuccess(s, Start);
            Assert.False(UpdateSchedule.IsDue(s, Start.AddHours(23), false));
            Assert.True(UpdateSchedule.IsDue(s, Start.AddHours(24), false));
        }

        [Fact]
        public void FirstFailureShowsLineOnlyAndRetriesAtNextStart()
        {
            UpdateState s = Enabled();
            UpdateNotice notice = UpdateSchedule.RecordFailure(s, Start, UpdateFailure.NoConnection, DateTime.MinValue);

            Assert.False(notice.ShowNotification);
            Assert.Contains("no internet connection", notice.Line);
            Assert.False(UpdateSchedule.IsDue(s, Start.AddDays(1), false));   // not again while AdbAux keeps running
            Assert.True(UpdateSchedule.IsDue(s, Start.AddHours(2), true));    // but at the next start
        }

        [Fact]
        public void SecondFailureWaitsUntilAWeekAfterTheFirstThenWeekly()
        {
            UpdateState s = Enabled();
            UpdateSchedule.RecordFailure(s, Start, UpdateFailure.NotResponding, DateTime.MinValue);
            UpdateNotice second = UpdateSchedule.RecordFailure(s, Start.AddDays(1), UpdateFailure.NotResponding, DateTime.MinValue);

            Assert.Null(second.Line);                 // the Basic tab line is shown for the first failure only
            Assert.False(second.ShowNotification);
            Assert.False(UpdateSchedule.IsDue(s, Start.AddDays(6), true));
            Assert.True(UpdateSchedule.IsDue(s, Start.AddDays(7), false));

            UpdateSchedule.RecordFailure(s, Start.AddDays(7), UpdateFailure.NotResponding, DateTime.MinValue);
            Assert.Equal(Start.AddDays(14), s.NextCheckUtc);
        }

        [Fact]
        public void ThirtyDaysWithoutSuccessNotifies()
        {
            UpdateState s = Enabled();
            UpdateSchedule.RecordSuccess(s, Start);
            UpdateSchedule.RecordFailure(s, Start.AddDays(1), UpdateFailure.NotResponding, DateTime.MinValue);
            UpdateNotice at29 = UpdateSchedule.RecordFailure(s, Start.AddDays(29), UpdateFailure.NotResponding, DateTime.MinValue);
            UpdateNotice at30 = UpdateSchedule.RecordFailure(s, Start.AddDays(30), UpdateFailure.NotResponding, DateTime.MinValue);

            Assert.False(at29.ShowNotification);
            Assert.True(at30.ShowNotification);
            Assert.Contains("30 days", at30.Line);
        }

        [Fact]
        public void TurningOnAgainStartsTheThirtyDaysAnew()
        {
            var s = new UpdateState();
            UpdateSchedule.RecordSuccess(s, Start);          // a success long ago
            s.Enable(Start.AddDays(100));                     // turned off, and on again 100 days later
            UpdateNotice notice = UpdateSchedule.RecordFailure(s, Start.AddDays(101), UpdateFailure.NoConnection, DateTime.MinValue);

            Assert.False(notice.ShowNotification);
        }

        [Fact]
        public void RequestLimitNeverNotifiesAndWaitsForGitHubsTime()
        {
            UpdateState s = Enabled();
            UpdateNotice notice = UpdateSchedule.RecordFailure(s, Start, UpdateFailure.RateLimited, Start.AddMinutes(40));

            Assert.False(notice.ShowNotification);
            Assert.Null(notice.Line);
            Assert.Equal(0, s.Failures);
            Assert.Equal(Start.AddMinutes(40), s.NextCheckUtc);
        }

        [Fact]
        public void CertificateErrorNotifiesAtFirstOccurrence()
        {
            UpdateState s = Enabled();
            UpdateNotice first = UpdateSchedule.RecordFailure(s, Start, UpdateFailure.SecureConnection, DateTime.MinValue);
            UpdateNotice again = UpdateSchedule.RecordFailure(s, Start.AddDays(7), UpdateFailure.SecureConnection, DateTime.MinValue);

            Assert.True(first.ShowNotification);
            Assert.False(again.ShowNotification);
        }

        [Fact]
        public void SuccessClearsTheFailures()
        {
            UpdateState s = Enabled();
            UpdateSchedule.RecordFailure(s, Start, UpdateFailure.NotResponding, DateTime.MinValue);
            UpdateSchedule.RecordSuccess(s, Start.AddHours(3));

            Assert.Equal(0, s.Failures);
            Assert.False(s.RetryAtNextStart);
            Assert.Equal(UpdateFailure.None, s.LastFailure);
            Assert.Equal(Start.AddHours(27), s.NextCheckUtc);
        }
    }
}
