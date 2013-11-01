using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Data;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;


namespace CommandDelay
{
    [ApiVersion(1, 14)]
    public class CommandDelay : TerrariaPlugin
    {
        public override Version Version
        {
            get { return new Version(1, 0, 0); }
        }

        public override string Name
        {
            get { return "CommandDelay"; }
        }

        public override string Author
        {
            get { return "Antagonist"; }
        }

        public override string Description
        {
            get { return "Command features"; }
        }

        public CommandDelay(Main game)
            : base(game)
        {
            Order = 333;
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, (args) => { OnInitialize(); });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, (args) => { OnInitialize(); });
            }
            base.Dispose(disposing);
        }

        public void OnInitialize()
        {
            //Commands.ChatCommands.Add(new Command("permission", Method, "command"));
            Commands.ChatCommands.Add(new Command("commanddelay", DelayCMD, "delay"));
        }
        public string SyntaxErrorPrefix = "Invalid syntax! Proper usage: ";

        public void DelayCMD(CommandArgs args)
        {
            TSPlayer player = args.Player;
            
            if (args.Parameters.Count > 1)
            {
                int interval;
                try
                {
                    interval = Convert.ToInt32(args.Parameters[0]);
                }
                catch(Exception error)
                {
                    player.SendErrorMessage("Input interval was not a number.");
                    return;
                }
                var newthread = new DelayThread(args);
                Thread thread = new Thread(new ThreadStart(newthread.Cmd));
                thread.Start();
            }
            else
            {
                player.SendErrorMessage(SyntaxErrorPrefix + "/delay <interval> <command>");
                return;
            }
        }

    }
    //end of plugin thread

    public class DelayThread
    {
        CommandArgs args;

        public DelayThread(CommandArgs args)
        {
            this.args = args;
        }
        public void Cmd()
        {
            int interval = Convert.ToInt32(args.Parameters[0])*1000;
            var parameters = args.Parameters;
            parameters.RemoveAt(0);
            string command = String.Join(" ", args.Parameters);
            if (!command.StartsWith("/"))
            {
                command = "/" + command;
            }
            System.Threading.Thread.Sleep(interval);
            try
            {
                TSPlayer player = args.Player;
                Commands.HandleCommand(player, command);
            }
            catch(Exception error){}
        }
    }
}