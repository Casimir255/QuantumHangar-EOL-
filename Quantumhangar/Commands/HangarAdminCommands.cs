using QuantumHangar.HangarChecks;
using System;
using System.Net.Mime;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;
using VRageMath;

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


        [Command("ttmod", "Tests all torch mod functions")]
        [Permission(MyPromoteLevel.Moderator)]
        public void testTorchMod()
        {
            DialogMessage msg = new DialogMessage("This is title", "This is subtitle", "This is content");
            NotificationMessage nmsg = new NotificationMessage("This is message", 5000, "White");

            DrawDebug sphere = new DrawDebug("Hangar");

            Color c = new Color(0, 255, 255, 10);


            sphere.addSphere(Context.Player.GetPosition(), 1000, c, VRage.Game.MySimpleObjectRasterizer.SolidAndWireframe, 1, 0.001f);


            ModCommunication.SendMessageTo(sphere, Context.Player.SteamUserId);
            ModCommunication.SendMessageTo(msg, Context.Player.SteamUserId);
           // ModCommunication.SendMessageTo(nmsg, Context.Player.SteamUserId);

        }


        [Command("clearSpheres", "Tests all torch mod functions")]
        [Permission(MyPromoteLevel.Moderator)]
        public void testTorchMod2()
        {

            DrawDebug sphere = new DrawDebug(Context.Player.SteamUserId.ToString());

            sphere.remove = true;

            ModCommunication.SendMessageTo(sphere, Context.Player.SteamUserId);
            // ModCommunication.SendMessageTo(nmsg, Context.Player.SteamUserId);

        }

        [Command("reset", "changes the flag to allow grid to be unhangared at players location")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void Reset(string NameOrSteamID, int ID)
        {
            AdminChecks User = new AdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => User.SetForceSpawnNearPlayer(NameOrSteamID, ID));

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

        [Command("reset", "changes the flag to allow grid to be unhangared at players location")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void Reset(string NameOrSteamID, int ID)
        {
            AdminChecks User = new AdminChecks(Context); 
            await HangarCommandSystem.RunAdminTaskAsync(() => User.SetForceSpawnNearPlayer(NameOrSteamID, ID));
            
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
