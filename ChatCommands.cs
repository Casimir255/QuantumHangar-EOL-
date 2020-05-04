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

namespace QuantumHangar
{

    [Torch.Commands.Category("hangar")]
    public class ChatCommands : CommandModule
    {
        public const ushort NETWORK_ID = 8934;

        private Main Plugin => (Main)Context.Plugin;

        [Command("save", "Saves targeted grid in hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Save()
        {
            Parallel.Invoke(() =>
            {
                MyCharacter character = null;
                Chat chat = new Chat(Context);
                if (!Plugin.Config.PluginEnabled)
                    return;

                if (Context.Player == null)
                {
                    chat.Respond("You cant do this via console stupid!");
                    return;
                }
                var player = ((MyPlayer)Context.Player).Identity;

                if (player.Character == null)
                {
                    chat.Respond("Player has no character to spawn the grid close to!");
                    return;
                }

                if (!HangarChecks.CheckGravity(Context, Plugin))
                {
                    return;
                }


                //Check Player Hangar Limits
                if (!HangarChecks.CheckHanagarLimits(Context, Plugin, out PlayerInfo Data))
                {
                    return;
                }


                //Check Player Timer
                if (!HangarChecks.CheckPlayerTimeStamp(Context, Plugin, ref Data))
                {
                    return;
                }


                //Check Enemy Distance
                if (!HangarChecks.CheckEnemyDistance(Context, Plugin))
                {
                    return;
                }

                character = player.Character;
                Result result = HangarChecks.GetGrids(Context, Plugin, character);

                if (!result.GetGrids)
                {
                    return;
                }

                List<MyCubeGrid> grids = result.grids;
                MyCubeGrid biggestGrid = result.biggestGrid;


                if (!HangarChecks.ExtensiveLimitChecker(Context, Plugin, result, Data))
                {
                    return;
                }



                //Check for existing grids in hangar!
                if (!HangarChecks.CheckForExistingGrids(Data, result))
                {
                    chat.Respond("A grid with that name is already in your hangar! Please rename!");
                    return;
                }


                //Check for price
                if (!HangarChecks.RequireCurrency(Context, Plugin, result))
                {
                    return;
                }


                if (!HangarChecks.BeginSave(Context, Plugin, result, Data))
                {
                    return;
                }


                else
                {
                    foreach (MyCubeGrid Grids in result.grids)
                    {
                        Grids.Close();
                    }
                }

            });
        }



        [Command("load", "Loads given grid from hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Load(string gridNameOrEntityId = null)
        {
            Parallel.Invoke(() =>
            {
                Chat chat = new Chat(Context);
                if (!Plugin.Config.PluginEnabled)
                    return;

                if (Context.Player == null)
                {
                    chat.Respond("You cant do this via console stupid!");
                    return;
                }

                IMyPlayer Player = Context.Player;

                string IDPath = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, Player.SteamUserId);
                //Log.Info("Player Path: " + path);


                //FileInfo SelectedFile = null;

                MyIdentity NewPlayer = ((MyPlayer)Context.Player).Identity;

                if (NewPlayer.Character == null)
                {
                    chat.Respond("Player has no character to spawn the grid close to!");
                    return;
                }


                if (!HangarChecks.CheckGravity(Context, Plugin))
                {
                    return;
                }


                PlayerInfo Data = new PlayerInfo();
                //Get PlayerInfo from file
                try
                {
                    Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(IDPath, "PlayerInfo.json")));

                    if (Data == null || Data.Grids.Count == 0)
                    {
                        chat.Respond("You have no grids in hangar!");
                        //Cannont load grids
                        return;
                    }
                }
                catch (Exception e)
                {
                    Main.Debug("Unable to open PlayerData!", e, Main.ErrorType.Warn);
                    return;
                    //New player. Go ahead and create new. Should not have a timer.
                }

                //Check Enemy Distance
                if (!HangarChecks.CheckEnemyDistance(Context, Plugin))
                {
                    return;
                }


                int result = 0;

                try
                {
                    result = Int32.Parse(gridNameOrEntityId);
                    //Got result. Check to see if its not an absured number

                    if (result > Data.Grids.Count)
                    {
                        chat.Respond("This hangar slot is empty! Select a grid that is in your hangar!");
                        return;
                    }
                }
                catch
                {
                    //If failed cont to normal string name
                }


                if (Data.Grids != null && result != 0)
                {

                    GridStamp Grid = Data.Grids[result - 1];

                    //Check PCU!
                    if (!HangarChecks.CheckGridLimits(Context, NewPlayer, Grid))
                    {
                        return;
                    }

                    //Check to see if the grid is on the market!
                    if (!HangarChecks.CheckIfOnMarket(Context, Grid, Plugin, NewPlayer))
                    {
                        return;
                    }



                    string path = Path.Combine(IDPath, Grid.GridName + ".sbc");

                    if (!HangarChecks.LoadGrid(Context, Plugin, path, IDPath, NewPlayer, Data, Grid))
                    {
                        return;
                    }
                }


                if (gridNameOrEntityId != null || gridNameOrEntityId != "")
                {
                    //Scan containg Grids if the user typed one
                    foreach (var grid in Data.Grids)
                    {

                        if (grid.GridName == gridNameOrEntityId)
                        {
                            //Check BlockLimits
                            if (!HangarChecks.CheckGridLimits(Context, NewPlayer, grid))
                            {
                                return;
                            }

                            //Check to see if the grid is on the market!
                            if (!HangarChecks.CheckIfOnMarket(Context, grid, Plugin, NewPlayer))
                            {
                                return;
                            }

                            string path = Path.Combine(IDPath, grid.GridName + ".sbc");
                            if (!HangarChecks.LoadGrid(Context, Plugin, path, IDPath, NewPlayer, Data, grid))
                            {
                                return;
                            }

                        }
                    }
                }

            });
        }

        [Command("list", "Lists all the grids in your hangar.")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            if (!Plugin.Config.PluginEnabled)
                return;

            Chat chat = new Chat(Context);
            if (Context.Player == null)
            {
                chat.Respond("You cant do this via console stupid!");
                return;
            }
            IMyPlayer Player = Context.Player;
            string path = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, Player.SteamUserId);

            PlayerInfo Data = new PlayerInfo();
            //Get PlayerInfo from file
            try
            {
                Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(path, "PlayerInfo.json")));
            }
            catch
            {
                //New player. Go ahead and create new. Should not have a timer.

            }

