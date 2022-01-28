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
        public async void SaveGrid(string GridName = "")
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.SaveGrid(GridName));
        }

        [Command("list", "Lists all grids of given player")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void ListGrids(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.ListGrids(NameOrSteamID));
        }

        [Command("load", "loads selected grid from a users hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SaveGrid(string NameOrSteamID, int ID, bool FromSavePos = true)
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.LoadGrid(NameOrSteamID, ID, FromSavePos));
        }

        [Command("remove", "removes the grid from the players hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void Remove(string NameOrSteamID, int ID)
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.RemoveGrid(NameOrSteamID, ID));
        }

        [Command("sync", "Re-scans the folder for added or removed grids")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void Sync(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.SyncHangar(NameOrSteamID));

        }

        [Command("syncall", "Re-scans all folders for added or removed grids")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SyncAll()
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.SyncAll());
        }

        [Command("autohangar", "Runs Autohangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void RunAutoHangar()
        {
            await HangarCommandSystem.RunAdminTaskAsync(() => AutoHangar.RunAutoHangar(false, Hangar.Config.AutoHangarStaticGrids, Hangar.Config.AutoHangarLargeGrids, Hangar.Config.AutoHangarSmallGrids, Hangar.Config.KeepPlayersLargestGrid));
        }

        [Command("autohanger-override", "Runs Autohanger with override values (staticgrids, largegrids, smallgrids, hangerLargest, saveAll)")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void RunAutoHangerOverride(String[] overrides)
        {
            bool staticgrids = false;
            bool largegrids = false;
            bool smallgrids = false;
            bool hangerLargest = false;
            bool saveAll = false;

            foreach (var _override in overrides) {
                if (_override == "staticgrids")
                {
                    staticgrids = true;
                } else if (_override == "largegrids")
                {
                    largegrids = true;
                } else if (_override == "smallgrids")
                {
                    smallgrids = true;
                } else if (_override == "hangerLargest")
                {
                    hangerLargest = true;
                } else if (_override == "saveAll")
                {
                    saveAll = true;
                }
            }
            await HangarCommandSystem.RunAdminTaskAsync(() => AutoHangar.RunAutoHangar(saveAll, staticgrids, largegrids, smallgrids, hangerLargest));

        }

        [Command("enable", "Enables Hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Enable(bool SetPluginEnable)
        {
            Hangar.Config.PluginEnabled = SetPluginEnable;
        }


        [Command("saveall", "Saves All grids on server into hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SaveAllGrids()
        {
            await HangarCommandSystem.RunAdminTaskAsync(() => AutoHangar.RunAutoHangar(true));
        }

    }


    [Torch.Commands.Category("hm")]
    public class HangarSimpAdminCommands : CommandModule
    {

        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SaveGrid(string GridName = "")
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.SaveGrid(GridName));
        }

        [Command("list", "Lists all grids of given player")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void ListGrids(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.ListGrids(NameOrSteamID));
        }

        [Command("load", "loads selected grid from a users hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SaveGrid(string NameOrSteamID, int ID, bool FromSavePos = true)
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.LoadGrid(NameOrSteamID, ID, FromSavePos));
        }

        [Command("remove", "removes the grid from the players hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void Remove(string NameOrSteamID, int ID)
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.RemoveGrid(NameOrSteamID, ID));
        }

        [Command("sync", "Re-scans the folder for added or removed grids")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void Sync(string NameOrSteamID)
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.SyncHangar(NameOrSteamID));

        }

        [Command("syncall", "Re-scans all folders for added or removed grids")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SyncAll()
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.SyncAll());

        }

        [Command("autohangar", "Runs Autohangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void RunAutoHangar()
        {
            await HangarCommandSystem.RunAdminTaskAsync(() => AutoHangar.RunAutoHangar(false, Hangar.Config.AutoHangarStaticGrids, Hangar.Config.AutoHangarLargeGrids, Hangar.Config.AutoHangarSmallGrids, Hangar.Config.KeepPlayersLargestGrid));
        }

        [Command("autohanger-override", "Runs Autohanger with override values (staticgrids, largegrids, smallgrids, hangerLargest, saveAll)")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void RunAutoHangerOverride(String[] overrides)
        {
            bool staticgrids = false;
            bool largegrids = false;
            bool smallgrids = false;
            bool hangerLargest = false;
            bool saveAll = false;

            foreach (var _override in overrides) {
                if (_override == "staticgrids")
                {
                    staticgrids = true;
                } else if (_override == "largegrids")
                {
                    largegrids = true;
                } else if (_override == "smallgrids")
                {
                    smallgrids = true;
                } else if (_override == "hangerLargest")
                {
                    hangerLargest = true;
                } else if (_override == "saveAll")
                {
                    saveAll = true;
                }
            }
            await HangarCommandSystem.RunAdminTaskAsync(() => AutoHangar.RunAutoHangar(saveAll, staticgrids, largegrids, smallgrids, hangerLargest));

        }

        [Command("enable", "Enables Hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Enable(bool SetPluginEnable)
        {
            Hangar.Config.PluginEnabled = SetPluginEnable;
        }

        [Command("saveall", "Saves All grids on server into hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SaveAllGrids()
        {
            await HangarCommandSystem.RunAdminTaskAsync(() => AutoHangar.RunAutoHangar(true));
        }


        /*
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
        */
    }



}
