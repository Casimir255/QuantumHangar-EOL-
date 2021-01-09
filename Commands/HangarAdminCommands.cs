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
    [Torch.Commands.Category("hangarmod")]
    public class HangarAdminCommands : CommandModule
    {

        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public void SaveGrid(string GridName = "")
        {
            AdminChecks User = new AdminChecks(Context);
            CommandSystem.RunTask(delegate { User.SaveGrid(GridName); });
        }

        [Command("list", "Lists all grids of given player")]
        [Permission(MyPromoteLevel.Admin)]
        public void ListGrids(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            CommandSystem.RunTask(delegate { User.ListGrids(NameOrSteamID); });
        }

        [Command("load", "loads selected grid from a users hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public void SaveGrid(string NameOrSteamID, int ID, bool FromSavePos = true)
        {
            AdminChecks User = new AdminChecks(Context);
            CommandSystem.RunTask(delegate { User.LoadGrid(NameOrSteamID, ID, FromSavePos); });
        }

        [Command("sync", "Re-scans the folder for added or removed grids")]
        [Permission(MyPromoteLevel.Admin)]
        public void Sync(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            CommandSystem.RunTask(delegate { User.SyncHangar(NameOrSteamID); });
        }

    }


    [Torch.Commands.Category("hm")]
    public class HangarSimpAdminCommands : CommandModule
    {

        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public void SaveGrid(string GridName = "")
        {
            AdminChecks User = new AdminChecks(Context);
            CommandSystem.RunTask(delegate { User.SaveGrid(GridName); });
        }

        [Command("list", "Lists all grids of given player")]
        [Permission(MyPromoteLevel.Admin)]
        public void ListGrids(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            CommandSystem.RunTask(delegate { User.ListGrids(NameOrSteamID); });
        }

        [Command("load", "loads selected grid from a users hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public void SaveGrid(string NameOrSteamID, int ID, bool FromSavePos = true)
        {
            AdminChecks User = new AdminChecks(Context);
            CommandSystem.RunTask(delegate { User.LoadGrid(NameOrSteamID, ID, FromSavePos); });
        }

        [Command("sync", "Re-scans the folder for added or removed grids")]
        [Permission(MyPromoteLevel.Admin)]
        public void Sync(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            CommandSystem.RunTask(delegate { User.SyncHangar(NameOrSteamID); });
        }

    }



}
