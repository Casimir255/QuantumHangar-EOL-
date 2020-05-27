using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Torch.Managers.PatchManager;

namespace QuantumHangar.Utilities
{
    public class Patcher
    {
        private static Hangar Plugin { get; set; }
        private static GridTracker Tracker { get; set; }



        public void Apply(PatchContext ctx, Hangar plugin)
        {
            if (plugin.Config.PluginEnabled)
            {

                var SaveMethod = typeof(MySession).GetMethod("Save", BindingFlags.Public | BindingFlags.Instance, null,
                new Type[] { typeof(MySessionSnapshot).MakeByRefType(), typeof(string) }, null);
                if (SaveMethod == null)
                {
                    throw new InvalidOperationException("Couldn't find Save");
                }
                ctx.GetPattern(SaveMethod).Suffixes.Add(Method(nameof(AfterSave)));
                Plugin = plugin;
                Tracker = plugin.Tracker;

            }
        }

        private static MethodInfo Method(string name)
        {
            return typeof(Patcher).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        }

        private static void AfterSave(bool __result)
        {
            if (__result)
            {
                Hangar.Debug("Running Server Saves! : " + __result);
                EconPlayerSaver.SaveOnlinePlayerAccounts(Plugin);

                Tracker.ServerSave();
                //Main.Debug(Plugin.Config.FolderDirectory);
            }
        }
    }


    class EconPlayerSaver
    {
        public static void SaveOnlinePlayerAccounts(Hangar Plugin)
        {

            if (Plugin == null)
            {
                Hangar.Debug("Major Error! Plugin refrence is null!");
                return;
            }

            if(!Plugin.Config.PluginEnabled || !Plugin.Config.CrossServerEcon)
            {
                return;
            }
            //Saving all online player balances

            List<PlayerAccount> PAccounts = new List<PlayerAccount>();

            foreach (MyPlayer player in MySession.Static.Players.GetOnlinePlayers())
            {


                ulong ID = MySession.Static.Players.TryGetSteamId(player.Identity.IdentityId);

                //Check if steam id is null
                if (ID == 0)
                    continue;

                EconUtils.TryGetPlayerBalance(ID, out long balance);
                PAccounts.Add(new PlayerAccount(player.DisplayName, ID, balance));
            }
            Hangar.Debug("Saving all online player Accounts!");
            //Attempt to broadcast to server
            CrossServerMessage message = new CrossServerMessage();
            message.Type = CrossServer.MessageType.PlayerAccountUpdated;
            message.BalanceUpdate = PAccounts;
            Plugin.Market.MarketServers.Update(message);
        }
    }
}
