#nullable enable
using Discord.Interactions;
using System.Text;

namespace CommunitySpotlightBot.Commands;

public class ReportCommands(ILogger<ReportCommands> logger) : InteractionModuleBase<SocketInteractionContext>
{
	private const int MaxHours = 504; // 3 weeks = 21 days * 24 hours
	private const int DefaultHours = 24;

	[SlashCommand("report", "Get the top forum threads from a specified time period")]
	public async Task HandleReportCommand(
		[Summary("hours", "Number of hours to look back (default: 24, max: 504)")]
		[MinValue(1)]
		[MaxValue(504)]
		int? hours = null)
	{
		try
		{
			await DeferAsync(ephemeral: true);

			// Validate and set hours
			var lookbackHours = hours ?? DefaultHours;
			if (lookbackHours > MaxHours)
			{
				await ModifyOriginalResponseAsync(x => x.Embed = Embeds.Error(
					$"Cannot look back more than {MaxHours} hours (3 weeks). Using maximum value instead.",
					"Invalid Time Period"));
				lookbackHours = MaxHours;
			}

			var guild = Context.Guild;
			var forumChannels = guild.Channels
				.Where(c => c is SocketForumChannel)
				.Cast<SocketForumChannel>()
				.ToList();

			if (!forumChannels.Any())
			{
				await ModifyOriginalResponseAsync(x => x.Embed = Embeds.Info(
					"No forum channels found in this server.",
					"Forum Report"));
				return;
			}

			var cutoffTime = DateTimeOffset.UtcNow.AddHours(-lookbackHours);
			var activeThreads = new List<(IThreadChannel Thread, ulong ForumId, int ReplyCount)>();

			foreach (var forum in forumChannels)
			{
				var threads = await forum.GetActiveThreadsAsync();
				foreach (var thread in threads)
				{
					// Only include threads created within the specified time period
					if (thread.CreatedAt >= cutoffTime)
					{
						var messages = await thread.GetMessagesAsync(1).FlattenAsync();
						var replyCount = thread.MessageCount - 1; // Subtract 1 to exclude the initial post
						activeThreads.Add((thread, forum.Id, replyCount));
					}
				}
			}

			var top5Threads = activeThreads
				.OrderByDescending(t => t.ReplyCount)
				.Take(5)
				.ToList();

			if (!top5Threads.Any())
			{
				await ModifyOriginalResponseAsync(x => x.Embed = Embeds.Info(
					$"No forum threads were created in the last {lookbackHours} hours.",
					"Forum Report"));
				return;
			}

			var eb = new EmbedBuilder()
				.WithTitle($"Top Forum Threads (Last {lookbackHours} Hours)")
				.WithColor(Color.Blue)
				.WithTimestamp(DateTimeOffset.UtcNow);

			var description = new StringBuilder();
			for (int i = 0; i < top5Threads.Count; i++)
			{
				var (thread, forumId, replyCount) = top5Threads[i];
				var jumpUrl = $"https://discord.com/channels/{Context.Guild.Id}/{thread.Id}";
				description.AppendLine($"{i + 1}. [{thread.Name}]({jumpUrl})");
				description.AppendLine($"   • {replyCount} replies");
				description.AppendLine($"   • In {MentionUtils.MentionChannel(forumId)}");
				description.AppendLine($"   • Created {TimestampTag.FromDateTimeOffset(thread.CreatedAt, TimestampTagStyles.Relative)}");
				description.AppendLine();
			}

			eb.WithDescription(description.ToString());

			await ModifyOriginalResponseAsync(x => x.Embed = eb.Build());
		}
		catch (Exception ex)
		{
			await logger.HandleError(ex, Context,
				async embed => await ModifyOriginalResponseAsync(x => x.Embed = embed),
				logMessage: "Error handling report command for guild {GuildId}",
				logArgs: [Context.Guild.Id]);
		}
	}
}