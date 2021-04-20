using QuantumHangar.HangarChecks;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
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
        [Permission(MyPromoteLevel.Moderator)]
        public void SaveGrid(string GridName = "")
        {
            AdminChecks User = new AdminChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.SaveGrid(GridName); }, 1);
        }

        [Command("list", "Lists all grids of given player")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ListGrids(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.ListGrids(NameOrSteamID); });
        }

        [Command("load", "loads selected grid from a users hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void SaveGrid(string NameOrSteamID, int ID, bool FromSavePos = true)
        {
            AdminChecks User = new AdminChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.LoadGrid(NameOrSteamID, ID, FromSavePos); });
        }

        [Command("remove", "removes the grid from the players hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Remove(string NameOrSteamID, int ID)
        {
            AdminChecks User = new AdminChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.RemoveGrid(NameOrSteamID, ID); }, 1);
        }

        [Command("sync", "Re-scans the folder for added or removed grids")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Sync(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.SyncHangar(NameOrSteamID); });

        }

        [Command("autohangar", "Runs Autohangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void RunAutoHangar()
        {
            HangarCommandSystem.RunTask(delegate { AutoHangar.RunAutoHangar(); }, 1);
        }

        [Command("enable", "Enables Hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Enable(bool SetPluginEnable)
        {
            Hangar.Config.PluginEnabled = SetPluginEnable;
        }

    }


    [Torch.Commands.Category("hm")]
    public class HangarSimpAdminCommands : CommandModule
    {

        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void SaveGrid(string GridName = "")
        {
            AdminChecks User = new AdminChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.SaveGrid(GridName);}, 1);
        }

        [Command("list", "Lists all grids of given player")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ListGrids(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.ListGrids(NameOrSteamID); });
        }

        [Command("load", "loads selected grid from a users hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void SaveGrid(string NameOrSteamID, int ID, bool FromSavePos = true)
        {
            AdminChecks User = new AdminChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.LoadGrid(NameOrSteamID, ID, FromSavePos); }, 1);
        }

        [Command("remove", "removes the grid from the players hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Remove(string NameOrSteamID, int ID)
        {
            AdminChecks User = new AdminChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.RemoveGrid(NameOrSteamID, ID); }, 1);
        }

        [Command("sync", "Re-scans the folder for added or removed grids")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Sync(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            HangarCommandSystem.RunTask(delegate { User.SyncHangar(NameOrSteamID); }, 1);
        }

        [Command("autohangar", "Runs Autohangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void RunAutoHangar()
        {
            HangarCommandSystem.RunTask(delegate { AutoHangar.RunAutoHangar(); }, 1);
        }

        

        [Command("exportgrid", "exportgrids to obj")]
        [Permission(MyPromoteLevel.Admin)]
        public void GridExporter()
        {
            Chat chat = new Chat(Context);
            GridResult Result = new GridResult(true);

            //Gets grids player is looking at
            if (!Result.GetGrids(chat, Context.Player.Character as MyCharacter))
                return;

            chat.Respond(Result.Grids.Count.ToString());
            typeof(MyCubeGrid).GetMethod("ExportToObjFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { Result.Grids, true, false });

            chat.Respond("Done!");

            //GridStamp stamp = Result.GenerateGridStamp();

        }

        

        [Command("enable", "Enables Hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public void Enable(bool SetPluginEnable)
        {
            Hangar.Config.PluginEnabled = SetPluginEnable;
        }

    }



}
