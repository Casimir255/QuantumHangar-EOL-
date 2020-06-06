using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRageMath;
using System.IO;
using NLog;
using Newtonsoft.Json;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Groups;
using Sandbox.Definitions;
using VRage.Game.Definitions;
using VRage.Game.ObjectBuilders.ComponentSystem;
using BankingAndCurrency = Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.GameSystems;
using System.ComponentModel;
using Sandbox.Game.Multiplayer;
using QuantumHangar.Utilities;

namespace QuantumHangar
{

    [Torch.Commands.Category("hangar")]
    public class ChatCommands : CommandModule
    {
        public const ushort NETWORK_ID = 8934;

        private Hangar Plugin => (Hangar)Context.Plugin;

        [Command("save", "Saves targeted grid in hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Save()
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Checks.SaveGrid();
        }

        [Command("load", "Loads given grid from hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Load(string GridNameOrNumber, bool LoadAtSavePosition = false)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Checks.LoadGrid(GridNameOrNumber, LoadAtSavePosition);

        }

        [Command("list", "Lists all the grids in your hangar.")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Checks.ListGrids();
        }

        [Command("delete", "Deletes grid from your hangar. Enter 0 to delete all grids")]
        [Permission(MyPromoteLevel.None)]
        public void delete(string GridNameOrNumber)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Checks.DeleteGrid(GridNameOrNumber);
        }

        [Command("sell", "Sells given grid on the global market")]
        [Permission(MyPromoteLevel.None)]
        public void GridSell(string GridNameOrNumber, string Price, string Description)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Checks.SellGrid(GridNameOrNumber, Price, Description);
        }



        [Command("removeoffer", "Removes selected grid from the market")]
        [Permission(MyPromoteLevel.None)]
        public void RemoveOffer(string GridNameOrNumber)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Checks.RemoveOffer(GridNameOrNumber);
        }


        [Command("storagecomp", "Provides information about the ModStorageComponents")]
        [Permission(MyPromoteLevel.Admin)]
        public void ModStorageComponents()
        {
            var storageDefs = MyDefinitionManager.Static.GetEntityComponentDefinitions<MyModStorageComponentDefinition>();

            Parallel.Invoke(() =>
            {
                foreach (MyModStorageComponentDefinition def in storageDefs)
                {
                    Hangar.Debug("Mod:" + def.Context.ModId);

                    foreach (var guid in def.RegisteredStorageGuids)
                    {
                        Hangar.Debug("GUIDs:" + guid);
                    }
                }

            });
        }


