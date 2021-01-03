using QuantumHangar.HangarChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace QuantumHangar.Commands
{

    [Torch.Commands.Category("hangar")]
    public class HangarCommands : CommandModule
    {

        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.None)]
        public void SaveGrid()
        {
            PlayerChecks User = new PlayerChecks(Context);
            CommandSystem.RunTask(delegate { User.SaveGrid(); });
        }

        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void ListGrids()
        {
            PlayerChecks User = new PlayerChecks(Context);
            CommandSystem.RunTask(delegate { User.ListGrids(); });
        }

        [Command("load", "Loads the specified grid by index number")]
        [Permission(MyPromoteLevel.None)]
        public void Load(int ID)
        {
            PlayerChecks User = new PlayerChecks(Context);
            CommandSystem.RunTask(delegate { User.LoadGrid(ID); });
        }
    }

    public static class PlayerHangarCommands
    {

    }


}
