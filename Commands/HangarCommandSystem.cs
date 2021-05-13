using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace QuantumHangar.Commands
{
    public static class HangarCommandSystem
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /* Need an action system/Queue*/
        private static ConcurrentDictionary<ulong, Task> Dictionary = new ConcurrentDictionary<ulong, Task>();

        public static void RunTask(Action Invoker, ulong? SteamID = null)
        {
            if (!Hangar.ServerRunning || !MySession.Static.Ready)
                return;


            if (SteamID.HasValue && SteamID.Value != 1 && Dictionary.TryGetValue(SteamID.Value, out Task RunningTask))
            {
                if (RunningTask.Status == TaskStatus.Running)
                {
                    ScriptedChatMsg A = new ScriptedChatMsg();
                    A.Author = "Hangar";
                    A.Target = MySession.Static.Players.TryGetIdentityId(SteamID.Value);
                    A.Text = "Your previous command has yet to finish! Please wait!";
                    A.Font = "Blue";
                    A.Color = VRageMath.Color.Yellow;

                    MyMultiplayerBase.SendScriptedChatMessage(ref A);


                    //Log.Warn("Aborted Action!");
                    return;
                }

                StringBuilder Builder = new StringBuilder();
                Builder.AppendLine($"Task is being removed! Status: {RunningTask.Status}");

                foreach (Exception ex in RunningTask.Exception.InnerExceptions)
                {
                    Builder.AppendLine(ex.ToString());
                }

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

            if (!SteamID.HasValue)
                SteamID = 1;


            if (SteamID.Value != 1)
            {
                Action Completed = delegate { RemoveAfterCompletion(SteamID.Value); };
                Invoker += Completed;
                Task Run = new Task(Invoker);
                Run.Start();

                Dictionary.TryAdd(SteamID.Value, Run);
                return;
            }

            if (SteamID.Value == 1)
            {
                //Log.Warn("PP");
                Task Run = new Task(Invoker);
                Run.Start();
                return;
            }



        }

        private static void RemoveAfterCompletion(ulong SteamID)
        {
            if (Dictionary.ContainsKey(SteamID))
                Dictionary.TryRemove(SteamID, out _);

            Log.Warn(SteamID + " Action completed!");
        }
    }
}
