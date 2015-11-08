﻿namespace HashBus.Viewer.MentionLeaderboard
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ColoredConsole;
    using Humanizer;

    class MentionLeaderboardView
    {
        private static readonly Dictionary<int, string> movementTokens =
            new Dictionary<int, string>
            {
                { int.MinValue, ">" },
                { -1, "^" },
                { 0, "=" },
                { 1, "v" },
            };

        private static readonly Dictionary<int, ConsoleColor> movementColors =
            new Dictionary<int, ConsoleColor>
            {
                { int.MinValue, ConsoleColor.Yellow },
                { -1, ConsoleColor.Green},
                { 0, ConsoleColor.Gray },
                { 1, ConsoleColor.Red },
            };

        private static readonly Dictionary<int, ConsoleColor> movementBackgroundColors =
            new Dictionary<int, ConsoleColor>
            {
                { int.MinValue, ConsoleColor.DarkYellow },
                { -1, ConsoleColor.DarkGreen },
                { 0, ConsoleColor.Black },
                { 1, ConsoleColor.DarkRed },
            };

        public static async Task StartAsync(
            string track,
            int refreshInterval,
            IService<string, WebApi.MentionLeaderboard> leaderboards,
            bool showPercentages,
            int verticalPadding,
            int horizontalPadding)
        {
            Console.CursorVisible = false;
            var previousLeaderboard = new WebApi.MentionLeaderboard();
            while (true)
            {
                WebApi.MentionLeaderboard currentLeaderboard;
                try
                {
                    currentLeaderboard = await leaderboards.GetAsync(track);
                }
                catch (Exception ex)
                {
                    ColorConsole.WriteLine("Failed to get leaderboard. ".Red(), ex.Message.DarkRed());
                    Thread.Sleep(1000);
                    continue;
                }

                var position = 0;
                var lines = new List<IEnumerable<ColorToken>>();
                foreach (var currentEntry in currentLeaderboard?.Entries ??
                    Enumerable.Empty<WebApi.MentionLeaderboard.Entry>())
                {
                    ++position;
                    var previousEntry = (previousLeaderboard?.Entries ??
                            Enumerable.Empty<WebApi.MentionLeaderboard.Entry>())
                        .Select((entry, index) => new
                        {
                            Entry = entry,
                            Position = index + 1
                        })
                        .FirstOrDefault(e => e.Entry.UserMentionId == currentEntry.UserMentionId);

                    var movement = previousEntry == null
                        ? int.MinValue
                        : Math.Sign(position - previousEntry.Position);

                    var countMovement = Math.Sign(Math.Min(previousEntry?.Entry.Count - currentEntry.Count ?? 0, movement));

                    var tokens = new List<ColorToken>
                    {
                        $"{movementTokens[movement]} {position.ToString().PadLeft(2)}".Color(movementColors[movement]),
                        $" {currentEntry.UserMentionName}".White(),
                        $" @{currentEntry.UserMentionScreenName}".Cyan(),
                        $" {currentEntry.Count:N0}".Color(movementColors[countMovement]),
                    };

                    if (showPercentages)
                    {
                        tokens.Add($" ({currentEntry.Count / (double)currentLeaderboard.MentionsCount:P0})".DarkGray());
                    }

                    var maxWidth = Console.WindowWidth - (horizontalPadding * 2);
                    tokens.Add(new string(' ', Math.Max(0, maxWidth - tokens.Sum(token => token.Text.Length))));

                    lines.Add(tokens.Trim(maxWidth).Select(token => token.On(movementBackgroundColors[movement])));
                }

                Console.Clear();
                for (var newLine = verticalPadding - 1; newLine >= 0; newLine--)
                {
                    ColorConsole.WriteLine();
                }

                var padding = new string(' ', horizontalPadding);
                ColorConsole.WriteLine(
                    padding,
                    $" {track} ".DarkCyan().On(ConsoleColor.White),
                    " Most Mentioned".White());

                ColorConsole.WriteLine(
                    padding,
                    "Powered by ".DarkGray(),
                    " NServiceBus ".White().OnDarkBlue(),
                    " from ".DarkGray(),
                    "Particular Software".White());

                ColorConsole.WriteLine();
                foreach (var line in lines)
                {
                    ColorConsole.WriteLine(new ColorToken[] { padding }.Concat(line).ToArray());
                }

                ColorConsole.WriteLine(
                    padding,
                    $"Total mentions:".Gray(),
                    " ",
                    $"{currentLeaderboard?.MentionsCount ?? 0:N0}"
                        .Color(currentLeaderboard?.MentionsCount - previousLeaderboard?.MentionsCount > 0 ? movementColors[-1] : movementColors[0]),
                    $" · {DateTime.UtcNow.ToLocalTime()}".DarkGray());

                var maxMessageLength = 0;
                var refreshTime = DateTime.UtcNow.AddMilliseconds(refreshInterval);
                using (var timer = new Timer(c =>
                {
                    var timeLeft = new TimeSpan(0, 0, 0, (int)Math.Round((refreshTime - DateTime.UtcNow).TotalSeconds));
                    var message = $"\r{padding}Refreshing in {timeLeft.Humanize()}...";
                    maxMessageLength = Math.Max(maxMessageLength, message.Length);
                    ColorConsole.Write(message.PadRight(maxMessageLength).DarkGray());
                }))
                {
                    timer.Change(0, 1000);
                    Thread.Sleep(refreshInterval);
                }

                previousLeaderboard = currentLeaderboard;
            }
        }
    }
}
