﻿using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using System;
using JifBot.Models;
using System.Linq;
using Discord.Commands;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using JIfBot;

namespace JifBot
{
    public class EventHandler
    {
        public CommandService commands;
        private DiscordSocketClient bot;
        private IServiceProvider map;
        private ReactionHandler reactionHandler;
        private static string configName = Program.configName;

        public EventHandler(IServiceProvider service)
        {
            map = service;
            bot = map.GetService<DiscordSocketClient>();
            commands = map.GetService<CommandService>();
            reactionHandler = new ReactionHandler();
        }

        public async Task AnnounceUserJoined(SocketGuildUser user)
        {
            Console.WriteLine("User " + user.Username + " Joined " + user.Guild.Name);

            var db = new BotBaseContext();
            var config = db.ServerConfig.AsQueryable().Where(s => s.ServerId == user.Guild.Id).FirstOrDefault();

            if (config != null && config.JoinId != 0)
            {
                IGuild server = user.Guild;
                ITextChannel channel = await server.GetTextChannelAsync(config.JoinId);

                var embed = new EmbedBuilder();
                var color = db.Variable.AsQueryable().Where(V => V.Name == "embedColor").FirstOrDefault();
                embed.WithColor(new Color(Convert.ToUInt32(color.Value, 16)));
                embed.ThumbnailUrl = user.GetAvatarUrl();
                embed.Title = $"**{user.Username} Joined The Server:**";
                embed.Description = ($"**User:** {user.Mention}");
                embed.WithCurrentTimestamp();
                await channel.SendMessageAsync("", false, embed: embed.Build());
            }
        }

        public async Task AnnounceLeftUser(SocketGuildUser user)
        {
            Console.WriteLine("User " + user.Username + " Left " + user.Guild.Name);

            var db = new BotBaseContext();
            var config = db.ServerConfig.AsQueryable().Where(s => s.ServerId == user.Guild.Id).FirstOrDefault();

            if (config != null && config.LeaveId != 0)
            {
                IGuild server = user.Guild;
                ITextChannel channel = await server.GetTextChannelAsync(config.LeaveId);

                var embed = new EmbedBuilder();
                var color = db.Variable.AsQueryable().Where(V => V.Name == "embedColor").FirstOrDefault();
                embed.WithColor(new Color(Convert.ToUInt32(color.Value, 16)));
                embed.ThumbnailUrl = user.GetAvatarUrl();
                embed.Title = $"**{user.Username} Left The Server:**";
                embed.Description = $"**User:**{user.Mention}";
                embed.WithCurrentTimestamp();
                await channel.SendMessageAsync("", false, embed.Build());
            }
        }

        public async Task SendMessageReport(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            SocketGuildChannel socketChannel = (SocketGuildChannel)channel;
            var db = new BotBaseContext();
            var config = db.ServerConfig.AsQueryable().Where(s => s.ServerId == socketChannel.Guild.Id).FirstOrDefault();

            if (config != null && config.MessageId != 0)
            {
                IGuild server = bot.GetGuild(config.ServerId);
                ITextChannel sendChannel = await server.GetTextChannelAsync(config.MessageId);

                var message = await cache.GetOrDownloadAsync();
                var embed = new EmbedBuilder();
                var color = db.Variable.AsQueryable().Where(V => V.Name == "embedColor").FirstOrDefault();
                embed.WithColor(new Color(Convert.ToUInt32(color.Value, 16)));
                embed.Title = "A message has been deleted";
                embed.Description = "\"" + message.Content + "\"";
                embed.WithCurrentTimestamp();
                embed.AddField("in " + channel.Name, "sent by: " + message.Author);
                embed.ThumbnailUrl = message.Author.GetAvatarUrl();
                await sendChannel.SendMessageAsync("", false, embed.Build());
            }
        }

        public static Task WriteLog(LogMessage lmsg)
        {
            var cc = Console.ForegroundColor;
            switch (lmsg.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }
            Console.WriteLine($"{DateTime.Now} [{lmsg.Severity,8}] {lmsg.Source}: {lmsg.Message}");
            Console.ForegroundColor = cc;
            return Task.CompletedTask;
        }

        public async Task HandleMessage(SocketMessage pMsg)
        {
            var message = pMsg as SocketUserMessage;

            //Don't handle if system message
            if (message == null)
                return;

            if (message.Author.IsBot)
                return;

            await handleCommand(message);
            await reactionHandler.ParseReactions(message);
        }

        private async Task handleCommand(SocketUserMessage message)
        {
            var db = new BotBaseContext();
            var context = new SocketCommandContext(bot, message);
            //Mark where the prefix ends and the command begins
            int argPos = 0;
            var config = db.Configuration.AsQueryable().Where(cfg => cfg.Name == configName).First();

            //Determine if the message has a valid prefix, adjust argPos
            if (message.HasStringPrefix(config.Prefix, ref argPos))
            {
                //Execute the command, store the result
                var result = await commands.ExecuteAsync(context, argPos, map);

                //If the command failed, notify the user
                if (!result.IsSuccess && result.ErrorReason != "Unknown command.")

                    await message.Channel.SendMessageAsync($"**Error:** {result.ErrorReason}");
            }
        }
    }
}
