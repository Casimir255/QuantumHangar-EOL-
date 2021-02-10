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
    public static class CommandSystem
    {
        private static readonly Logger Log = LogManager.GetLogger("Hangar." + nameof(CommandSystem));

        /* Need an action system/Queue*/
        private static ConcurrentDictionary<ulong, bool> Dictionary = new ConcurrentDictionary<ulong, bool>();




        public static void RunTask(Action Invoker, ulong? SteamID = null)
        {
            if (!Hangar.ServerRunning || !MySession.Static.Ready)
                return;


            if (SteamID.HasValue && Dictionary.ContainsKey(SteamID.Value))
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

            if (SteamID.HasValue)
            {
                Dictionary.TryAdd(SteamID.Value, true);
                Action Completed = delegate { RemoveAfterCompletion(SteamID.Value); };
                Invoker += Completed;
            }

            Task Run = new Task(Invoker);
            Run.Start();
        }



        private static void RemoveAfterCompletion(ulong SteamID)
        {
            if (Dictionary.ContainsKey(SteamID))
                Dictionary.TryRemove(SteamID, out _);

            Log.Warn(SteamID + " Action completed!");
        }
    }
}
