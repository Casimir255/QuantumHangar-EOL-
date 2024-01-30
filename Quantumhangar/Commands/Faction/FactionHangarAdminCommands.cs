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
using VRageMath;

namespace QuantumHangar.Commands
{
    [Category("factionhangermod")]
    public class FactionHangarAdminCommands : CommandModule
    {
        [Command("whitelist", "Add or remove a player from whitelist")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Whitelist(string tag, string targetNameOrSteamId)
        {
            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.ChangeWhitelist(targetNameOrSteamId));
        }
        [Command("whitelisted", "Output the whitelisted steam ids")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Whitelist(string tag)
        {
            

            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.ListWhitelist());
        }
        [Command("webhook", "Change the webhook")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Webhook(string tag, string webhook)
        {
            

            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.ChangeWebhook(webhook));
        }
        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public async void SaveGrid(string tag, string gridname)
        {
            


            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.SaveGrid(gridname));
        }

        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public async void ListGrids(string tag)
        {
            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.ListGrids());
        }

        [Command("load", "Loads the specified grid by index number")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Load(string tag, string id, bool loadNearPlayer = false)
        {
            

            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.LoadGrid(id, loadNearPlayer));
        }

        [Command("remove", "removes the grid from your hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Remove(string tag, string id)
        {
            

            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.RemoveGrid(id));
        }


        [Command("info", "Provides some info of the current grid in your hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Info(string tag, string id = "")
        {
            

            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.DetailedInfo(id));
        }
    }

    [Category("fhm")]
    public class FactionHangarAdminCommandsAlias : CommandModule
    {
        [Command("whitelist", "Add or remove a player from whitelist")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Whitelist(string tag, string targetNameOrSteamId)
        {
            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.ChangeWhitelist(targetNameOrSteamId));
        }
        [Command("whitelisted", "Output the whitelisted steam ids")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Whitelist(string tag)
        {
            

            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.ListWhitelist());
        }
        [Command("webhook", "Change the webhook")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Webhook(string tag, string webhook)
        {
            

            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.ChangeWebhook(webhook));
        }
        [Command("save", "Saves the grid you are looking at to hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public async void SaveGrid(string gridname)
        {
            


            var user = new FactionAdminChecks(Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.SaveGrid(gridname));
        }

        [Command("list", "Lists all the grids saved in your hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public async void ListGrids(string tag)
        {
            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.ListGrids());
        }

        [Command("load", "Loads the specified grid by index number")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Load(string tag, string id, bool loadNearPlayer = false)
        {
            

            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.LoadGrid(id, loadNearPlayer));
        }

        [Command("remove", "removes the grid from your hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Remove(string tag, string id)
        {
            

            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.RemoveGrid(id));
        }


        [Command("info", "Provides some info of the current grid in your hangar")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Info(string tag, string id = "")
        {
            

            var user = new FactionAdminChecks(tag, Context);
            await HangarCommandSystem.RunAdminTaskAsync(() => user.DetailedInfo(id));
        }

    }
}