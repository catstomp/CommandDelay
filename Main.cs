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
        public string NoPermissionError = "You do not have permission to use this command.";
        public bool ncalcenabled = true;
        public static List<int> threads = new List<int>();

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
            Commands.ChatCommands.Add(new Command("execute", execCMD, "exec"));
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
                var thread = new Thread(new ThreadStart(newthread.Cmd));
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
                    player.SendErrorMessage("Amount must be a number.");
                    return;
                }
                else if (amount < 1)
                {
                    player.SendErrorMessage("Amount must be positive.");
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
                    for (int i = 0; i < amount; i++)
                    {
                        Group group = player.Group;
                        if (group.HasPermission("commanddelay.anycommand"))
                        {
                            player.Group = new SuperAdminGroup();
                        }
                        Commands.HandleCommand(player, command);
                        if (!command.StartsWith("/user group " + player.UserAccountName) && group.HasPermission("commanddelay.anycommand"))
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
        public void execCMD(CommandArgs args)
        {
            TSPlayer player = args.Player;

            bool clear = true;
            for (int i = 0; i < threads.Count; i++)
            {
                if (threads[i] != -1)
                {
                    clear = false;
                    i = threads.Count;
                }
            }
            if (clear)
            {
                threads.Clear();
            }

            if (args.Parameters.Count == 1)
            {
                if (args.Parameters[0] == "list")
                {
                    if (player.Group.HasPermission("commanddelay.manage"))
                    {
                        var list = "";
                        for (int i = 0; i < threads.Count; i++)
                        {
                            var state = "running";
                            if (threads[i] == 1)
                            {
                                state = "paused";
                            }
                            list += String.Format("({0}): {1}", i, state);
                        }
                        player.SendSuccessMessage("Current loops:");
                        player.SendInfoMessage(list.Length > 0 ? list : "There are no loops running.");
                        return;
                    }
                    else
                    {
                        player.SendErrorMessage(NoPermissionError);
                        return;
                    }
                }
                else
                {
                    player.SendErrorMessage(SyntaxErrorPrefix + "/exec [<looptime>/stop/pause/resume/list] <interval between commands> <command/options>");
                    return;
                }
            }
            else if (args.Parameters.Count == 2)
            {
                if (player.Group.HasPermission("commanddelay.manage"))
                {
                    if (args.Parameters[0] == "stop")
                    {
                        int threadid;
                        if (!int.TryParse(args.Parameters[1], out threadid))
                        {
                            player.SendErrorMessage("Invalid loop ID.");
                            return;
                        }
                        else
                        {
                            threadid = Convert.ToInt32(args.Parameters[1]);
                            if (threadid < 0)
                            {
                                player.SendErrorMessage("Loop ID must be positive or zero.");
                                return;
                            }
                            else if (threads.Count >= threadid)
                            {
                                threads[threadid - 1] = -1;
                                player.SendSuccessMessage(string.Format("Stopped loop {0}.", threadid));
                                return;
                            }
                            else
                            {
                                player.SendErrorMessage("Invalid loop ID.");
                                return;
                            }
                        }
                    }
                    else if (args.Parameters[0] == "resume")
                    {
                        int threadid;
                        if (!int.TryParse(args.Parameters[1], out threadid))
                        {
                            player.SendErrorMessage("Invalid loop ID.");
                            return;
                        }
                        else
                        {
                            threadid = Convert.ToInt32(args.Parameters[1]);
                            if (threadid < 0)
                            {
                                player.SendErrorMessage("Loop ID must be positive or zero.");
                                return;
                            }
                            else if (threads.Count >= threadid)
                            {
                                if (threads[threadid - 1] != 0)
                                {
                                    threads[threadid - 1] = 0;
                                    player.SendSuccessMessage(string.Format("Resumed loop {0}.", threadid));
                                    return;
                                }
                                else
                                {
                                    player.SendErrorMessage(string.Format("Thread {0} is already running.", threadid));
                                    return;
                                }
                            }
                            else
                            {
                                player.SendErrorMessage("Invalid loop ID.");
                                return;
                            }
                        }
                    }
                    else if (args.Parameters[0] == "pause")
                    {
                        int threadid;
                        if (!int.TryParse(args.Parameters[1], out threadid))
                        {
                            player.SendErrorMessage("Invalid loop ID.");
                            return;
                        }
                        else
                        {
                            threadid = Convert.ToInt32(args.Parameters[1]);
                            if (threadid < 0)
                            {
                                player.SendErrorMessage("Loop ID must be positive or zero.");
                                return;
                            }
                            else if (threads.Count >= threadid)
                            {
                                if (threads[threadid - 1] != 1)
                                {
                                    threads[threadid - 1] = 1;
                                    player.SendSuccessMessage(string.Format("Paused loop {0}.", threadid));
                                    return;
                                }
                                else
                                {
                                    player.SendErrorMessage(string.Format("Thread {0} is already paused.", threadid));
                                    return;
                                }
                            }
                            else
                            {
                                player.SendErrorMessage("Invalid loop ID.");
                                return;
                            }
                        }
                    }
                    else
                    {
                        player.SendErrorMessage(SyntaxErrorPrefix + "/exec [<looptime>/stop/pause/resume/list] <interval between commands> <command/options>");
                        return;
                    }
                }
                else
                {
                    player.SendErrorMessage(NoPermissionError);
                    return;
                }
            }
            else if (args.Parameters.Count > 2)
            {
                int amount;
                int max;
                if (!int.TryParse(args.Parameters[0], out amount) && args.Parameters[0] != "inf")
                {
                    player.SendErrorMessage("Amount must be a number.");
                    return;
                }
                else if (amount < 0)
                {
                    player.SendErrorMessage("Amount must be positive.");
                    return;
                }
                else if (!int.TryParse(args.Parameters[1], out max))
                {
                    player.SendErrorMessage("Delay interval must be a number.");
                    return;
                }
                else if (max < 0)
                {
                    player.SendErrorMessage("Delay interval must be positive or zero.");
                    return;
                }
                else
                {
                    var newthread = new ExecThread(args, threads.Count);
                    var thread = new Thread(new ThreadStart(newthread.Loop));
                    thread.Start();
                    threads.Add(0);
                    return;
                }
            }
            else
            {
                player.SendErrorMessage(SyntaxErrorPrefix + "/exec [<looptime>/stop/pause/resume/list] <interval between commands> <command/options>");
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
            if (Netplay.disconnect)
                return;

            int seconds = Convert.ToInt32(args.Parameters[0]);
            var parameters = args.Parameters;
            parameters.RemoveAt(0);
            string command = String.Join(" ", args.Parameters);
            if (!command.StartsWith("/"))
            {
                command = "/" + command;
            }
            while (seconds-- > 0)
            {
                if (Netplay.disconnect)
                    return;
                Thread.Sleep(1000);
            }
            try
            {
                TSPlayer player = args.Player;
                Group group = player.Group;
                if (group.HasPermission("commanddelay.anycommand"))
                {
                    player.Group = new SuperAdminGroup();
                }
                Commands.HandleCommand(player, command);
                if (!command.StartsWith("/user group " + player.UserAccountName) && group.HasPermission("commanddelay.anycommand"))
                {
                    player.Group = group;
                }
            }
            catch (Exception e)
            {
                //Player probably doesn't exist anymore
            }
        }
    }
    public class ExecThread
    {
        CommandArgs args;
        int threadid;

        public ExecThread(CommandArgs args, int threadid)
        {
            this.args = args;
            this.threadid = threadid;
        }
        public void Loop()
        {
            var player = args.Player;
            var amount = 1;
            var seconds = 0;
            var infloop = false;
            int interval = Convert.ToInt32(args.Parameters[1]);
            try
            {
                if (args.Parameters[0] == "inf")
                {
                    infloop = true;
                }
                else
                {
                    amount = Convert.ToInt32(args.Parameters[0]);
                }
                if (args.Parameters[0] == "inf" && args.Parameters[1] == "0")
                {
                    player.SendErrorMessage("Do not try to make an infinite and instant loop, bro come on.");
                    return;
                }
                var parameters = args.Parameters;
                parameters.RemoveAt(0);
                parameters.RemoveAt(0);
                var command = String.Join(" ", parameters);
                if (!command.StartsWith("/"))
                {
                    command = "/" + command;
                }
                command = command.Replace("%group", player.Group.Name);
                command = command.Replace("%name", player.Name);
                command = command.Replace("%account", player.UserAccountName);
                command = command.Replace("%prefix", player.Group.Prefix);
                int i = 0;
                while (i < amount)
                {
                    if (!infloop)
                    {
                        i += 1;
                    }
                    command = command.Replace("%i", "" + (i));
                    command = command.Replace("" + (i - 1), "" + (i));
                    command = command.Replace("%-i", "" + (amount - i));
                    command = command.Replace("" + (amount - i + 1), "" + (amount - i));
                    seconds = interval;
                    while (seconds-- > 0)
                    {
                        if (Netplay.disconnect || CommandDelay.threads[threadid] == -1)
                            return;
                        System.Threading.Thread.Sleep(1000);
                        if (CommandDelay.threads[threadid] == -1)
                            return;
                        while (CommandDelay.threads[threadid] == 1)
                        {
                            //Waits until the loop is resumed, is this okay to do?
                            if (Netplay.disconnect || CommandDelay.threads[threadid] == -1)
                                return;
                        }
                    }
                    Group group = player.Group;
                    if (group.HasPermission("commanddelay.anycommand"))
                    {
                        player.Group = new SuperAdminGroup();
                    }
                    Commands.HandleCommand(player, command);
                    if (!command.StartsWith("/user group " + player.UserAccountName) && group.HasPermission("commanddelay.anycommand"))
                    {
                        player.Group = group;
                    }
                }
            }
            catch (Exception e){}
        }
    }
}