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


        public static async Task RunTask(Action Invoker, ulong? SteamID = null)
        {
            if (!Hangar.ServerRunning || !MySession.Static.Ready)
                return;


            if (SteamID.HasValue && SteamID.Value != 1 && Dictionary.TryGetValue(SteamID.Value, out var RunningTask))
            {
                if (RunningTask.Status == TaskStatus.Running)
                {
                    var A = new ScriptedChatMsg
                    {
                        Author = "Hangar",
                        Target = MySession.Static.Players.TryGetIdentityId(SteamID.Value),
                        Text = "Your previous command has yet to finish! Please wait!",
                        Font = "Blue",
                        Color = VRageMath.Color.Yellow
                    };

                    MyMultiplayerBase.SendScriptedChatMessage(ref A);


                    //Log.Warn("Aborted Action!");
                    return;
                }

                var Builder = new StringBuilder();
                Builder.AppendLine($"Task is being removed! Status: {RunningTask.Status}");

                foreach (var ex in RunningTask.Exception.InnerExceptions) Builder.AppendLine(ex.ToString());

                Log.Error(Builder.ToString());
                Dictionary.TryRemove(SteamID.Value, out _);
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

            SteamID ??= 1;


            if (SteamID.Value != 1)
            {
                void Completed()
                {
                    RemoveAfterCompletion(SteamID.Value);
                }

                Invoker += Completed;

                Dictionary.TryAdd(SteamID.Value, null);
                await Task.Run(() => Invoker);


                return;
            }

            if (SteamID.Value == 1)
            {
                //Log.Warn("PP");
                await Task.Run(() => Invoker);
            }
        }

        private static void RemoveAfterCompletion(ulong SteamID)
        {
            if (Dictionary.ContainsKey(SteamID))
                Dictionary.TryRemove(SteamID, out var task);

            Log.Warn(SteamID + " Action completed!");
        }


        public static async Task RunTaskAsync(Action Function, CommandContext Context)
        {
            if (!CheckTaskStatus(Context))
                return;

            var steamUserId = Context.Player.SteamUserId;
            try
            {
                await Task.Run(Function);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                Context.Respond("An error occurred when running this command! Check logs for more details!");
            }

            RemoveCompletedTask(steamUserId);
        }


        public static async Task RunAdminTaskAsync(Action Function)
        {
            await Task.Run(Function);
        }


        public static bool CheckTaskStatus(CommandContext Context)
        {
            if (RunningTasks.Contains(Context.Player.SteamUserId))
            {
                Context.Respond("Your previous command has yet to finish!");
                return false;
            }


            RunningTasks.Add(Context.Player.SteamUserId);
            return true;
        }


        public static void RemoveCompletedTask(ulong SteamUserID)
        {
            if (RunningTasks.Contains(SteamUserID))
                RunningTasks.Remove(SteamUserID);
        }
    }
}