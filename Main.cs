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
            get { return new Version(1, 2, 4); }
        }

        public override string Name{ get { return "CommandDelay"; } }

        public override string Author{ get { return "Antagonist"; } }

        public override string Description{ get { return "Command features"; } }

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
        public static List<TSPlayer> loopplayers = new List<TSPlayer>();
        public static List<string> loopstatus = new List<string>();
        public static List<string> loopcommands = new List<string>();
        public static List<string> looptimes = new List<string>();

        public void Setup()
        {
            if (!File.Exists(Path.Combine("ServerPlugins", "NCalc.dll")))
            {
                ncalcenabled = false;
            }
        }

        public void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("commanddelay.delay.start", DelayCMD, "delay"));
            Commands.ChatCommands.Add(new Command("commanddelay.loop.start", LoopCMD, "loop"));
            Commands.ChatCommands.Add(new Command("commanddelay.calculate", calcCMD, "calc"));
            Commands.ChatCommands.Add(new Command("commanddelay.execute.start", execCMD, "exec"));
            Setup();
        }

        public void DelayCMD(CommandArgs args)
        {
            TSPlayer player = args.Player;
            if (args.Parameters.Count > 1)
            {
                int interval;
                args.Parameters[0] = args.Parameters[0].Replace("k", "000");
                args.Parameters[0] = args.Parameters[0].Replace("m", "000000");
                args.Parameters[0] = args.Parameters[0].Replace("b", "000000000");
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

            bool clear = true;
            for (int i = 0; i < loopplayers.Count; i++)
            {
                if (loopstatus[i] != "Stopped")
                {
                    clear = false;
                    i = loopplayers.Count;
                }
            }
            if (clear)
            {
                loopplayers.Clear();
                loopstatus.Clear();
                loopcommands.Clear();
                looptimes.Clear();
            }

            if (args.Parameters.Count == 1)
            {
                var option = args.Parameters[0].ToLower();

                if (option == "list")
                {
                    if (!player.Group.HasPermission("commanddelay.loop.manage"))
                    {
                        player.SendErrorMessage(NoPermissionError);
                        return;
                    }
                    int myid = 0;
                    var results = new List<string>();
                    for (int i = 0; i < loopplayers.Count; i++)
                    {
                        if (loopplayers[i] == player)
                        {
                            myid += 1;
                            results.Add(String.Format("[{0}]: {1}", myid, loopstatus[i]));
                        }
                    }
                    player.SendSuccessMessage("Your existing loops:");
                    player.SendInfoMessage(results.Count > 0 ? String.Join(", ", results) : "You do not currently have any loops running.");
                    return;
                }
                else
                {
                    player.SendErrorMessage(SyntaxErrorPrefix + "/loop <list/resume/pause/stop/new loop amount> <loop ID>");
                }
            }
            if (args.Parameters.Count == 2)
            {
                var option = args.Parameters[0].ToLower();
                if (!player.Group.HasPermission("commanddelay.loop.manage") && (option == "resume" || option == "stop" || option == "pause"))
                {
                    player.SendErrorMessage(NoPermissionError);
                    return;
                }
                var amount = args.Parameters[1];
                int loopid;
                if (!int.TryParse(args.Parameters[1], out loopid))
                {
                    player.SendErrorMessage("Loop ID must be a number.");
                    return;
                }
                loopid = Convert.ToInt32(args.Parameters[1]);
                var myloops = new List<int>();

                for (int i = 0; i < loopplayers.Count; i++)
                {
                    if (loopplayers[i] == player)
                    {
                        myloops.Add(i);
                    }
                }

                if (myloops.Count == 0)
                {
                    player.SendErrorMessage("You do not currently have any existing loops.");
                    return;
                }

                if (myloops.Count < loopid || loopid < 1)
                {
                    player.SendErrorMessage("Invalid loop ID.");
                    return;
                }

                if (option == "stop")
                {
                    loopstatus[myloops[loopid - 1]] = "Stopped";
                    player.SendSuccessMessage("Successfully stopped loop [{0}].", loopid);
                    return;
                }
                if (option == "resume")
                {
                    loopstatus[myloops[loopid - 1]] = "Resumed";
                    player.SendSuccessMessage("Successfully resumed loop [{0}].", loopid);
                    return;
                }
                if (option == "pause")
                {
                    loopstatus[myloops[loopid - 1]] = "Paused";
                    player.SendSuccessMessage("Successfully paused loop [{0}].", loopid);
                    return;
                }
            }
            if (args.Parameters.Count > 0)
            {
                int amount = 0;
                args.Parameters[0] = args.Parameters[0].Replace("k", "000");
                args.Parameters[0] = args.Parameters[0].Replace("m", "000000");
                args.Parameters[0] = args.Parameters[0].Replace("b", "000000000");
                if (!int.TryParse(args.Parameters[0], out amount))
                {
                    player.SendErrorMessage("Amount must be a number.");
                    return;
                }
                else if (Convert.ToInt32(args.Parameters[0]) < 1)
                {
                    player.SendErrorMessage("Amount must be positive.");
                    return;
                }
                else
                {
                    var parameters = new List<string>(args.Parameters);
                    parameters.RemoveAt(0);
                    string command = String.Join(" ", args.Parameters);
                    if (!command.StartsWith("/"))
                    {
                        command = "/" + command;
                    }
                    for (int i = 0; i < amount; i++)
                    {
                        Group group = player.Group;
                        if (group.HasPermission("commanddelay.loop.anycommand"))
                        {
                            player.Group = new SuperAdminGroup();
                        }
                        Commands.HandleCommand(player, command);
                        if (!command.StartsWith("/user group " + player.UserAccountName) && group.HasPermission("commanddelay.loop.anycommand"))
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
            try
            {
                bool clear = true;
                bool anycommand = false;

                for (int i = 0; i < loopplayers.Count; i++)
                {
                    if (loopstatus[i] != "Stopped")
                    {
                        clear = false;
                        i = loopplayers.Count;
                    }
                }
                if (clear)
                {
                    loopplayers.Clear();
                    loopstatus.Clear();
                    looptimes.Clear();
                    loopcommands.Clear();
                }

                if (player.Group.HasPermission("commanddelay.execute.anycommand"))
                {
                    anycommand = true;
                }

                string command = String.Join(" ", args.Parameters);


                if (args.Parameters.Count == 1)
                {
                    if (args.Parameters[0] == "list")
                    {
                        if (!player.Group.HasPermission("commanddelay.execute.manage"))
                        {
                            player.SendErrorMessage(NoPermissionError);
                            return;
                        }
                        var results = new List<string>();
                        for (int i = 0; i < loopplayers.Count; i++)
                        {
                            results.Add(String.Format("[{0}]: {1} ({2}, {3}, {4})", (i + 1), loopplayers[i].Name, loopstatus[i], looptimes[i], loopcommands[i]));
                        }
                        player.SendSuccessMessage("All Existing Loops:");
                        player.SendInfoMessage(results.Count > 0 ? String.Join(", ", results) : "No loops currently exist.");
                        return;
                    }
                    else
                    {
                        player.SendErrorMessage(SyntaxErrorPrefix + "/exec [<looptime>/stop/pause/resume/list] <interval between commands> <command/options>");
                        return;
                    }
                }
                else if (args.Parameters.Count == 2)
                {
                    var option = args.Parameters[0].ToLower();
                    int loopid = 0;

                    if (option != "stop" && option != "pause" && option != "resume")
                    {
                        player.SendErrorMessage(SyntaxErrorPrefix + "/exec [<looptime>/stop/pause/resume/list] <interval between commands>");
                        return;
                    }
                    if (!player.Group.HasPermission("commanddelay.execute.manage"))
                    {
                        player.SendErrorMessage(NoPermissionError);
                        return;
                    }
                    if (!int.TryParse(args.Parameters[1], out loopid) || loopstatus.Count < loopid)
                    {
                        player.SendErrorMessage("Invalid loop ID.");
                        return;
                    }
                    loopid = Convert.ToInt32(args.Parameters[1]);
                    if (loopid < 1)
                    {
                        player.SendErrorMessage("Loop ID must be positive");
                        return;
                    }
                    if (loopstatus[loopid - 1] == "Stopped")
                    {
                        player.SendErrorMessage("You cannot modify stopped loops.");
                        return;
                    }
                    if (option == "stop")
                    {
                        loopstatus[loopid - 1] = "Stopped";
                        player.SendSuccessMessage(string.Format("Stopped loop {0}.", loopid));
                        return;
                    }
                    else if (option == "resume")
                    {
                        if (loopstatus[loopid - 1] != "Running")
                        {
                            loopstatus[loopid - 1] = "Running";
                            player.SendSuccessMessage(string.Format("Resumed loop {0}.", loopid));
                            return;
                        }
                        else
                        {
                            player.SendErrorMessage(string.Format("Thread {0} is already running.", loopid));
                            return;
                        }
                    }
                    else if (option == "pause")
                    {
                        if (loopstatus[loopid - 1] != "Paused")
                        {
                            loopstatus[loopid - 1] = "Paused";
                            player.SendSuccessMessage(string.Format("Paused loop {0}.", loopid));
                            return;
                        }
                        else
                        {
                            player.SendErrorMessage(string.Format("Thread {0} is already paused.", loopid));
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
                    args.Parameters[0] = args.Parameters[0].Replace("k", "000").Replace("m", "000000").Replace("b", "000000000");
                    args.Parameters[1] = args.Parameters[1].Replace("k", "000").Replace("m", "000000").Replace("b", "000000000");
                    int amount = 0;
                    int interval = 0;
                    if (args.Parameters[0].ToLower() != "inf")
                    {
                        if (!int.TryParse(args.Parameters[0], out amount))
                        {
                            player.SendErrorMessage("Amount must be a number.");
                            return;
                        }
                        else if (Convert.ToInt32(args.Parameters[0]) < 0)
                        {
                            player.SendErrorMessage("Amount must be positive.");
                            return;
                        }
                    }
                    if (!int.TryParse(args.Parameters[1], out interval))
                    {
                        player.SendErrorMessage("Delay interval must be a number.");
                        return;
                    }
                    else if (Convert.ToInt32(args.Parameters[1]) < 0)
                    {
                        player.SendErrorMessage("Delay interval must be positive or zero.");
                        return;
                    }
                    else
                    {
                        var newthread = new ExecThread(args, loopplayers.Count, anycommand);
                        var thread = new Thread(new ThreadStart(newthread.Loop));
                        thread.Start();
                        loopplayers.Add(player);
                        loopstatus.Add("Running");
                        try
                        {
                            loopcommands.Add("'/" + args.Parameters[2] + "'");
                        }
                        catch (Exception e)
                        {
                            Log.ConsoleError(String.Format("CommandDelay Special Error [1] [Params: {0}]: {1}", String.Join(", ", args.Parameters), e.Message));
                        }
                        looptimes.Add(String.Format("{0}x{1}", args.Parameters[0], args.Parameters[1]));
                        Log.ConsoleInfo(String.Format("CommandDelay: {0} started loop ({1}x{2}, {3})", player.Name, args.Parameters[0], args.Parameters[1], "'/" + args.Parameters[2] + "'"));
                        return;
                    }
                }
                else
                {
                    player.SendErrorMessage(SyntaxErrorPrefix + "/exec [<looptime>/stop/pause/resume/list] <interval between commands> <command/options>");
                    return;
                }
            }
            catch (Exception e)
            {
                Log.ConsoleError("CD Exec Error: " + e.Message);
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
            var parameters = new List<string>(args.Parameters);
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
                if (group.HasPermission("commanddelay.delay.anycommand"))
                {
                    player.Group = new SuperAdminGroup();
                }
                Commands.HandleCommand(player, command);
                if (!command.StartsWith("/user group " + player.UserAccountName) && group.HasPermission("commanddelay.delay.anycommand"))
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
        int loopid;
        bool ignoreperms;

        public ExecThread(CommandArgs args, int loopid, bool anycommand)
        {
            this.args = args;
            this.loopid = loopid;
            this.ignoreperms = anycommand;
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
                var parameters = new List<string>(args.Parameters);
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

                string normalcommand = command;

                int i = 0;
                int count = 0;
                while (i < amount)
                {
                    if (!infloop)
                    {
                        i += 1;
                    }
                    count += 1;
                    command = normalcommand;
                    command = command.Replace("%i", "" + (count));
                    command = command.Replace("%-i", "" + (amount-i));

                    seconds = interval;
                    while (seconds-- > 0)
                    {
                        if (Netplay.disconnect || CommandDelay.loopstatus[loopid] == "Stopped")
                            return;
                        System.Threading.Thread.Sleep(1000);
                        if (CommandDelay.loopstatus[loopid] == "Stopped")
                            return;
                        while (CommandDelay.loopstatus[loopid] == "Paused")
                        {
                            System.Threading.Thread.Sleep(1000);
                            //Waiting until the loop is resumed, is this okay to do?
                            if (Netplay.disconnect || CommandDelay.loopstatus[loopid] == "Stopped")
                                return;
                        }
                    }
                    Group group = player.Group;
                    if (ignoreperms)
                    {
                        player.Group = new SuperAdminGroup();
                    }
                    Commands.HandleCommand(player, command);
                    if (!command.ToLower().StartsWith("/user group " + player.UserAccountName) && ignoreperms)
                    {
                        player.Group = group;
                    }
                }
                if (CommandDelay.loopstatus.Count >= loopid)
                {
                    CommandDelay.loopstatus[loopid] = "Stopped";
                }
            }
            catch (Exception e){}
        }
    }
}