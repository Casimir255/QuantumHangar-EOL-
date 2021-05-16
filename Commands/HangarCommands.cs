using QuantumHangar.HangarChecks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public async void SaveGrid()
        {
            PlayerChecks User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => User.SaveGrid(), Context);
        }

        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void ListGrids()
        {
            PlayerChecks User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => User.ListGrids(), Context);
        }

        [Command("load", "Loads the specified grid by index number")]
        [Permission(MyPromoteLevel.None)]
        public async void Load(int ID, bool LoadNearPlayer = false)
        {
            PlayerChecks User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => User.LoadGrid(ID, LoadNearPlayer), Context);
        }

        [Command("remove", "removes the grid from your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void Remove(int ID)
        {
            PlayerChecks User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => User.RemoveGrid(ID), Context);
        }


        [Command("info", "Provides some info of the current grid in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void Info(int ID = 0)
        {
            PlayerChecks User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => User.DetailedInfo(ID), Context);
        }
    }

    [Torch.Commands.Category("h")]
    public class HangarSimpCommands : CommandModule
    {

        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void SaveGrid()
        {
            PlayerChecks User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(User.SaveGrid, Context);
        }

        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void ListGrids()
        {
            PlayerChecks User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(User.ListGrids, Context);
        }

        [Command("load", "Loads the specified grid by index number")]
        [Permission(MyPromoteLevel.None)]
        public async void Load(int ID, bool LoadNearPlayer = false)
        {
            PlayerChecks User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => User.LoadGrid(ID, LoadNearPlayer), Context);
        }

        [Command("remove", "removes the grid from your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void Remove(int ID)
        {
            PlayerChecks User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => User.RemoveGrid(ID), Context);
        }


        [Command("info", "Provides some info of the current grid in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void Info(int ID = 0)
        {
            PlayerChecks User = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => User.DetailedInfo(ID), Context);
        }

    }

}