        [Command("setmoney", "Changes the balance of any player.")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetMoney(string name, int amount)
        {
            try
            {

                //MyPlayer.PlayerId Player = new MyPlayer.PlayerId(Context.Player.SteamUserId);

                //MySession.Static.Players.TryGetPlayerById(Player, out MyPlayer NewPlayer);

                //Context.Respond("New Player: " + NewPlayer.DisplayName);



                MyIdentity player = MySession.Static.Players.GetAllIdentities().First(x => x.DisplayName == name);
                ulong steamid = MySession.Static.Players.TryGetSteamId(player.IdentityId);
                /*

                BankingAndCurrency.MyBankingSystem.RequestBalanceChange(player.IdentityId, amount);
                //Sandbox.Game.GameSystems.BankingAndCurrency.MyBankingSystem account = new Sandbox.Game.GameSystems.BankingAndCurrency.MyBankingSystem();
                Context.Respond(BankingAndCurrency.MyBankingSystem.GetBalance(player.IdentityId).ToString());
                Log.Info(BankingAndCurrency.MyBankingSystem.GetBalance(player.IdentityId).ToString());
                Main.Debug(BankingAndCurrency.MyBankingSystem.GetBalance(player.IdentityId).ToString());
                */
                //KeyValuePair<ulong, long> account = new KeyValuePair<ulong, long>(steamid, amount);


                Utilis.TryUpdatePlayerBalance(new PlayerAccount(name, steamid, amount));


                //MyPlayer.PlayerId Player = new MyPlayer.PlayerId(Context.Player.SteamUserId);
                //MySession.Static.Players.TryGetPlayerById(Player, out MyPlayer NewPlayer);
                Context.Respond("Player " + name + " balance has been updated!");

            }
            catch (Exception e)
            {
                Context.Respond("Error " + e);
                Hangar.Debug("Err", e, Hangar.ErrorType.Trace);
            }

            //account.TryGetAccountInfo(Context.Player.IdentityId, out Sandbox.Game.GameSystems.BankingAndCurrency.MyAccountInfo myAccountInfo);

            //myAccountInfo.Balance
        }


        [Command("checkmoney", "Changes the balance of any player.")]
        [Permission(MyPromoteLevel.Admin)]
        public void checkmoney(string name)
        {
            try
            {

                //MyPlayer.PlayerId Player = new MyPlayer.PlayerId(Context.Player.SteamUserId);

                //MySession.Static.Players.TryGetPlayerById(Player, out MyPlayer NewPlayer);

                //Context.Respond("New Player: " + NewPlayer.DisplayName);



                MyIdentity player = MySession.Static.Players.GetAllIdentities().First(x => x.DisplayName == name);
                ulong steamid = MySession.Static.Players.TryGetSteamId(player.IdentityId);
                /*

                BankingAndCurrency.MyBankingSystem.RequestBalanceChange(player.IdentityId, amount);
                //Sandbox.Game.GameSystems.BankingAndCurrency.MyBankingSystem account = new Sandbox.Game.GameSystems.BankingAndCurrency.MyBankingSystem();
                Context.Respond(BankingAndCurrency.MyBankingSystem.GetBalance(player.IdentityId).ToString());
                Log.Info(BankingAndCurrency.MyBankingSystem.GetBalance(player.IdentityId).ToString());
                Main.Debug(BankingAndCurrency.MyBankingSystem.GetBalance(player.IdentityId).ToString());
                */

                Context.Respond("SteamID: " + steamid);
                Utilis.TryGetPlayerBalance(steamid, out long checkmoney);


                //MyPlayer.PlayerId Player = new MyPlayer.PlayerId(Context.Player.SteamUserId);
                //MySession.Static.Players.TryGetPlayerById(Player, out MyPlayer NewPlayer);
                Context.Respond("Player " + name + " has a balance of: " + checkmoney + "sc");

            }
            catch (Exception e)
            {
                Context.Respond("Error " + e);
                Hangar.Debug("Err", e, Hangar.ErrorType.Trace);
            }

            //account.TryGetAccountInfo(Context.Player.IdentityId, out Sandbox.Game.GameSystems.BankingAndCurrency.MyAccountInfo myAccountInfo);

            //myAccountInfo.Balance
        }



















        [Command("info", "Provides information about the ship in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void HangarDetails(string GridNameOrNumber)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Checks.HangarInfo(GridNameOrNumber);
        }


        [Command("help", "Gives basic instructions on various hangar commands")]
        [Permission(MyPromoteLevel.None)]
        public void HangarHelp()
        {
            Chat chat = new Chat(Context);
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("!hangar save (Saves the grid you are looking at)");
            stringBuilder.AppendLine("!hangar list (Lists all the grids in your hangar with their number)");
            stringBuilder.AppendLine("!hangar load [NameOrNumber] (Loads the specified grid from hangar)");
            stringBuilder.AppendLine("!hangar sell [NameOrNumber] [Price] [\"Description\"] (Put grid up for sale)");
            stringBuilder.AppendLine("!hangar info [NameOrNumber] (Provides info about the grid in your hangar)");
            stringBuilder.AppendLine("!hangar removeoffer [NameOrNumber] (Removes a ship form the market)");


            chat.Respond(stringBuilder.ToString());
        }

    }








    [Torch.Commands.Category("hangarmod")]
    public class AdminCommands : CommandModule
    {

        private Hangar Plugin => (Hangar)Context.Plugin;

        [Command("save", "Saves targeted grid in hangar ignoring limits")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void AdminSave(string GridName = null)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin, true);
            Checks.AdminSaveGrid(GridName);

        }

        [Command("load", "loads targeted grid from hangar ignoring limits")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void AdminLoad(string NameOrID, string GridNameOrNumber)
        {

            HangarChecks Checks = new HangarChecks(Context, Plugin, true);
            Checks.AdminLoadGrid(NameOrID, GridNameOrNumber);


        }



        [Command("AutoHangar", "Runs AutoHangar based off of configs")]
        [Permission(MyPromoteLevel.Admin)]
        public void RunAuto()
        {
            AutoHangar autoHangar = new AutoHangar(Plugin.Config, Plugin.Market, Plugin.Tracker);
            autoHangar.RunAutoHangar();
        }




        [Command("list", "Lists all the grids in someones hangar.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void List(string NameOrID)
        {
            if (!Plugin.Config.PluginEnabled)
                return;
            Chat chat = new Chat(Context, true);
            MyIdentity MPlayer;
            try
            {
                MPlayer = MySession.Static.Players.GetAllIdentities().First(x => x.DisplayName == NameOrID);
            }
            catch (Exception e)
            {
                Hangar.Debug("Player dosnt exist on the server!", e, Hangar.ErrorType.Warn);
                chat.Respond("Player doesnt exist!");
                return;
            }
            ulong SteamID = MySession.Static.Players.TryGetSteamId(MPlayer.IdentityId);

            GridMethods methods = new GridMethods(SteamID, Plugin.Config.FolderDirectory);

            PlayerInfo Data = new PlayerInfo();
            //Get PlayerInfo from file
            try
            {
                Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(methods.FolderPath, "PlayerInfo.json")));
            }
            catch
            {
                //New player. Go ahead and create new. Should not have a timer.

            }

            if (Data.Grids == null || Data.Grids.Count == 0)
            {
                chat.Respond("There are no grids in the hangar!");
                return;
            }

            //Comment this out for now
            MyPromoteLevel level = MySession.Static.PromotedUsers[SteamID];
            int MaxStorage = Plugin.Config.NormalHangarAmount;
            if (level >= MyPromoteLevel.Scripter)
            {
                MaxStorage = Plugin.Config.ScripterHangarAmount;
            }


            var sb = new StringBuilder();

            sb.AppendLine("Player has " + Data.Grids.Count() + "/" + MaxStorage + " stored grids:");
            //sb.AppendLine("Player has " + Data.Grids.Count() +" stored grids:");
            int count = 1;
            foreach (var grid in Data.Grids)
            {
                sb.AppendLine(" [" + count + "] - " + grid.GridName);
                count++;
            }

            chat.Respond(sb.ToString());
        }

        [Command("info", "Provides information about the ship in your hangar")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void HangarDetails(string NameOrID, string GridNameOrNumber)
        {
            Parallel.Invoke(() =>
            {


                if (!Plugin.Config.PluginEnabled)
                    return;

                Chat chat = new Chat(Context, true);
                MyIdentity MPlayer = MySession.Static.Players.GetAllIdentities().First(x => x.DisplayName == NameOrID);
                ulong SteamID = MySession.Static.Players.TryGetSteamId(MPlayer.IdentityId);

                IMyPlayer Player = Context.Player;

                GridMethods methods = new GridMethods(SteamID, Plugin.Config.FolderDirectory);


                PlayerInfo Data = new PlayerInfo();
                //Get PlayerInfo from file
                try
                {
                    Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(methods.FolderPath, "PlayerInfo.json")));
                }
                catch (Exception e)
                {
                    //New player. Go ahead and create new. Should not have a timer.
                    Hangar.Debug("Unable to open PlayerData!", e, Hangar.ErrorType.Warn);
                    return;
                }


                int result = 0;

                try
                {
                    result = Int32.Parse(GridNameOrNumber);
                    //Got result. Check to see if its not an absured number
                }
                catch
                {
                    //If failed cont to normal string name
                }


                if (Data.Grids != null && result != 0 && result <= Data.Grids.Count)
                {

                    GridStamp Grid = Data.Grids[result - 1];


                    StringBuilder stringBuilder = new StringBuilder();

                    stringBuilder.AppendLine("__________•°•[ Ship Properties ]•°•__________");
                    stringBuilder.AppendLine("Estimated Market Value: " + Grid.MarketValue + " [sc]");
                    stringBuilder.AppendLine("GridMass: " + Grid.GridMass + "kg");
                    stringBuilder.AppendLine("Num of Small Grids: " + Grid.SmallGrids);
                    stringBuilder.AppendLine("Num of Large Grids: " + Grid.LargeGrids);
                    stringBuilder.AppendLine("Max Power Output: " + Grid.MaxPowerOutput);
                    stringBuilder.AppendLine("Build Percentage: " + Math.Round(Grid.GridBuiltPercent * 100, 2) + "%");
                    stringBuilder.AppendLine("Max Jump Distance: " + Grid.JumpDistance);
                    stringBuilder.AppendLine("Ship PCU: " + Grid.GridPCU);
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine();

                    stringBuilder.AppendLine("__________•°•[ Block Count ]•°•__________");
                    foreach (KeyValuePair<string, int> pair in Grid.BlockTypeCount)
                    {
                        stringBuilder.AppendLine(pair.Key + ": " + pair.Value);
                    }


                    if (Context.Player != null)
                    {
                        ModCommunication.SendMessageTo(new DialogMessage(Grid.GridName, $"Ship Information", stringBuilder.ToString()), Player.SteamUserId);
                    }
                    else
                    {
                        chat.Respond(stringBuilder.ToString());
                    }


                    //Context.Respond("You removed " + Grid.GridName + " from the market!");
                }


                if (GridNameOrNumber != null || GridNameOrNumber != "")
                {
                    //Scan containg Grids if the user typed one
                    foreach (var grid in Data.Grids)
                    {

                        if (grid.GridName == GridNameOrNumber)
                        {

                            StringBuilder stringBuilder = new StringBuilder();

                            stringBuilder.AppendLine("__________•°•[ Ship Properties ]•°•__________");
                            stringBuilder.AppendLine("Estimated Market Value: " + grid.MarketValue + " [sc]");
                            stringBuilder.AppendLine("GridMass: " + grid.GridMass + "kg");
                            stringBuilder.AppendLine("Num of Small Grids: " + grid.SmallGrids);
                            stringBuilder.AppendLine("Num of Large Grids: " + grid.LargeGrids);
                            stringBuilder.AppendLine("Max Power Output: " + grid.MaxPowerOutput);
                            stringBuilder.AppendLine("Build Percentage: " + Math.Round(grid.GridBuiltPercent * 100, 2) + "%");
                            stringBuilder.AppendLine("Max Jump Distance: " + grid.JumpDistance);
                            stringBuilder.AppendLine("Ship PCU: " + grid.GridPCU);
                            stringBuilder.AppendLine();
                            stringBuilder.AppendLine();

                            stringBuilder.AppendLine("__________•°•[ Block Count ]•°•__________");
                            foreach (KeyValuePair<string, int> pair in grid.BlockTypeCount)
                            {
                                stringBuilder.AppendLine(pair.Key + ": " + pair.Value);
                            }


                            if (Context.Player != null)
                            {
                                ModCommunication.SendMessageTo(new DialogMessage(grid.GridName, $"Ship Information", stringBuilder.ToString()), Player.SteamUserId);
                            }
                            else
                            {
                                chat.Respond(stringBuilder.ToString());
                            }
                            // Context.Respond("You removed " + grid.GridName + " from the market!");
                        }
                    }
                }


            });
        }

        [Command("removeoffer", "Removes active grid from market")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void RemoveOffer(string GridNameOrOfferNumber)
        {
            return;
        }

        [Command("forceupdate", "updates all players hangar folders. (you can remove files)")]
        [Permission(MyPromoteLevel.Admin)]
        public void ForceUpdate(bool FixMarket = false)
        {
            //Context.Respond("Updates Hangars");
            //var p = Task.Run(() => HangarScans.HangarReset(Plugin.Config.FolderDirectory, FixMarket));
        }




        [Command("SaveAll", "Saves Everygrid in the server to players hangars")]
        [Permission(MyPromoteLevel.Admin)]
        public void SaveAll()
        {
            AutoHangar autoHangar = new AutoHangar(Plugin.Config, Plugin.Market, Plugin.Tracker);
            autoHangar.SaveAll();
        }

    }


}
