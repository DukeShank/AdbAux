using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AdbAux
{
    /// <summary>The user's answer to "check for updates automatically?" (asked once at first start).</summary>
    public enum UpdateCheckChoice
    {
        NotAsked,
        Yes,
        No
    }

    /// <summary>Why an update check failed. Each type has its own message and notification rule.</summary>
    public enum UpdateFailure
    {
        None,
        NoConnection,       // no network, or the name api.github.com could not be resolved
        NotResponding,      // timeout, connection refused, or a GitHub server error (HTTP 5xx)
        RateLimited,        // GitHub allows 60 requests per hour per IP address without sign-in
        SecureConnection,   // certificate or TLS error: something may be intercepting HTTPS
        UnexpectedReply     // a reply AdbAux cannot read
    }

    /// <summary>
    /// State of the automatic update check on this PC. Saved in settings.xml, but not in a settings
    /// backup, because it belongs to this PC. All times are UTC; DateTime.MinValue means "never".
    /// </summary>
    public class UpdateState
    {
        public DateTime EnabledUtc;          // when the automatic check was turned on
        public DateTime LastAttemptUtc;
        public DateTime LastSuccessUtc;
        public DateTime FirstFailureUtc;     // first failure of the current series of failures
        public int Failures;                 // automatic checks failed in a row (request limits not counted)
        public UpdateFailure LastFailure = UpdateFailure.None;
        public bool RetryAtNextStart;        // after the first failure: try again when AdbAux starts next
        public DateTime NextCheckUtc;        // MinValue: due now
        public string SkippedAdbAuxVersion = "";
        public string SkippedScrcpyVersion = "";

        /// <summary>Makes the times UTC (the settings file may have been edited, or written in another time zone).</summary>
        public void Sanitize()
        {
            EnabledUtc = AsUtc(EnabledUtc);
            LastAttemptUtc = AsUtc(LastAttemptUtc);
            LastSuccessUtc = AsUtc(LastSuccessUtc);
            FirstFailureUtc = AsUtc(FirstFailureUtc);
            NextCheckUtc = AsUtc(NextCheckUtc);
            if (Failures < 0)
            {
                Failures = 0;
            }
            if (!Enum.IsDefined(typeof(UpdateFailure), LastFailure))
            {
                LastFailure = UpdateFailure.None;
            }
            SkippedAdbAuxVersion = SkippedAdbAuxVersion ?? "";
            SkippedScrcpyVersion = SkippedScrcpyVersion ?? "";
        }

        /// <summary>Starts a new schedule: used when the automatic check is turned on.</summary>
        public void Enable(DateTime nowUtc)
        {
            EnabledUtc = nowUtc;
            Failures = 0;
            FirstFailureUtc = DateTime.MinValue;
            LastFailure = UpdateFailure.None;
            RetryAtNextStart = false;
            NextCheckUtc = DateTime.MinValue;
        }

        private static DateTime AsUtc(DateTime d)
        {
            if (d == DateTime.MinValue || d.Kind == DateTimeKind.Utc)
            {
                return DateTime.SpecifyKind(d, DateTimeKind.Utc);
            }
            return d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }
    }

    /// <summary>What to show after an automatic check failed.</summary>
    public sealed class UpdateNotice
    {
        public bool ShowNotification;   // Windows notification
        public string Line;             // line on the Basic tab; null: leave the Basic tab as it is
    }

    /// <summary>
    /// When the automatic check runs, and what a failure shows. Rules (agreed for 0.6.0):
    /// - after a success: the next check 24 hours later;
    /// - first failure: try again at the next start of AdbAux; a line on the Basic tab;
    /// - second failure: try again 7 days after the first failure; then once a week;
    /// - no successful check for 30 days: a Windows notification and a line at each failed check;
    /// - certificate (secure connection) error: a notification at its first occurrence;
    /// - request limit: never a notification; try again at the time GitHub gives.
    /// Manual checks do not use this schedule.
    /// </summary>
    public static class UpdateSchedule
    {
        public static readonly TimeSpan Interval = TimeSpan.FromHours(24);
        public static readonly TimeSpan RetryInterval = TimeSpan.FromDays(7);
        public static readonly TimeSpan WarnAfter = TimeSpan.FromDays(30);
        private static readonly TimeSpan RateLimitFallback = TimeSpan.FromHours(1);

        /// <param name="firstCheckSinceStart">True until the first automatic check since AdbAux started has run.</param>
        public static bool IsDue(UpdateState s, DateTime nowUtc, bool firstCheckSinceStart)
        {
            if (firstCheckSinceStart && s.RetryAtNextStart)
            {
                return true;
            }
            return nowUtc >= s.NextCheckUtc;
        }

        public static void RecordSuccess(UpdateState s, DateTime nowUtc)
        {
            s.LastAttemptUtc = nowUtc;
            s.LastSuccessUtc = nowUtc;
            s.Failures = 0;
            s.FirstFailureUtc = DateTime.MinValue;
            s.LastFailure = UpdateFailure.None;
            s.RetryAtNextStart = false;
            s.NextCheckUtc = nowUtc + Interval;
        }

        public static UpdateNotice RecordFailure(UpdateState s, DateTime nowUtc, UpdateFailure kind, DateTime retryAfterUtc)
        {
            var notice = new UpdateNotice();
            s.LastAttemptUtc = nowUtc;
            UpdateFailure previous = s.LastFailure;
            s.LastFailure = kind;

            if (kind == UpdateFailure.RateLimited)
            {
                // Temporary and not a connection problem: no message, not counted as a failure.
                s.NextCheckUtc = retryAfterUtc > nowUtc ? retryAfterUtc : nowUtc + RateLimitFallback;
                return notice;
            }

            s.Failures++;
            if (s.Failures == 1)
            {
                s.FirstFailureUtc = nowUtc;
                s.RetryAtNextStart = true;
                s.NextCheckUtc = nowUtc + RetryInterval;
            }
            else
            {
                s.RetryAtNextStart = false;
                DateTime weekAfterFirst = s.FirstFailureUtc + RetryInterval;
                s.NextCheckUtc = weekAfterFirst > nowUtc ? weekAfterFirst : nowUtc + RetryInterval;
            }

            // Time without a successful check, counted from the last success or from when the check was
            // turned on, whichever is later (turning it on after a long break does not warn at once).
            DateTime since = s.LastSuccessUtc > s.EnabledUtc ? s.LastSuccessUtc : s.EnabledUtc;
            if (since == DateTime.MinValue)
            {
                since = s.FirstFailureUtc;
            }
            string reason = Reason(kind);
            if (nowUtc - since >= WarnAfter)
            {
                notice.ShowNotification = true;
                notice.Line = "Updates not checked for " + (int)(nowUtc - since).TotalDays + " days: " + reason
                    + ". Check your connection or firewall, or use Check for AdbAux updates on the About tab.";
            }
            else if (s.Failures == 1)
            {
                notice.Line = "Update check failed: " + reason + ". AdbAux tries again when it starts next.";
            }

            if (kind == UpdateFailure.SecureConnection && previous != UpdateFailure.SecureConnection)
            {
                notice.ShowNotification = true;
                if (notice.Line == null)
                {
                    notice.Line = "Update check failed: " + reason
                        + ". A proxy, antivirus program or network device may be intercepting secure connections.";
                }
            }
            return notice;
        }

        /// <summary>Short text for a failure type, used in the messages.</summary>
        public static string Reason(UpdateFailure kind)
        {
            switch (kind)
            {
                case UpdateFailure.NoConnection: return "no internet connection";
                case UpdateFailure.NotResponding: return "GitHub did not respond";
                case UpdateFailure.RateLimited: return "GitHub request limit reached";
                case UpdateFailure.SecureConnection: return "secure connection to GitHub failed";
                case UpdateFailure.UnexpectedReply: return "unexpected reply from GitHub";
                default: return "unknown error";
            }
        }

        /// <summary>Text for the About tab: the result of the last automatic check and the next one.</summary>
        public static string Describe(UpdateState s, UpdateCheckChoice choice)
        {
            if (choice != UpdateCheckChoice.Yes)
            {
                return "Automatic update check: off.";
            }
            if (s.LastAttemptUtc == DateTime.MinValue)
            {
                return "Automatic update check: on, not run yet.";
            }
            string text = "Last automatic update check: " + Local(s.LastAttemptUtc);
            if (s.LastFailure == UpdateFailure.None)
            {
                return text + ", completed. Next check: " + Local(s.NextCheckUtc) + ".";
            }
            text += ", failed: " + Reason(s.LastFailure) + ".";
            if (s.LastSuccessUtc != DateTime.MinValue)
            {
                text += " Last successful check: " + Local(s.LastSuccessUtc) + ".";
            }
            return text + " Next try: " + (s.RetryAtNextStart ? "when AdbAux starts next" : Local(s.NextCheckUtc)) + ".";
        }

        private static string Local(DateTime utc)
        {
            return utc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        }
    }

    /// <summary>An update check that failed, with the type of failure.</summary>
    public sealed class UpdateCheckException : Exception
    {
        public UpdateCheckException(UpdateFailure kind, string message, Exception inner)
            : base(message, inner)
        {
            Kind = kind;
        }

        public UpdateFailure Kind { get; private set; }

        /// <summary>For RateLimited: when GitHub accepts requests again (MinValue: not given).</summary>
        public DateTime RetryAfterUtc { get; set; }
    }

    /// <summary>A published AdbAux release.</summary>
    public sealed class AppRelease
    {
        public string Tag;          // e.g. v0.6.0
        public Version Version;     // null if the tag has no version number
        public string PageUrl;      // the release page on GitHub
    }

    /// <summary>
    /// Reads release information from GitHub's public API (no sign-in). GitHub receives the PC's IP
    /// address and the User-Agent "AdbAux/&lt;version&gt;"; nothing else is sent.
    /// </summary>
    public static class GitHub
    {
        public const string AdbAuxRepository = "DukeShank/AdbAux";
        public const string ScrcpyRepository = "Genymobile/scrcpy";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        public static string AdbAuxProjectUrl
        {
            get { return "https://github.com/" + AdbAuxRepository; }
        }

        public static string AdbAuxReleasesUrl
        {
            get { return AdbAuxProjectUrl + "/releases"; }
        }

        public static string AdbAuxIssuesUrl
        {
            get { return AdbAuxProjectUrl + "/issues"; }
        }

        public static HttpClient NewClient()
        {
            // .NET Framework may default to old TLS versions that GitHub refuses.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AdbAux/" + AppInfo.Version);   // GitHub's API requires a User-Agent
            return http;
        }

        /// <summary>
        /// The latest published release of a repository (drafts and pre-releases are excluded by GitHub),
        /// or null if the repository has no published release or cannot be seen (HTTP 404).
        /// Throws UpdateCheckException with the type of failure.
        /// </summary>
        public static async Task<Dictionary<string, object>> GetLatestReleaseAsync(string repository)
        {
            string url = "https://api.github.com/repos/" + repository + "/releases/latest";
            using (HttpClient http = NewClient())
            using (var timeout = new CancellationTokenSource(RequestTimeout))
            {
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                HttpResponseMessage response;
                try
                {
                    response = await http.GetAsync(url, timeout.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException ex)
                {
                    throw new UpdateCheckException(UpdateFailure.NotResponding, "GitHub did not respond within 30 seconds.", ex);
                }
                catch (HttpRequestException ex)
                {
                    throw Classify(ex);
                }

                using (response)
                {
                    int code = (int)response.StatusCode;
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                    if (code == 429 || (response.StatusCode == HttpStatusCode.Forbidden && RateLimitReached(response)))
                    {
                        DateTime retry = RetryTime(response);
                        string when = retry == DateTime.MinValue ? "later" : "at " + retry.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
                        throw new UpdateCheckException(UpdateFailure.RateLimited,
                            "GitHub request limit reached; AdbAux tries again " + when + ".", null) { RetryAfterUtc = retry };
                    }
                    if (code >= 500)
                    {
                        throw new UpdateCheckException(UpdateFailure.NotResponding,
                            "GitHub reported a server error (HTTP " + code + ").", null);
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new UpdateCheckException(UpdateFailure.UnexpectedReply,
                            "GitHub answered with HTTP " + code + ".", null);
                    }

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Dictionary<string, object> data;
                    try
                    {
                        data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                    }
                    catch (Exception ex)
                    {
                        throw new UpdateCheckException(UpdateFailure.UnexpectedReply, "The reply from GitHub could not be read.", ex);
                    }
                    if (data == null || !data.ContainsKey("tag_name"))
                    {
                        throw new UpdateCheckException(UpdateFailure.UnexpectedReply, "The reply from GitHub contains no release.", null);
                    }
                    return data;
                }
            }
        }

        /// <summary>The latest published AdbAux release, or null if none is published yet.</summary>
        public static async Task<AppRelease> GetLatestAdbAuxAsync()
        {
            Dictionary<string, object> data = await GetLatestReleaseAsync(AdbAuxRepository).ConfigureAwait(false);
            if (data == null)
            {
                return null;
            }
            string tag = Convert.ToString(data["tag_name"], CultureInfo.InvariantCulture);
            return new AppRelease
            {
                Tag = tag,
                Version = ScrcpyInstaller.ParseVersion(tag),
                // Built from the tag rather than taken from the reply, so only a page of this
                // repository on github.com is ever opened.
                PageUrl = AdbAuxReleasesUrl + "/tag/" + Uri.EscapeDataString(tag)
            };
        }

        private static UpdateCheckException Classify(HttpRequestException ex)
        {
            var web = ex.InnerException as WebException;
            if (web != null)
            {
                switch (web.Status)
                {
                    case WebExceptionStatus.NameResolutionFailure:
                        return new UpdateCheckException(UpdateFailure.NoConnection,
                            "No internet connection (the name api.github.com could not be resolved).", ex);
                    case WebExceptionStatus.TrustFailure:
                        return new UpdateCheckException(UpdateFailure.SecureConnection,
                            "Secure connection to GitHub failed: its certificate was not accepted. A proxy, antivirus "
                            + "program or network device may be intercepting secure connections.", ex);
                    case WebExceptionStatus.SecureChannelFailure:
                        return new UpdateCheckException(UpdateFailure.SecureConnection,
                            "Secure connection to GitHub failed (TLS).", ex);
                }
                if (web.InnerException is AuthenticationException)
                {
                    return new UpdateCheckException(UpdateFailure.SecureConnection,
                        "Secure connection to GitHub failed: " + web.InnerException.Message, ex);
                }
            }
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return new UpdateCheckException(UpdateFailure.NoConnection, "No internet connection.", ex);
            }
            string detail = web != null ? web.Message : ex.Message;
            return new UpdateCheckException(UpdateFailure.NotResponding, "GitHub could not be reached: " + detail, ex);
        }

        private static bool RateLimitReached(HttpResponseMessage response)
        {
            IEnumerable<string> values;
            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out values))
            {
                foreach (string v in values)
                {
                    if (v.Trim() == "0")
                    {
                        return true;
                    }
                }
            }
            return response.Headers.RetryAfter != null;
        }

        /// <summary>When GitHub accepts requests again, from its reply headers (MinValue: not given).</summary>
        private static DateTime RetryTime(HttpResponseMessage response)
        {
            if (response.Headers.RetryAfter != null)
            {
                if (response.Headers.RetryAfter.Delta.HasValue)
                {
                    return DateTime.UtcNow + response.Headers.RetryAfter.Delta.Value;
                }
                if (response.Headers.RetryAfter.Date.HasValue)
                {
                    return response.Headers.RetryAfter.Date.Value.UtcDateTime;
                }
            }
            IEnumerable<string> values;
            if (response.Headers.TryGetValues("X-RateLimit-Reset", out values))
            {
                foreach (string v in values)
                {
                    long seconds;
                    if (long.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
                    {
                        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
                    }
                }
            }
            return DateTime.MinValue;
        }
    }

    /// <summary>Whether Windows reports an internet connection.</summary>
    internal static class NetworkStatus
    {
        // Windows' Network List Manager: the same connectivity test that the network icon in the taskbar shows.
        private static readonly Guid NetworkListManagerClsid = new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B");

        public static bool HasInternet()
        {
            object manager = null;
            try
            {
                Type type = Type.GetTypeFromCLSID(NetworkListManagerClsid);
                manager = Activator.CreateInstance(type);
                object result = type.InvokeMember("IsConnectedToInternet", BindingFlags.GetProperty, null, manager, null);
                return Convert.ToBoolean(result, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                // Network List Manager not available: fall back to "any network adapter is connected".
                return NetworkInterface.GetIsNetworkAvailable();
            }
            finally
            {
                if (manager != null && Marshal.IsComObject(manager))
                {
                    Marshal.ReleaseComObject(manager);
                }
            }
        }
    }
}
