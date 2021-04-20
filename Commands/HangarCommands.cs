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
            HangarCommandSystem.RunTask(delegate { User.SaveGrid(); }, Context.Player?.SteamUserId);
        }

        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void ListGrids()
        {
            PlayerChecks User = new PlayerChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.ListGrids(); }, Context.Player?.SteamUserId);
        }

        [Command("load", "Loads the specified grid by index number")]
        [Permission(MyPromoteLevel.None)]
        public void Load(int ID, bool LoadNearPlayer = false)
        {
            PlayerChecks User = new PlayerChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.LoadGrid(ID, LoadNearPlayer); }, Context.Player?.SteamUserId);
        }

        [Command("remove", "removes the grid from your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Remove(int ID)
        {
            PlayerChecks User = new PlayerChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.RemoveGrid(ID); }, Context.Player?.SteamUserId);
        }


        [Command("info", "Provides some info of the current grid in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Info(int ID = 0)
        {
            PlayerChecks User = new PlayerChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.DetailedInfo(ID); }, Context.Player?.SteamUserId);
        }
    }

    [Torch.Commands.Category("h")]
    public class HangarSimpCommands : CommandModule
    {

        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.None)]
        public void SaveGrid()
        {
            PlayerChecks User = new PlayerChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.SaveGrid(); }, Context.Player?.SteamUserId);
        }

        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void ListGrids()
        {
            PlayerChecks User = new PlayerChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.ListGrids(); }, Context.Player?.SteamUserId);
        }

        [Command("load", "Loads the specified grid by index number")]
        [Permission(MyPromoteLevel.None)]
        public void Load(int ID, bool LoadNearPlayer = false)
        {
            PlayerChecks User = new PlayerChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.LoadGrid(ID, LoadNearPlayer); }, Context.Player?.SteamUserId);
        }


        [Command("remove", "removes the grid from your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Remove(int ID)
        {
            PlayerChecks User = new PlayerChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.RemoveGrid(ID); }, Context.Player?.SteamUserId);
        }


        [Command("info", "Provides some info of the current grid in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Info(int ID = 0)
        {
            PlayerChecks User = new PlayerChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.DetailedInfo(ID); }, Context.Player?.SteamUserId);
        }

    }

}
