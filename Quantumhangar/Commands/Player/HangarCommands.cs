using NLog.Targets;
using QuantumHangar.HangarChecks;
using QuantumHangar.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using System.Collections.Generic;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace QuantumHangar.Commands
{
    [Category("hangar")]
    public class HangarCommands : CommandModule
    {
        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void SaveGrid()
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }


            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.SaveGrid(), Context);
        }

        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void ListGrids()
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.ListGrids(), Context);
        }

        [Command("load", "Loads the specified grid by index number")]
        [Permission(MyPromoteLevel.None)]
        public async void Load(string id, bool loadNearPlayer = false)
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.LoadGrid(id, loadNearPlayer), Context);
        }

        [Command("remove", "removes the grid from your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void Remove(string id)
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.RemoveGrid(id), Context);
        }


        [Command("info", "Provides some info of the current grid in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void Info(string id = "")
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.DetailedInfo(id), Context);
        }
    }

    [Category("h")]
    public class HangarSimpCommands : CommandModule
    {
        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void SaveGrid()
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }


            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.SaveGrid(), Context);
        }

        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void ListGrids()
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.ListGrids(), Context);
        }

        [Command("load", "Loads the specified grid by index number")]
        [Permission(MyPromoteLevel.None)]
        public async void Load(string id, bool loadNearPlayer = false)
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.LoadGrid(id, loadNearPlayer), Context);
        }

        [Command("remove", "removes the grid from your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void Remove(string id)
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.RemoveGrid(id), Context);
        }


        [Command("info", "Provides some info of the current grid in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void Info(string id = "")
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new PlayerChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.DetailedInfo(id), Context);
        }


        [Command("box", "Displays bounding boxes of the grid you are looking at")]
        [Permission(MyPromoteLevel.None)]
        public void displayBox()
        {
            if (Context.Player == null)
                return;

            if (!GridUtilities.FindGridList(null, (MyCharacter)Context.Player.Character, out List<MyCubeGrid> Grids))
            {
                Context.Respond("No grids found. Check your viewing angle or make sure you spelled right!");
                return;
            }

            DrawDebug s = new DrawDebug("Hangar_Debug");
            Color color = new Color(255, 255, 0, 10);

            foreach(var grid in Grids)
                s.addOBBLinkedEntity(grid.EntityId, color, MySimpleObjectRasterizer.Wireframe, 1f, 0.005f);


            ModCommunication.SendMessageTo(s, Context.Player.SteamUserId);

        }


        [Command("boxclear", "Clears screen of all displayed bounding boxes")]
        [Permission(MyPromoteLevel.None)]
        public void clearBox()
        {
            if (Context.Player == null)
                return;

            DrawDebug s = new DrawDebug("Hangar_Debug");
            s.removeAll = true;

            ModCommunication.SendMessageTo(s, Context.Player.SteamUserId);
        }

    }
}