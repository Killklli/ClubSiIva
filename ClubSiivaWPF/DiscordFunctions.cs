using ClubSiivaWPF.Databases;
using Discord;
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
        // Discord Client for actual connection
        private static DiscordSocketClient discordclient;
        private static IServiceProvider _services;
        public static List<String> ModRoles = new List<string>();
        private static LiteDatabase db;
        private static LiteDatabase hidb;
        private static LiteDatabase usrdb;

        public static async Task StartDiscord(LiteDatabase queue, LiteDatabase history, LiteDatabase users)
        {
            _services = ConfigureServices();
            discordclient = _services.GetRequiredService<DiscordSocketClient>();
            discordclient.Log += LogAsync;
            discordclient.Ready += Ready;
            discordclient.MessageReceived += MessageReceivedAsync;
            db = queue;
            hidb = history;
            usrdb = users;
            // read file into a string and deserialize JSON to a type
            Config conf = JsonConvert.DeserializeObject<Config>(File.ReadAllText(@"discord.cfg"));
            // Tokens should be considered secret data, and never hard-coded.
            if (conf.DiscordToken == "TokenString")
            {
                MessageBox.Show("Please set your config file discord.cfg to have your bot token" + Environment.NewLine + "---------------------------Instructions---------------------------" + Environment.NewLine + "The required files and links will open after this box closes" + Environment.NewLine + "Add a new application, and under that application add a new bot and get it's token, put that token in the config file" + Environment.NewLine + "And then add the bot to your discord using the client ID from the application" + Environment.NewLine + "https://discordapp.com/oauth2/authorize?&client_id=CLIENTID&scope=bot&permissions=8", "Please configure discord");
                Process process = new Process();
                process.StartInfo.FileName = Directory.GetCurrentDirectory() + "/discord.cfg";
                process.Start();
                System.Diagnostics.Process.Start("https://discordapp.com/developers/applications/");
                System.Environment.Exit(0);
            }
            ModRoles = conf.ModRoles;
            await discordclient.LoginAsync(TokenType.Bot, conf.DiscordToken);
            await discordclient.StartAsync();
            await discordclient.SetGameAsync("ClubSiiva", null, ActivityType.Listening);
            // Block the program until it is closed.
            await Task.Delay(-1);
        }
        private static async Task<Task> Ready()
        {
            Debug.WriteLine(discordclient.CurrentUser + " is connected!");
            return Task.CompletedTask;
        }
        private static async Task MessageReceivedAsync(SocketMessage message)
        {
            // The bot should never respond to itself.
            if (message.Author.Id == discordclient.CurrentUser.Id)
                return;
            try
            {
                if (message.Content != string.Empty)
                {
                    if (message.Content[0] == '!')
                    {
                        _ = Task.Run(() => Commands.CommandsAsync(message, hidb, db, usrdb, ModRoles));
                    }
                }
            }
            catch
            {
                try
                {
                    _ = message.Channel.SendMessageAsync("Something went wrong please try again");
                }
                catch { }
            }
        }
        private static Task LogAsync(LogMessage log)
        {
            Debug.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
        private static IServiceProvider ConfigureServices()
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
