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
using NCalc;


namespace CommandDelay
{
    [ApiVersion(1, 14)]
    public class CommandDelay : TerrariaPlugin
    {
        public override Version Version
        {
            get { return new Version(1, 1, 0); }
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
            Order = 334;
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
            }
            base.Dispose(disposing);
        }
        
        public string SyntaxErrorPrefix = "Invalid syntax! Proper usage: ";
        public bool ncalcenabled = true;

        public void Setup()
        {
            if (!File.Exists(Path.Combine("ServerPlugins", "NCalc.dll")))
            {
                ncalcenabled = false;
            }
        }

        public void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("commanddelay", DelayCMD, "delay"));
            Commands.ChatCommands.Add(new Command("commandloop", LoopCMD, "loop"));
            Commands.ChatCommands.Add(new Command("calculate", calcCMD, "calc"));
            Setup();
        }

        public void DelayCMD(CommandArgs args)
        {
            TSPlayer player = args.Player;

            if (args.Parameters.Count > 1)
            {
                int interval;
                if (!int.TryParse(args.Parameters[0], out interval))
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
        public void LoopCMD(CommandArgs args)
        {
            TSPlayer player = args.Player;

            if (args.Parameters.Count > 1)
            {
                int amount;
                if (!int.TryParse(args.Parameters[0], out amount))
                {
                    player.SendErrorMessage("Input amount was not a number.");
                    return;
                }
                else
                {
                    var parameters = args.Parameters;
                    parameters.RemoveAt(0);
                    string command = String.Join(" ", args.Parameters);
                    if (!command.StartsWith("/"))
                    {
                        command = "/" + command;
                    }
                    for (int i = 0; i <= amount; i++)
                    {
                        Group group = player.Group;
                        player.Group = new SuperAdminGroup();
                        Commands.HandleCommand(player, command);
                        if (!command.StartsWith("/user group " + player.UserAccountName))
                        {
                            player.Group = group;
                        }
                    }
                }
            }
            else
            {
                player.SendErrorMessage(SyntaxErrorPrefix + "/loop <amount> <command>");
                return;
            }
        }
        public void calcCMD(CommandArgs args)
        {
            TSPlayer player = args.Player;
            if (!ncalcenabled)
            {
                player.SendErrorMessage("This feature does not work when the NCalc addon is not installed.");
                return;
            }
            else if (args.Parameters.Count > 0)
            {
                try
                {
                    Expression e = new Expression(string.Join(" ", args.Parameters));
                    object result = e.Evaluate();
                    player.SendSuccessMessage("Ans=" + result);
                }
                catch (Exception e)
                {
                    player.SendErrorMessage("Calculator Error: Please enter a correct math equation.");
                    return;
                }
            }
            else
            {
                player.SendErrorMessage(SyntaxErrorPrefix + "/calc <equation>");
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
                Group group = player.Group;
                player.Group = new SuperAdminGroup();
                Commands.HandleCommand(player, command);
                if (!command.StartsWith("/user group " + player.UserAccountName))
                {
                    player.Group = group;
                }
            }
            catch(Exception e)
            {
                //Player probably doesn't exist anymore
            }
        }
    }
}