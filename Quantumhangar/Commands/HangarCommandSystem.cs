using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;

namespace QuantumHangar.Commands
{
    public static class HangarCommandSystem
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /* Need an action system/Queue*/
        private static readonly ConcurrentDictionary<ulong, Task> Dictionary = new ConcurrentDictionary<ulong, Task>();


        //Keep a list of all runing tasks
        private static readonly List<ulong> RunningTasks = new List<ulong>();


        public static async Task RunTask(Action invoker, ulong? steamId = null)
        {
            if (!Hangar.ServerRunning || !MySession.Static.Ready)
                return;


            if (steamId.HasValue && steamId.Value != 1 && Dictionary.TryGetValue(steamId.Value, out var runningTask))
            {
                if (runningTask.Status == TaskStatus.Running)
                {
                    var a = new ScriptedChatMsg
                    {
                        Author = "Hangar",
                        Target = MySession.Static.Players.TryGetIdentityId(steamId.Value),
                        Text = "Your previous command has yet to finish! Please wait!",
                        Font = "Blue",
                        Color = VRageMath.Color.Yellow
                    };

                    MyMultiplayerBase.SendScriptedChatMessage(ref a);


                    //Log.Warn("Aborted Action!");
                    return;
                }

                var builder = new StringBuilder();
                builder.AppendLine($"Task is being removed! Status: {runningTask.Status}");

                foreach (var ex in runningTask.Exception.InnerExceptions) builder.AppendLine(ex.ToString());

                Log.Error(builder.ToString());
                Dictionary.TryRemove(steamId.Value, out _);
            }


            /*
            if (!SteamID.HasValue)
            {
                //Log.Info(" Running Admin command!");

                try
                {
                    Invoker.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
                return;
            }
            */

            steamId ??= 1;


            if (steamId.Value != 1)
            {
                void Completed()
                {
                    RemoveAfterCompletion(steamId.Value);
                }

                invoker += Completed;

                Dictionary.TryAdd(steamId.Value, null);
                await Task.Run(() => invoker);


                return;
            }

            if (steamId.Value == 1)
            {
                //Log.Warn("PP");
                await Task.Run(() => invoker);
            }
        }

        private static void RemoveAfterCompletion(ulong steamId)
        {
            if (Dictionary.ContainsKey(steamId))
                Dictionary.TryRemove(steamId, out var task);

            Log.Warn(steamId + " Action completed!");
        }


        public static async Task RunTaskAsync(Action function, CommandContext context)
        {
            if (!CheckTaskStatus(context))
                return;

            var steamUserId = context.Player.SteamUserId;
            try
            {
                await Task.Run(function);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                context.Respond("An error occurred when running this command! Check logs for more details!");
            }

            RemoveCompletedTask(steamUserId);
        }


        public static async Task RunAdminTaskAsync(Action function)
        {
            await Task.Run(function);
        }


        public static bool CheckTaskStatus(CommandContext context)
        {
            if (RunningTasks.Contains(context.Player.SteamUserId))
            {
                context.Respond("Your previous command has yet to finish!");
                return false;
            }


            RunningTasks.Add(context.Player.SteamUserId);
            return true;
        }


        public static void RemoveCompletedTask(ulong steamUserId)
        {
            if (RunningTasks.Contains(steamUserId))
                RunningTasks.Remove(steamUserId);
        }
    }
}