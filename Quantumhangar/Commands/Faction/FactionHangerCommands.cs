using System;
using NLog.Targets;
using QuantumHangar.HangarChecks;
using QuantumHangar.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.GameServices;
using VRageMath;

namespace QuantumHangar.Commands
{

    [Category("factionhanger")]
    public class FactionHangarCommands : CommandModule
    {
        [Command("whitelist", "Add or remove a player from whitelist")]
        [Permission(MyPromoteLevel.None)]
        public async void Whitelist(string targetNameOrSteamId)
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new FactionChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.ChangeWhitelist(targetNameOrSteamId), Context);
        }
        [Command("whitelisted", "Output the whitelisted steam ids")]
        [Permission(MyPromoteLevel.None)]
        public async void Whitelist()
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new FactionChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.ListWhitelist(), Context);
        }
        [Command("webhook", "Change the webhook")]
        [Permission(MyPromoteLevel.None)]
        public async void Webhook(string webhook)
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new FactionChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.ChangeWebhook(webhook), Context);
        }
        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void SaveGrid()
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            if (CommandCooldownChecker.FailsFactionPreChecks(Context)) return;
            var user = new FactionChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.SaveGrid(), Context);
        }



        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void ListGrids()
        {
            var user = new FactionChecks(Context);
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

            if (CommandCooldownChecker.FailsFactionPreChecks(Context)) return;
            var user = new FactionChecks(Context);
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

            var user = new FactionChecks(Context);
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

            var user = new FactionChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.DetailedInfo(id), Context);
        }
    }

    [Category("fh")]
    public class FactionHangarSimpCommands : CommandModule
    {
        [Command("whitelist", "Add or remove a player from whitelist")]
        [Permission(MyPromoteLevel.None)]
        public async void Whitelist(string targetNameOrSteamId)
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new FactionChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.ChangeWhitelist(targetNameOrSteamId), Context);
        }

        [Command("whitelisted", "Output the whitelisted steam ids")]
        [Permission(MyPromoteLevel.None)]
        public async void Whitelist()
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new FactionChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.ListWhitelist(), Context);
        }
        [Command("webhook", "change the webhook")]
        [Permission(MyPromoteLevel.None)]
        public async void Webhook(string webhook)
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }

            var user = new FactionChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.ChangeWebhook(webhook), Context);
        }
        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void SaveGrid()
        {
            if (Context.Player == null)
            {
                Context.Respond("This is a player only command!");
                return;
            }
            if (CommandCooldownChecker.FailsFactionPreChecks(Context)) return;
            var user = new FactionChecks(Context);
            await HangarCommandSystem.RunTaskAsync(() => user.SaveGrid(), Context);
        }

        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public async void ListGrids()
        {
            var user = new FactionChecks(Context);
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
            if (CommandCooldownChecker.FailsFactionPreChecks(Context)) return;
            var user = new FactionChecks(Context);
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

            var user = new FactionChecks(Context);
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

            var user = new FactionChecks(Context);
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

            foreach (var grid in Grids)
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