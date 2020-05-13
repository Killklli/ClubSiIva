using ClubSiivaWPF.Databases;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace ClubSiivaWPF
{
    class DiscordFunctions
    {




        public static Task LogAsync(LogMessage log)
        {
            Debug.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
        public static IServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .BuildServiceProvider();
        }
        /// <summary>
        /// Write a message to discord as a block
        /// </summary>
        /// <param name="message">The Socket Message to use</param>
        /// <param name="title">The title of the object</param>
        /// <param name="description">The data in the description</param>
        /// <param name="chooseColor">The color of the file function to call</param>
        public static void EmbedThis(SocketMessage message, string title = null, string description = null, string chooseColor = "")
        {
            var toEmebed = new EmbedBuilder();
            toEmebed.WithTitle(title);
            toEmebed.WithDescription(description);
            chooseColor.ToLower();
            switch (chooseColor)
            {
                case "red":
                    toEmebed.WithColor(Color.DarkRed);
                    break;
                case "blue":
                    toEmebed.WithColor(Color.Blue);
                    break;
                case "gold":
                    toEmebed.WithColor(Color.Gold);
                    break;
                case "magenta":
                    toEmebed.WithColor(Color.Magenta);
                    break;
                case "green":
                    toEmebed.WithColor(Color.Green);
                    break;
                case "orange":
                    toEmebed.WithColor(Color.Orange);
                    break;
                default:
                    toEmebed.WithColor(Color.Orange);
                    break;
            }

            message.Channel.SendMessageAsync("", false, toEmebed.Build());
        }

    }
}
