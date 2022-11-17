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
        public async void SellGrid(int id, long price, string description)
        {
            if (!Hangar.Config.PluginEnabled)
                return;


            if (Context.Player == null || !Hangar.Config.GridMarketEnabled)
            {
                Context.Respond("This is a player only command!");
                return;
            }


            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.SellGrid(id, price, description), Context);
        }


        [Command("marketlist", "Lists all grids for sale")]
        [Permission(MyPromoteLevel.None)]
        public void MarketList()
        {
            if (!Hangar.Config.GridMarketEnabled || !Hangar.Config.PluginEnabled)
                return;


            var builder = new StringBuilder();
            foreach (var value in HangarMarketController.MarketOffers.Values)
                builder.AppendLine($"{value.Name} - {value.Price}");

            var response = new Chat(Context);
            response.Respond(builder.ToString());
        }
    }


    [Category("h")]
    public class HangarMarketCommandsSimp : CommandModule
    {
        [Command("sell", "Sells the grid")]
        [Permission(MyPromoteLevel.None)]
        public async void SellGrid(int id, long price, string description)
        {
            if (!Hangar.Config.PluginEnabled)
                return;

            if (Context.Player == null || !Hangar.Config.GridMarketEnabled)
            {
                Context.Respond("This is a player only command!");
                return;
            }


            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.SellGrid(id, price, description), Context);
        }


        [Command("marketlist", "Lists all grids for sale")]
        [Permission(MyPromoteLevel.None)]
        public void MarketList()
        {
            if (!Hangar.Config.GridMarketEnabled || !Hangar.Config.PluginEnabled)
                return;


            var builder = new StringBuilder();
            foreach (var value in HangarMarketController.MarketOffers.Values)
                builder.AppendLine($"{value.Name} - {value.Price}");

            var response = new Chat(Context);
            response.Respond(builder.ToString());
        }
    }
}