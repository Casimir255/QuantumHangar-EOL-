using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace QuantumHangar.HangarMarket
{

    [Torch.Commands.Category("hangar")]
    public class HangarMarketCommands : CommandModule
    {


        [Command("sell", "Sells the grid")]
        [Permission(MyPromoteLevel.None)]
        public async void SellGrid(int ID, double price, string description)
        {



        }


    }
}
