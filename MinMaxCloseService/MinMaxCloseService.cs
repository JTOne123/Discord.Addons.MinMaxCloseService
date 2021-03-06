﻿using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Discord.Addons.MinMaxClose
{
	public class MinMaxCloseService
	{
		private DiscordSocketClient Client { get; }
		private List<MinMaxCloseMessage> messages { get; set; }
		Emoji stop = new Emoji("\ud83c\uddfd");
		Emoji minimize = new Emoji("\u2B07");
		Emoji maximize = new Emoji("\u2195");
		Emoji info = new Emoji("\u2139");

		public MinMaxCloseService(DiscordSocketClient client)
		{
			Client = client;
			messages = new List<MinMaxCloseMessage>();
			client.ReactionAdded += MinMaxCloseReaction;
			client.ReactionRemoved += MinMaxCloseReactionRemoved;
			client.MessageDeleted += MinMaxCloseRemoveMessage;
			client.ReactionsCleared += MinMaxCloseReactionsCleared;
		}

		private async Task MinMaxCloseRemoveMessage(Cacheable<IMessage, ulong> cacheable, ISocketMessageChannel channel)
		{
			var message = await cacheable.GetOrDownloadAsync();
			if (messages.FirstOrDefault(x => x.Message == message).Message == message) messages.Remove(messages.FirstOrDefault(x => x.Message == message));
			else return;
		}

		private async Task MinMaxCloseReaction(Cacheable<IUserMessage, ulong> cacheable, ISocketMessageChannel messageChannel, SocketReaction reaction)
		{
			var message = await cacheable.GetOrDownloadAsync();
			if (reaction.UserId == Client.CurrentUser.Id) return;
			MinMaxCloseMessage minMaxCloseMessage = null;
			if (messages.Any(x => x.Message.Id == message.Id)) minMaxCloseMessage = messages.FirstOrDefault(x => x.Message.Id == message.Id);
			else return;

			switch (reaction.Emote.Name)
			{
				case "\ud83c\uddfd"://delete
					if (minMaxCloseMessage.UserID != reaction.UserId) return;
					await minMaxCloseMessage.Message.DeleteAsync();
					break;
				case "\u2B07"://minimize
					await message.RemoveReactionAsync(minimize, reaction.User.GetValueOrDefault());
					if (!minMaxCloseMessage.Maximized) return;
					minMaxCloseMessage.Maximized = false;
					if (message.Content != "") await message.ModifyAsync(x => x.Content = minMaxCloseMessage.ShortMessage);
					else
					{
						var embed = new EmbedBuilder();
						embed.AddField(DateTime.UtcNow.ToString() + " UTC.", minMaxCloseMessage.ShortMessage);
						await message.ModifyAsync(x => x.Embed = embed.Build());
					}

					break;
				case "\u2195"://maximize
					await message.RemoveReactionAsync(maximize, reaction.User.GetValueOrDefault());
					if (minMaxCloseMessage.Maximized) return;
					minMaxCloseMessage.Maximized = true;
					if (minMaxCloseMessage.Message.Content != "") await message.ModifyAsync(x => x.Content = minMaxCloseMessage.Message.Content);
					else
					{
						await message.ModifyAsync(x => x.Embed = minMaxCloseMessage.Message.Embeds.FirstOrDefault() as Embed);
						await message.ModifyAsync(x => x.Content = "");
					}
					break;
				case "\u2139"://info
					if(minMaxCloseMessage.InfoCalled) return;
					minMaxCloseMessage.InfoCalled = true;
					await message.RemoveReactionAsync(info, reaction.User.GetValueOrDefault());
					var msg = await message.Channel.SendMessageAsync("Use :regional_indicator_x: to delete the message, can only be done by the user that called the command, :arrow_up_down: to maximize the message,:arrow_down: to minimize the message.");
					_ = Task.Run(() => DelayDeleteAsync(message: msg as IMessage));
					break;
			}
		}

		private async Task MinMaxCloseReactionRemoved(Cacheable<IUserMessage, ulong> cacheable, ISocketMessageChannel messageChannel, SocketReaction reaction)
		{
			var message = await cacheable.GetOrDownloadAsync();
			MinMaxCloseMessage minMaxCloseMessage = null;
			if (messages.Any(x => x.Message.Id == message.Id)) minMaxCloseMessage = messages.FirstOrDefault(x => x.Message.Id == message.Id);
			else return;
			if (!(message.Reactions.ContainsKey(info))) await message.AddReactionAsync(info);
			if (!(message.Reactions.ContainsKey(minimize))) await message.AddReactionAsync(minimize);
			if (!(message.Reactions.ContainsKey(maximize))) await message.AddReactionAsync(maximize);
			if (!(message.Reactions.ContainsKey(stop))) await message.AddReactionAsync(stop);

		}

		private async Task MinMaxCloseReactionsCleared(Cacheable<IUserMessage, ulong> cacheable, ISocketMessageChannel messageChannel)
		{
			var message = await cacheable.GetOrDownloadAsync();

			MinMaxCloseMessage minMaxCloseMessage = null;
			if (messages.Any(x => x.Message.Id == message.Id)) minMaxCloseMessage = messages.FirstOrDefault(x => x.Message.Id == message.Id);
			else return;
			if (!(message.Reactions.ContainsKey(stop))) await message.AddReactionAsync(stop);
			if (!(message.Reactions.ContainsKey(minimize))) await message.AddReactionAsync(minimize);
			if (!(message.Reactions.ContainsKey(maximize))) await message.AddReactionAsync(maximize);
			if (!(message.Reactions.ContainsKey(info))) await message.AddReactionAsync(info);
		}

		public async Task SendMMCMessageAsync(SocketCommandContext context, IUserMessage message)
		{
			var msg = message;
			var minMaxCloseMessage = new MinMaxCloseMessage(msg, context);
			messages.Add(minMaxCloseMessage);
			await msg.AddReactionAsync(info);
			await msg.AddReactionAsync(minimize);
			await msg.AddReactionAsync(maximize);
			await msg.AddReactionAsync(stop);

			_ = Task.Run(() => DelayDeleteAsync(context, msg, 300));

		}


		private async Task DelayDeleteAsync(SocketCommandContext context = null, IMessage message = null, int? timeDelay = null)
		{
			if (context != null)
			{
				if (context.Channel is SocketDMChannel) return;
			}
			if (timeDelay == null) timeDelay = 15;
			await Task.Delay(timeDelay.Value * 1000);
			if (message.Channel.GetMessageAsync(message.Id) != null) await message.DeleteAsync();
		}
	}
}
