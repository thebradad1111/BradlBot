﻿using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BradlBot.Commands;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Interactivity;
using Newtonsoft.Json;


namespace BradlBot
{
    class Program
    {
        public static DateTime TimeStarted = DateTime.Now;
        
        public DiscordClient Client { get; set; }
        public CommandsNextModule Commands { get; set; }
        static InteractivityModule Interactivity { get; set; }
        
        public static void Main(string[] args)
        {
            Console.WriteLine("BradlBot Starting");
            
            //Setup config 
            if (!File.Exists("config.json"))
            {
                Console.WriteLine("What do you want your command prefix to be?");
                string prefix = Console.ReadLine();
                Console.WriteLine("What do you want your token to be?");
                string token = Console.ReadLine();
                ConfigJson cfgj = new ConfigJson()
                {
                    CommandPrefix = prefix,
                    Token = token
                };

                string cfgstring = JsonConvert.SerializeObject(cfgj);
                using (var fs = File.Create("config.json"))
                    using (var sw = new StreamWriter(fs, new UTF8Encoding()))
                        sw.Write(cfgstring);
            }
            
            var prog = new Program();
            prog.CheckConfigAsync().GetAwaiter().GetResult();
        }

        
        public async Task CheckConfigAsync()
        {
            //Check config exists
            var json = "";
            try
            {
                //Actually start
                using (var fs = File.OpenRead("config.json"))
                using (var sr = new StreamReader(fs, new UTF8Encoding()))
                    json = await sr.ReadToEndAsync();

                //Next load values from file
                var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
                var cfg = new DiscordConfig()
                {
                    Token = cfgjson.Token,
                    TokenType = TokenType.Bot,

                    AutoReconnect = true,
                    LogLevel = LogLevel.Debug,
                    UseInternalLogHandler = true,
                };
                
                await SetupBotAsync(cfg, cfgjson);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error with config or runtime error: {e.GetType()} - {e.Message}");
#if DEBUG
                throw;
#endif
            }
            finally
            {
                Console.WriteLine("Press any key to quit...");
                Console.ReadKey();
            }

        }
        
        
        public async Task SetupBotAsync(DiscordConfig cfg, ConfigJson cfgjson)
        {

            //Create instance of client
            this.Client = new DiscordClient(cfg);
            
            //Create events
            this.Client.Ready += this.Client_Ready;
            this.Client.GuildAvailable += this.Client_GuildAvailable;
            this.Client.ClientError += this.Client_ClientError;
            
            //Comands config
            var ccfg = new DSharpPlus.CommandsNext.CommandsNextConfiguration()
            {
                StringPrefix = cfgjson.CommandPrefix,
                EnableDms = true,
                EnableMentionPrefix = true
            };

            this.Commands = this.Client.UseCommandsNext(ccfg);
                
            //Commands Events
            this.Commands.CommandExecuted += this.Command_CommandExecuted;
            this.Commands.CommandErrored += this.Command_CommandError;
                
            //Commands registration
            this.Commands.RegisterCommands<UserCommands>();
            this.Commands.RegisterCommands<ModCommands>();
            this.Commands.RegisterCommands<OwnerCommands>();
            
            //Interactivity Setup
            Interactivity = Client.UseInteractivity();
            
            //Connect and login
            await this.Client.ConnectAsync();
            
            //Save time started
            TimeStarted = DateTime.UtcNow;
            
            //Prevent premature quitting
            await Task.Delay(-1);
        }

        private Task Client_Ready(ReadyEventArgs e)
        {
            //lets log that it's occured
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "BradlBot","Client is ready to process events", DateTime.Now);
            //return a completed task
            return Task.CompletedTask;
        }

        private Task Client_GuildAvailable(GuildCreateEventArgs e)
        {
            //Lets log name of guild that was sent
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "BradlBot",$"Guild available: {e.Guild.Name}",DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_ClientError(ClientErrorEventArgs e)
        {
            //Log guild that was sent
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "BradlBot",$"Exception: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Command_CommandExecuted(CommandExecutedEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info, "BradlBot",
                $"{e.Context.User.Username} successfully executed '{e.Command.QualifiedName}' on {e.Context.Guild.Name} - {e.Context.Channel.Name}",
                DateTime.Now);
            return Task.CompletedTask;
        }

        private async Task Command_CommandError(CommandErrorEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Error, "BradlBot",$"{e.Context.User.Username} tried to run '{e.Command?.QualifiedName ?? "<invalid_cmd>"}' on {e.Context.Guild.Name} - {e.Context.Channel.Name} but it gave the error: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);   
                    
            //Check to see if lack of permisions
            if (e.Exception is ChecksFailedException)
            {
                var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");
                CommandsCommon.Respond(e.Context, "Access Denied", $"{emoji} You do not have permission to execute this.", 0xFF0000);
            }
            else if(e.Exception is ArgumentException)
            {
                //Arguments are wrong so tell them so
                var emoji = DiscordEmoji.FromName(e.Context.Client, ":face_palm:");
                CommandsCommon.Respond(e.Context,"Error",$"{emoji}Incorrect arguments; see '!help {e.Command?.Name ?? "<Command Name>"}'", 0xFF0000);
            }
            else
            {
                CommandsCommon.RespondWithError(e.Context, $"{e.Exception.GetType()} - {e.Exception.Message}");
            }
        }
    }
    
    //Holds config.json
    public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; set; }
        
        [JsonProperty("prefix")]
        public string CommandPrefix { get; set; }
    }
}