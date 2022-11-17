using QuantumHangar.HangarChecks;
using System;
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
        public async void SaveGrid(string gridName = "")
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.SaveGrid(gridName));
        }

        [Command("list", "Lists all grids of given player")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void ListGrids(string nameOrSteamId)
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.ListGrids(nameOrSteamId));
        }

        [Command("load", "loads selected grid from a users hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void LoadGrid(string nameOrSteamId, int id, bool fromSavePos = true)
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.LoadGrid(nameOrSteamId, id, fromSavePos));
        }

        [Command("remove", "removes the grid from the players hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void Remove(string nameOrSteamId, int id)
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.RemoveGrid(nameOrSteamId, id));
        }

        [Command("sync", "Re-scans the folder for added or removed grids")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void Sync(string nameOrSteamId)
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.SyncHangar(nameOrSteamId));

        }

        [Command("syncall", "Re-scans all folders for added or removed grids")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SyncAll()
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.SyncAll());
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

            foreach (var @override in overrides) {
                if (@override == "staticgrids")
                {
                    staticgrids = true;
                } else if (@override == "largegrids")
                {
                    largegrids = true;
                } else if (@override == "smallgrids")
                {
                    smallgrids = true;
                } else if (@override == "hangerLargest")
                {
                    hangerLargest = true;
                } else if (@override == "saveAll")
                {
                    saveAll = true;
                }
            }
            await HangarCommandSystem.RunAdminTaskAsync(() => AutoHangar.RunAutoHangar(saveAll, staticgrids, largegrids, smallgrids, hangerLargest));

        }

        [Command("enable", "Enables Hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Enable(bool setPluginEnable)
        {
            Hangar.Config.PluginEnabled = setPluginEnable;
        }


        [Command("saveall", "Saves All grids on server into hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SaveAllGrids()
        {
            await HangarCommandSystem.RunAdminTaskAsync(() => AutoHangar.RunAutoHangar(true, true, true, true, true));
        }

    }


    [Torch.Commands.Category("hm")]
    public class HangarSimpAdminCommands : CommandModule
    {

        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SaveGrid(string gridName = "")
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.SaveGrid(gridName));
        }

        [Command("list", "Lists all grids of given player")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void ListGrids(string nameOrSteamId)
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.ListGrids(nameOrSteamId));
        }

        [Command("load", "loads selected grid from a users hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SaveGrid(string nameOrSteamId, int id, bool fromSavePos = true)
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.LoadGrid(nameOrSteamId, id, fromSavePos));
        }

        [Command("remove", "removes the grid from the players hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void Remove(string nameOrSteamId, int id)
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.RemoveGrid(nameOrSteamId, id));
        }

        [Command("sync", "Re-scans the folder for added or removed grids")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void Sync(string nameOrSteamId)
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.SyncHangar(nameOrSteamId));

        }

        [Command("syncall", "Re-scans all folders for added or removed grids")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SyncAll()
        {
            AdminChecks user = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.SyncAll());

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

            foreach (var @override in overrides) {
                if (@override == "staticgrids")
                {
                    staticgrids = true;
                } else if (@override == "largegrids")
                {
                    largegrids = true;
                } else if (@override == "smallgrids")
                {
                    smallgrids = true;
                } else if (@override == "hangerLargest")
                {
                    hangerLargest = true;
                } else if (@override == "saveAll")
                {
                    saveAll = true;
                }
            }
            await HangarCommandSystem.RunAdminTaskAsync(() => AutoHangar.RunAutoHangar(saveAll, staticgrids, largegrids, smallgrids, hangerLargest));

        }

        [Command("enable", "Enables Hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Enable(bool setPluginEnable)
        {
            Hangar.Config.PluginEnabled = setPluginEnable;
        }

        [Command("saveall", "Saves All grids on server into hangar")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void SaveAllGrids()
        {
            await HangarCommandSystem.RunAdminTaskAsync(() => AutoHangar.RunAutoHangar(true, true, true, true, true));
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
