// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Game;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Play;
using Velopack;
using Velopack.Sources;
using OsuUpdateManager = osu.Game.Updater.UpdateManager;

namespace osu.Desktop.Updater
{
    public partial class VelopackUpdateManager : OsuUpdateManager
    {
        private UpdateManager updateManager;
        private INotificationOverlay notificationOverlay = null!;

        private static readonly Logger logger = Logger.GetLogger("updater");

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private ILocalUserPlayInfo? localUserInfo { get; set; }

        public VelopackUpdateManager()
        {
            const string? github_token = null; // TODO: populate.
            var log = new VelopackLogger();
            var source = new GithubSource("https://github.com/ppy/osu", github_token!, false, new HttpClientFileDownloader());
            // you can uncomment the below and pass to UpdateManager to test updates when running locally / debugging.
            // var locator = new TestVelopackLocator("osulazer", "1.0.0", @"C:\Source\osu-deploy\bin\Debug\net6.0\packages", logger);
            updateManager = new UpdateManager(source, logger: log);
        }

        [BackgroundDependencyLoader]
        private void load(INotificationOverlay notifications)
        {
            notificationOverlay = notifications;
        }

        protected override async Task<bool> PerformUpdateCheck() => await checkForUpdateAsync().ConfigureAwait(false);

        private async Task<bool> checkForUpdateAsync(UpdateProgressNotification? notification = null)
        {
            // should we schedule a retry on completion of this check?
            bool scheduleRecheck = true;

            try
            {
                // Avoid any kind of update checking while gameplay is running.
                if (localUserInfo?.IsPlaying.Value == true)
                    return false;

                var info = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

                if (info == null)
                {
                    if (updateManager.IsUpdatePendingRestart)
                    {
                        // the user may have dismissed the completion notice, so show it again.
                        notificationOverlay.Post(new UpdateApplicationCompleteNotification
                        {
                            Activated = () =>
                            {
                                restartToApplyUpdate();
                                return true;
                            },
                        });
                        return true;
                    }

                    // no updates available. bail and retry later.
                    return false;
                }

                scheduleRecheck = false;

                if (notification == null)
                {
                    notification = new UpdateProgressNotification
                    {
                        CompletionClickAction = restartToApplyUpdate,
                    };

                    Schedule(() => notificationOverlay.Post(notification));
                }

                notification.StartDownload();

                try
                {
                    await updateManager.DownloadUpdatesAsync(info, p => notification.Progress = p / 100f).ConfigureAwait(false);
                    notification.State = ProgressNotificationState.Completed;
                }
                catch (Exception e)
                {

                    // In the case of an error, a separate notification will be displayed.
                    notification.FailDownload();
                    Logger.Error(e, @"update download failed!");
                }
            }
            catch (Exception e)
            {
                // we'll ignore this and retry later. can be triggered by no internet connection or thread abortion.
                scheduleRecheck = true;
                Logger.Error(e, @"update check failed!");
            }
            finally
            {
                if (scheduleRecheck)
                {
                    // check again in 30 minutes.
                    Scheduler.AddDelayed(() => Task.Run(async () => await checkForUpdateAsync().ConfigureAwait(false)), 60000 * 30);
                }
            }

            return true;
        }

        private bool restartToApplyUpdate()
        {
            updateManager.WaitExitThenApplyUpdate(true);
            Schedule(() => game.AttemptExit());
            return true;
        }

        private class VelopackLogger : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= Microsoft.Extensions.Logging.LogLevel.Information)
                {
                    string message = formatter(state, exception);
                    logger.Add(message);
                }
            }
        }
    }
}
