#nullable enable
using Discord.Interactions;
using System.Text;

public class AdminCommands(
	DbHelper db, 
	ILogger<AdminCommands> logger) : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("version", "Get the current version of the bot")]
	public async Task HandleVersionCommand()
	{
		try
		{
			await DeferAsync(ephemeral: true);
			
			var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
			await ModifyOriginalResponseAsync(x => x.Embed = Embeds.Info(
				$"Bot Version: `{version}`",
				"Version Information"));
		}
		catch (Exception ex)
		{
			await logger.HandleError(ex, Context,
				async embed => await ModifyOriginalResponseAsync(x => x.Embed = embed),
				logMessage: "Error handling version command for guild {GuildId}",
				logArgs: [Context.Guild.Id]);
		}
	}
}
