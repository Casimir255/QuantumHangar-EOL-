using QuantumHangar.Commands;
using QuantumHangar.HangarChecks;
using System.Text;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace QuantumHangar.HangarMarket
{
    [Category("hangar")]
    public class HangarMarketCommands : CommandModule
    {
        [Command("sell", "Sells the grid")]
        [Permission(MyPromoteLevel.None)]
        public async void SellGrid(int ID, long price, string description)
        {
            if (!Hangar.Config.PluginEnabled)
                return;


            if (Context.Player == null || !Hangar.Config.GridMarketEnabled)
            {
                Context.Respond("This is a player only command!");
                return;
            }


            var User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => User.SellGrid(ID, price, description), Context);
        }


        [Command("marketlist", "Lists all grids for sale")]
        [Permission(MyPromoteLevel.None)]
        public void MarketList()
        {
            if (!Hangar.Config.GridMarketEnabled || !Hangar.Config.PluginEnabled)
                return;


            var Builder = new StringBuilder();
            foreach (var Value in HangarMarketController.MarketOffers.Values)
                Builder.AppendLine($"{Value.Name} - {Value.Price}");

            var Response = new Chat(Context);
            Response.Respond(Builder.ToString());
        }
    }


    [Category("h")]
    public class HangarMarketCommandsSimp : CommandModule
    {
        [Command("sell", "Sells the grid")]
        [Permission(MyPromoteLevel.None)]
        public async void SellGrid(int ID, long price, string description)
        {
            if (!Hangar.Config.PluginEnabled)
                return;

            if (Context.Player == null || !Hangar.Config.GridMarketEnabled)
            {
                Context.Respond("This is a player only command!");
                return;
            }


            var User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => User.SellGrid(ID, price, description), Context);
        }


        [Command("marketlist", "Lists all grids for sale")]
        [Permission(MyPromoteLevel.None)]
        public void MarketList()
        {
            if (!Hangar.Config.GridMarketEnabled || !Hangar.Config.PluginEnabled)
                return;


            var Builder = new StringBuilder();
            foreach (var Value in HangarMarketController.MarketOffers.Values)
                Builder.AppendLine($"{Value.Name} - {Value.Price}");

            var Response = new Chat(Context);
            Response.Respond(Builder.ToString());
        }
    }
}