            if (Data.Grids == null || Data.Grids.Count == 0)
            {
                chat.Respond("You have no grids in the hangar!");
                return;
            }

            int MaxStorage = Plugin.Config.NormalHangarAmount;
            if (Context.Player.PromoteLevel >= MyPromoteLevel.Scripter)
            {
                MaxStorage = Plugin.Config.ScripterHangarAmount;
            }


            var sb = new StringBuilder();

            sb.AppendLine("You have " + Data.Grids.Count() + "/" + MaxStorage + " stored grids:");
            int count = 1;
            foreach (var grid in Data.Grids)
            {
                sb.AppendLine(" [" + count + "] - " + grid.GridName);
                count++;
            }

            chat.Respond(sb.ToString());
        }

        [Command("delete", "Deletes grid from your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void delete()
        {
            Chat chat = new Chat(Context);
            chat.Respond("This command is underdevelopment!");
            return;
            if (Context.Player == null)
            {
                chat.Respond("You cant do this via console stupid!");
                return;
            }
            IMyPlayer Player = Context.Player;
            string path = GridMethods.CreatePathForPlayer("C:/QuantumHangar", Player.SteamUserId);

            DirectoryInfo gridDir = new DirectoryInfo(path);
            FileInfo[] dirList = gridDir.GetFiles("*", SearchOption.TopDirectoryOnly);

            int MaxStorage = 2;
            if (Context.Player.PromoteLevel >= MyPromoteLevel.Scripter)
            {
                MaxStorage = 6;
            }


            var sb = new StringBuilder();
            if (dirList.Count() != 0)
            {
                sb.AppendLine("You have " + dirList.Count() + "/" + MaxStorage + " stored grids:");
                int count = 1;
                foreach (FileInfo File in dirList)
                {
                    sb.AppendLine(" [" + count + "]-" + File.Name);
                    count++;
                }

                chat.Respond(sb.ToString());
            }
            else
            {
                chat.Respond("You have no grids in the hangar!");
            }

        }


        [Command("sell", "Sells given grid on the global market")]
        [Permission(MyPromoteLevel.None)]
        public void GridSell(string GridNameOrNumber, string price, string description)
        {
            var t = Task.Run(() => TaskedGridSell(Context, Plugin, GridNameOrNumber, price, description));
        }
        private static void TaskedGridSell(CommandContext Context, Main Plugin, string GridNameOrNumber, string price, string description)
        {
            //Put in background worker
            Chat chat = new Chat(Context);
            if (!Plugin.Config.PluginEnabled || !Plugin.Config.GridMarketEnabled)
            {
                chat.Respond("Grid Market is not enabled!");
                return;
            }


            if (Context.Player == null)
            {
                chat.Respond("You cant do this via console stupid!");
                return;
            }

            IMyPlayer Player = Context.Player;

            string IDPath = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, Player.SteamUserId);


            //FileInfo SelectedFile = null;

            var executingPlayer = ((MyPlayer)Context.Player).Identity;
            if (executingPlayer.Character == null)
            {
                chat.Respond("Player has no character to spawn the grid close to!");
                return;
            }

            PlayerInfo Data = new PlayerInfo();
            //Get PlayerInfo from file
            try
            {
                Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(IDPath, "PlayerInfo.json")));
                if (Data.Grids.Count == 0)
                {
                    chat.Respond("You have no grids in your hangar!");
                    return;
                }
            }
            catch (Exception e)
            {
                Main.Debug("Unable to load PlayerData", e, Main.ErrorType.Warn);
                return;
                //New player. Go ahead and create new. Should not have a timer.
            }

            //Check price
            long NumPrice;
            if (!Int64.TryParse(price, out NumPrice))
            {
                chat.Respond("Invalid Price format! Make sure to not include ',' ");
                return;
            }
            else
            {
                if (NumPrice <= 0)
                {
                    chat.Respond("Price needs to be a positive non-zero number!");
                }
            }


            int result = 0;
            try
            {
                result = Int32.Parse(GridNameOrNumber);

                if (result > Data.Grids.Count)
                {
                    chat.Respond("This hangar slot is empty! Select a grid that is in your hangar!");
                    return;
                }
                //Got result. Check to see if its not an absured number
            }
            catch
            {
                //If failed cont to normal string name
            }


            //If user entered a number
            if (Data.Grids != null && result != 0)
            {
                GridStamp Grid = Data.Grids[result - 1];



                if (Grid.GridForSale == true)
                {
                    chat.Respond("Selected grid is already on the market!");
                    return;
                }


                //string path = Path.Combine(IDPath, Grid.GridName + ".sbc");

                Data.Grids[result - 1].GridForSale = true;
                chat.Respond("Preparing grid for market!");


                if (!HangarChecks.SellOnMarket(Plugin, IDPath, Grid, Data, Player, NumPrice, description))
                {
                    chat.Respond("Grid doesnt exist in your hangar folder! Contact and Admin!");
                    return;
                }

                return;
            }

            //Scan containg Grids if the user typed a grid name
            foreach (var grid in Data.Grids)
            {

                if (grid.GridName == GridNameOrNumber)
                {
                    if (grid.GridForSale == true)
                    {
                        chat.Respond("Selected grid is already on the market!");
                        return;
                    }

                    grid.GridForSale = true;
                    if (!HangarChecks.SellOnMarket(Plugin, IDPath, grid, Data, Player, NumPrice, description))
                    {
                        chat.Respond("Grid doesnt exist in your hangar folder! Contact and Admin!");
                        return;
                    }

                }
            }

        }


        [Command("removeoffer", "Removes active grid from market")]
        [Permission(MyPromoteLevel.None)]
        public void RemoveOffer(string GridNameOrNumber)
        {
            Parallel.Invoke(() =>
            {
                if (!Plugin.Config.PluginEnabled)
                    return;

                Chat chat = new Chat(Context);

                if (Context.Player == null)
                {
                    chat.Respond("You cant do this via console stupid!");
                    return;
                }

                IMyPlayer Player = Context.Player;

                string IDPath = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, Player.SteamUserId);
                //Log.Info("Player Path: " + path);


                //FileInfo SelectedFile = null;

                MyIdentity NewPlayer = ((MyPlayer)Context.Player).Identity;

                if (NewPlayer.Character == null)
                {
                    chat.Respond("Player has no character to spawn the grid close to!");
                    return;
                }


                PlayerInfo Data = new PlayerInfo();
                //Get PlayerInfo from file
                try
                {
                    Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(IDPath, "PlayerInfo.json")));

                    if (Data == null || Data.Grids.Count == 0)
                    {
                        chat.Respond("You have no grids in hangar!");
                        //Cannont load grids
                        return;
                    }
                }
                catch (Exception e)
                {
                    Main.Debug("Unable to open PlayerData!", e, Main.ErrorType.Warn);
                    return;
                    //New player. Go ahead and create new. Should not have a timer.
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


                    //Check to see if the grid is on the market!
                    if (!HangarChecks.CheckIfOnMarket(Context, Grid, Plugin, NewPlayer))
                    {
                        return;
                    }
                    chat.Respond("You removed " + Grid.GridName + " from the market!");
                    FileSaver.Save(Path.Combine(IDPath, "PlayerInfo.json"), Data);
                    //File.WriteAllText(Path.Combine(IDPath, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
                }


                if (GridNameOrNumber != null || GridNameOrNumber != "")
                {
                    //Scan containg Grids if the user typed one
                    foreach (var grid in Data.Grids)
                    {

                        if (grid.GridName == GridNameOrNumber)
                        {

                            //Check to see if the grid is on the market!
                            if (!HangarChecks.CheckIfOnMarket(Context, grid, Plugin, NewPlayer))
                            {
                                return;
                            }

                            chat.Respond("You removed " + grid.GridName + " from the market!");
                            FileSaver.Save(Path.Combine(IDPath, "PlayerInfo.json"), Data);
                            //File.WriteAllText(Path.Combine(IDPath, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
                        }
                    }
                }


            });
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
                    Main.Debug("Mod:" + def.Context.ModId);

                    foreach (var guid in def.RegisteredStorageGuids)
                    {
                        Main.Debug("GUIDs:" + guid);
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


                EconUtils.TryUpdatePlayerBalance(new PlayerAccount(name, steamid, amount));


                //MyPlayer.PlayerId Player = new MyPlayer.PlayerId(Context.Player.SteamUserId);
                //MySession.Static.Players.TryGetPlayerById(Player, out MyPlayer NewPlayer);
                Context.Respond("Player " + name + " balance has been updated!");

            }
            catch (Exception e)
            {
                Context.Respond("Error " + e);
                Main.Debug("Err", e, Main.ErrorType.Trace);
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
                EconUtils.TryGetPlayerBalance(steamid, out long checkmoney);


                //MyPlayer.PlayerId Player = new MyPlayer.PlayerId(Context.Player.SteamUserId);
                //MySession.Static.Players.TryGetPlayerById(Player, out MyPlayer NewPlayer);
                Context.Respond("Player " + name + " has a balance of: " + checkmoney + "sc");

            }
            catch (Exception e)
            {
                Context.Respond("Error " + e);
                Main.Debug("Err", e, Main.ErrorType.Trace);
            }

            //account.TryGetAccountInfo(Context.Player.IdentityId, out Sandbox.Game.GameSystems.BankingAndCurrency.MyAccountInfo myAccountInfo);

            //myAccountInfo.Balance
        }



        [Command("info", "Provides information about the ship in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void HangarDetails(string GridNameOrNumber)
        {
            Parallel.Invoke(() =>
            {
                if (!Plugin.Config.PluginEnabled)
                    return;

                Chat chat = new Chat(Context);
                if (Context.Player == null)
                {
                    chat.Respond("You cant do this via console stupid!");
                    return;
                }

                IMyPlayer Player = Context.Player;

                string IDPath = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, Player.SteamUserId);
                //Log.Info("Player Path: " + path);


                //FileInfo SelectedFile = null;

                MyIdentity NewPlayer = ((MyPlayer)Context.Player).Identity;

                if (NewPlayer.Character == null)
                {
                    chat.Respond("Player has no character to spawn the grid close to!");
                    return;
                }


                PlayerInfo Data = new PlayerInfo();
                //Get PlayerInfo from file
                try
                {
                    Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(IDPath, "PlayerInfo.json")));

                    if (Data == null || Data.Grids.Count == 0)
                    {
                        chat.Respond("You have no grids in hangar!");
                        //Cannont load grids
                        return;
                    }
                }
                catch (Exception e)
                {
                    Main.Debug("Unable to open PlayerData!", e, Main.ErrorType.Warn);
                    return;
                    //New player. Go ahead and create new. Should not have a timer.
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


                    ModCommunication.SendMessageTo(new DialogMessage(Grid.GridName, $"Ship Information", stringBuilder.ToString()), Player.SteamUserId);


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


                            ModCommunication.SendMessageTo(new DialogMessage(grid.GridName, $"Ship Information", stringBuilder.ToString()), Player.SteamUserId);
                            // Context.Respond("You removed " + grid.GridName + " from the market!");
                        }
                    }
                }


            });
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

        private Main Plugin => (Main)Context.Plugin;

        [Command("save", "Saves targeted grid in hangar ignoring limits")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void AdminSave(string gridName = null)
        {
            if (!Plugin.Config.PluginEnabled)
                return;

            Chat chat = new Chat(Context);
            MyCharacter character = null;
            Result result = new Result();
            //MyCubeGrid grid;
            if (gridName == null)
            {
                if (Context.Player == null)
                {
                    chat.Respond("You cant do this via console stupid!");
                    return;
                }
                var player = ((MyPlayer)Context.Player).Identity;

                if (player.Character == null)
                {
                    chat.Respond("Player has no character to spawn the grid close to!");
                    return;
                }

                character = player.Character;
                Main.Debug("Admin is runing this in-game! Getting grid looked at!");

                result = HangarChecks.AdminGetGrids(Context, Plugin, character);
                if (!result.GetGrids)
                {
                    return;
                }
            }
            else
            {
                Main.Debug("Admin is running this in console!");
                result = HangarChecks.AdminGetGrids(Context, Plugin, character, gridName);
                if (!result.GetGrids)
                {
                    return;
                }
            }


            ulong id = MySession.Static.Players.TryGetSteamId(result.biggestGrid.BigOwners[0]);
            if (id == 0)
            {
                chat.Respond("Unable to find grid owners steamID! Perhaps they were purged from the server?");
                return;
            }


            IMyPlayer Player = Context.Player;
            string path = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, id);
            PlayerInfo Data = new PlayerInfo();
            //Get PlayerInfo from file
            try
            {
                Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(path, "PlayerInfo.json")));
            }
            catch
            {
                //New player. Go ahead and create new. Should not have a timer.
            }


            if (!HangarChecks.CheckForExistingGrids(Data, result))
            {
                chat.Respond("A grid with that name is already in this persons hangar! Please rename!");
                return;
            }


            if (GridMethods.BackupSignleGridStatic(Plugin.Config.FolderDirectory, id, result.grids, null, false))
            {
                chat.Respond("Save Complete!");

                //Load player file and update!

                int MaxPCU = 0;
                foreach (var a in result.grids)
                {
                    MaxPCU = MaxPCU + a.BlocksPCU;
                }


                //Fill out grid info and store in file
                HangarChecks.GetBPDetails(result, Plugin.Config, out GridStamp Grid);

                Data.Grids.Add(Grid);

                //Deleteall grids
                foreach (MyCubeGrid singlegrid in result.grids)
                {
                    singlegrid.Delete();
                }

                //Overwrite file
                FileSaver.Save(Path.Combine(path, "PlayerInfo.json"), Data);
                //File.WriteAllText(Path.Combine(path, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
            }
            else
                chat.Respond("Export Failed!");

        }

        [Command("AutoHangar", "Runs AutoHangar")]
        [Permission(MyPromoteLevel.Admin)]
        public void RunAuto()
        {
            Main.Debug("Attempting Autohangar!");
            //Stamp.AddHours(.5);
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(HangarScans.AutoHangar);
            worker.RunWorkerAsync(Plugin.Config);
        }

        [Command("load", "loads targeted grid from hangar ignoring limits")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void AdminLoad(string NameOrID, string gridNameOrEntityId)
        {
            if (!Plugin.Config.PluginEnabled)
                return;

            Chat chat = new Chat(Context);
            if (Context.Player == null)
            {
                chat.Respond("You cant do this via console stupid!");
                return;
            }

            MyIdentity NewPlayer = ((MyPlayer)Context.Player).Identity;

            MyIdentity MPlayer = MySession.Static.Players.GetAllIdentities().First(x => x.DisplayName == NameOrID);
            ulong SteamID = MySession.Static.Players.TryGetSteamId(MPlayer.IdentityId);

            string IDPath = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, SteamID);
            //Log.Info("Player Path: " + path);


            //FileInfo SelectedFile = null;

            var executingPlayer = ((MyPlayer)Context.Player).Identity;
            if (executingPlayer.Character == null)
            {
                chat.Respond("Player has no character to spawn the grid close to!");
                return;
            }

            PlayerInfo Data = new PlayerInfo();
            //Get PlayerInfo from file
            try
            {
                Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(IDPath, "PlayerInfo.json")));
            }
            catch
            {
                //New player. Go ahead and create new. Should not have a timer.

            }



            var playerPosition = Vector3D.Zero;
            playerPosition = executingPlayer.Character.PositionComp.GetPosition();

            int result = 0;

            try
            {
                result = Int32.Parse(gridNameOrEntityId);
                //Got result. Check to see if its not an absured number
            }
            catch
            {
                //If failed cont to normal string name
            }

            if (Data.Grids != null && result != 0 && result <= Data.Grids.Count)
            {
                GridStamp Grid = Data.Grids[result - 1];

                string path = Path.Combine(IDPath, Grid.GridName + ".sbc");

                if (!HangarChecks.LoadGrid(Context, Plugin, path, IDPath, NewPlayer, Data, Grid))
                {
                    return;
                }

                return;
            }



            foreach (var grid in Data.Grids)
            {
                if (grid.GridName == gridNameOrEntityId)
                {
                    string path = Path.Combine(IDPath, grid.GridName + ".sbc");
                    if (!HangarChecks.LoadGrid(Context, Plugin, path, IDPath, NewPlayer, Data, grid))
                    {
                        return;
                    }
                }
            }





        }


        [Command("list", "Lists all the grids in someones hangar.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void List(string NameOrID)
        {
            if (!Plugin.Config.PluginEnabled)
                return;
            Chat chat = new Chat(Context);
            MyIdentity MPlayer;
            try
            {
                MPlayer = MySession.Static.Players.GetAllIdentities().First(x => x.DisplayName == NameOrID);
            }
            catch (Exception e)
            {
                Main.Debug("Player dosnt exist on the server!", e, Main.ErrorType.Warn);
                chat.Respond("Player doesnt exist!");
                return;
            }
            ulong SteamID = MySession.Static.Players.TryGetSteamId(MPlayer.IdentityId);


            string path = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, SteamID);

            PlayerInfo Data = new PlayerInfo();
            //Get PlayerInfo from file
            try
            {
                Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(path, "PlayerInfo.json")));
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
        [Permission(MyPromoteLevel.None)]
        public void HangarDetails(string NameOrID, string GridNameOrNumber)
        {
            Parallel.Invoke(() =>
            {


                if (!Plugin.Config.PluginEnabled)
                    return;

                Chat chat = new Chat(Context);
                MyIdentity MPlayer = MySession.Static.Players.GetAllIdentities().First(x => x.DisplayName == NameOrID);
                ulong SteamID = MySession.Static.Players.TryGetSteamId(MPlayer.IdentityId);

                IMyPlayer Player = Context.Player;
                string path = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, SteamID);

                PlayerInfo Data = new PlayerInfo();
                //Get PlayerInfo from file
                try
                {
                    Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(path, "PlayerInfo.json")));
                }
                catch (Exception e)
                {
                    //New player. Go ahead and create new. Should not have a timer.
                    Main.Debug("Unable to open PlayerData!", e, Main.ErrorType.Warn);
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
        [Permission(MyPromoteLevel.None)]
        public void RemoveOffer(string GridNameOrOfferNumber)
        {
            return;
            Parallel.Invoke(() =>
            {
                if (!Plugin.Config.PluginEnabled)
                    return;



                IMyPlayer Player = Context.Player;

                string IDPath = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, Player.SteamUserId);
                //Log.Info("Player Path: " + path);


                //FileInfo SelectedFile = null;

                MyIdentity NewPlayer = ((MyPlayer)Context.Player).Identity;

                if (NewPlayer.Character == null)
                {
                    Context.Respond("Player has no character to spawn the grid close to!");
                    return;
                }


                PlayerInfo Data = new PlayerInfo();
                //Get PlayerInfo from file
                try
                {
                    Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(IDPath, "PlayerInfo.json")));

                    if (Data == null || Data.Grids.Count == 0)
                    {
                        Context.Respond("You have no grids in hangar!");
                        //Cannont load grids
                        return;
                    }
                }
                catch (Exception e)
                {
                    Main.Debug("Unable to open PlayerData!", e, Main.ErrorType.Warn);
                    return;
                    //New player. Go ahead and create new. Should not have a timer.
                }


                int result = 0;

                try
                {
                    result = Int32.Parse(GridNameOrOfferNumber);
                    //Got result. Check to see if its not an absured number
                }
                catch
                {
                    //If failed cont to normal string name
                }


                if (Data.Grids != null && result != 0 && result <= Data.Grids.Count)
                {

                    GridStamp Grid = Data.Grids[result - 1];


                    //Check to see if the grid is on the market!
                    if (!HangarChecks.CheckIfOnMarket(Context, Grid, Plugin, NewPlayer))
                    {
                        return;
                    }
                    Context.Respond("You removed " + Grid.GridName + " from the market!");
                }


                if (GridNameOrOfferNumber != null || GridNameOrOfferNumber != "")
                {
                    //Scan containg Grids if the user typed one
                    foreach (var grid in Data.Grids)
                    {

                        if (grid.GridName == GridNameOrOfferNumber)
                        {

                            //Check to see if the grid is on the market!
                            if (!HangarChecks.CheckIfOnMarket(Context, grid, Plugin, NewPlayer))
                            {
                                return;
                            }

                            Context.Respond("You removed " + grid.GridName + " from the market!");
                        }
                    }
                }


            });
        }
    }


}
