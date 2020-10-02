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
    [Torch.Commands.Category("hanger")]
    public class FunnyChatCommands : CommandModule
    {
        [Command("save", "Doesnt save targeted grid in hangar")]
        [Permission(MyPromoteLevel.None)]
        public void CheekySave()
        {
            if (Context.Player == null)
                return;

            StringBuilder S = new StringBuilder();
            S.AppendLine(string.Format("I'm suprised you can even build a ship when you dont know the difference between Hanger & Hangar, {0}!", Context.Player.DisplayName));
            S.AppendLine("Hangeeeer Save denied! Because its !hangar not !hanger boi");
            Context.Respond(S.ToString(), Color.Yellow, "R");
        }

        [Command("load", "Doesnt load targeted grid in hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Cheekyload()
        {
            if (Context.Player == null)
                return;

            StringBuilder S = new StringBuilder();
            S.AppendLine(string.Format("How did you manage to even save a grid if you STILL cant figure out the difference between Hanger & Hangar, {0}!", Context.Player.DisplayName));
            S.AppendLine("Hangeeeer Load denied! Go fly into the sun!");
            Context.Respond(S.ToString(), Color.Yellow, "R");
        }
    }


    [Torch.Commands.Category("hangar")]
    public class ChatCommands : CommandModule
    {
        public const ushort NETWORK_ID = 8934;

        private Hangar Plugin => (Hangar)Context.Plugin;

        [Command("save", "Saves targeted grid in hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Save()
        {
            Commands.SaveGrid(Plugin, Context);

        }

        [Command("load", "Loads given grid from hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Load(string GridNameOrNumber, bool LoadAtSavePosition = false)
        {
            Commands.LoadGrid(Plugin, Context, GridNameOrNumber, LoadAtSavePosition);
        }

        [Command("list", "Lists all the grids in your hangar.")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            Commands.List(Plugin, Context);
        }

        [Command("remove", "Remove grid from your hangar. Enter 0 to delete all grids")]
        [Permission(MyPromoteLevel.None)]
        public void delete(string GridNameOrNumber)
        {
            Commands.Delete(Plugin, Context, GridNameOrNumber);
        }

        [Command("sell", "Sells given grid on the global market")]
        [Permission(MyPromoteLevel.None)]
        public void GridSell(string GridNameOrNumber, string Price, string Description)
        {
            Commands.Sell(Plugin, Context, GridNameOrNumber, Price, Description);
        }



        [Command("removeoffer", "Removes selected grid from the market")]
        [Permission(MyPromoteLevel.None)]
        public void RemoveOffer(string GridNameOrNumber)
        {
            Commands.RemoveOffer(Plugin, Context, GridNameOrNumber);
        }



        [Command("info", "Provides information about the ship in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void HangarDetails(string GridNameOrNumber)
        {
            Commands.HangarDetails(Plugin, Context, GridNameOrNumber);
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

    [Torch.Commands.Category("h")]
    public class SimpChatCommands : CommandModule
    {
        public const ushort NETWORK_ID = 8934;

        private Hangar Plugin => (Hangar)Context.Plugin;

        [Command("save", "Saves targeted grid in hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Save()
        {
            Commands.SaveGrid(Plugin, Context);
        }

        [Command("load", "Loads given grid from hangar")]
        [Permission(MyPromoteLevel.None)]
        public void Load(string GridNameOrNumber, bool LoadAtSavePosition = false)
        {
            Commands.LoadGrid(Plugin, Context, GridNameOrNumber, LoadAtSavePosition);
        }

        [Command("list", "Lists all the grids in your hangar.")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            Commands.List(Plugin, Context);
        }

        [Command("remove", "Remove grid from your hangar. Enter 0 to delete all grids")]
        [Permission(MyPromoteLevel.None)]
        public void delete(string GridNameOrNumber)
        {
            Commands.Delete(Plugin, Context, GridNameOrNumber);
        }

        [Command("sell", "Sells given grid on the global market")]
        [Permission(MyPromoteLevel.None)]
        public void GridSell(string GridNameOrNumber, string Price, string Description)
        {
            Commands.Sell(Plugin, Context, GridNameOrNumber, Price, Description);
        }

        [Command("removeoffer", "Removes selected grid from the market")]
        [Permission(MyPromoteLevel.None)]
        public void RemoveOffer(string GridNameOrNumber)
        {
            Commands.RemoveOffer(Plugin, Context, GridNameOrNumber);
        }


        [Command("info", "Provides information about the ship in your hangar")]
        [Permission(MyPromoteLevel.None)]
        public void HangarDetails(string GridNameOrNumber)
        {
            Commands.HangarDetails(Plugin, Context, GridNameOrNumber);
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


    public static class Commands
    {

        //Non Admin commands
        public static void SaveGrid(Hangar Plugin, CommandContext Context)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Task T = new Task(() => Checks.SaveGrid());
            T.Start();
        }

        public static void LoadGrid(Hangar Plugin, CommandContext Context, string GridNameOrNumber, bool LoadAtSavePosition = false)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Task T = new Task(() => Checks.LoadGrid(GridNameOrNumber, LoadAtSavePosition));
            T.Start();
        }

        public static void List(Hangar Plugin, CommandContext Context)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Task T = new Task(() => Checks.ListGrids());
            T.Start();
        }

        public static void Delete(Hangar Plugin, CommandContext Context, string GridNameOrNumber)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Task T = new Task(() => Checks.DeleteGrid(GridNameOrNumber));
            T.Start();
        }

        public static void Sell(Hangar Plugin, CommandContext Context, string GridNameOrNumber, string Price, string Description)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Task T = new Task(() => Checks.SellGrid(GridNameOrNumber, Price, Description));
            T.Start();
        }

        public static void RemoveOffer(Hangar Plugin, CommandContext Context, string GridNameOrNumber)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Task T = new Task(() => Checks.RemoveOffer(GridNameOrNumber));
            T.Start();
        }

        public static void HangarDetails(Hangar Plugin, CommandContext Context, string GridNameOrNumber)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin);
            Task T = new Task(() => Checks.HangarInfo(GridNameOrNumber));
            T.Start();
        }



        //Admin Commands
        public static void AdminSave(Hangar Plugin, CommandContext Context, string GridNameOrEntityID)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin, true);
            Task T = new Task(() => Checks.AdminSaveGrid(GridNameOrEntityID));
            T.Start();
        }

        public static void AdminLoad(Hangar Plugin, CommandContext Context, string NameOrSteamID, string GridNameOrNumber)
        {
            HangarChecks Checks = new HangarChecks(Context, Plugin, true);
            Task T = new Task(() => Checks.AdminLoadGrid(NameOrSteamID, GridNameOrNumber));
            T.Start();
        }

        public static void AdminSyncPlayer(Hangar Plugin, CommandContext Context, string NameOrSteamId)
        {
            Action X = new Action(delegate
            {

                if (!Plugin.Config.PluginEnabled)
                    return;
                Chat chat = new Chat(Context, true);
                int newGrids = 0;
                if (Utils.AdminTryGetPlayerSteamID(NameOrSteamId, chat, out ulong SteamID))
                {
                    GridMethods methods = new GridMethods(SteamID, Plugin.Config.FolderDirectory);
                    newGrids = methods.SyncWithDisk();
                }
                chat.Respond($"Found {newGrids} grids");

            });

            Task T = new Task(X);
            T.Start();
        }

        public static void AdminSyncAllPlayers(Hangar Plugin, CommandContext Context)
        {
            Action X = new Action(delegate
            {

                if (!Plugin.Config.PluginEnabled)
                    return;
                Chat chat = new Chat(Context, true);
                var PlayerIdentities = MySession.Static.Players.GetAllIdentities().OfType<MyIdentity>();
                int newGrids = 0;
                HashSet<ulong> processedIds = new HashSet<ulong>();
                foreach (MyIdentity player in PlayerIdentities)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    ulong SteamID = MySession.Static.Players.TryGetSteamId(player.IdentityId);
                    if (SteamID != 0)
                    {
                        if (processedIds.Contains(SteamID))
                        {
                            continue;
                        }
                        processedIds.Add(SteamID);
                        GridMethods methods = new GridMethods(SteamID, Plugin.Config.FolderDirectory);
                        newGrids += methods.SyncWithDisk();
                    }
                }
                chat.Respond($"Found {newGrids} grids for {processedIds.Count} players");

            });

            Task T = new Task(X);
            T.Start();
        }

        public static void AdminRunAutoHangar(Hangar Plugin, CommandContext Context)
        {


            AutoHangar autoHangar = new AutoHangar(Plugin);
            autoHangar.RunAutoHangar();
            Chat chat = new Chat(Context, true);
            chat.Respond("Finished Running Autohangar!");
        }

        public static void AdminList(Hangar Plugin, CommandContext Context, string NameOrSteamID)
        {
            Action X = new Action(delegate
            {

                if (!Plugin.Config.PluginEnabled)
                    return;

                Chat chat = new Chat(Context, true);


                if (Utils.AdminTryGetPlayerSteamID(NameOrSteamID, chat, out ulong SteamID))
                {

                    GridMethods methods = new GridMethods(SteamID, Plugin.Config.FolderDirectory);

                    if (!methods.LoadInfoFile(out PlayerInfo Data))
                    {
                        return;
                    }

                    if (Data.Grids == null || Data.Grids.Count == 0)
                    {
                        chat.Respond("There are no grids in the hangar!");
                        return;
                    }


                    int MaxStorage = Plugin.Config.NormalHangarAmount;
                    if (MySession.Static.PromotedUsers.ContainsKey(SteamID))
                    {
                        //prob redundant but ill leave it incase some future leveling system
                        MyPromoteLevel level = MySession.Static.PromotedUsers[SteamID];
                        if (level >= MyPromoteLevel.Scripter)
                        {
                            MaxStorage = Plugin.Config.ScripterHangarAmount;
                        }
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
            });
            Task T = new Task(X);
            T.Start();
        }

        public static void AdminDetails(Hangar Plugin, CommandContext Context, string NameOrSteamID, string GridNameOrNumber)
        {


            Action X = new Action(delegate
            {

                if (!Plugin.Config.PluginEnabled)
                    return;

                Chat chat = new Chat(Context, true);


                if (Utils.AdminTryGetPlayerSteamID(NameOrSteamID, chat, out ulong SteamID))
                {
                    GridMethods methods = new GridMethods(SteamID, Plugin.Config.FolderDirectory);
                    methods.LoadInfoFile(out PlayerInfo Data);




                    if (Int32.TryParse(GridNameOrNumber, out int result))
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
                            ModCommunication.SendMessageTo(new DialogMessage(Grid.GridName, $"Ship Information", stringBuilder.ToString()), Context.Player.SteamUserId);
                        }
                        else
                        {
                            chat.Respond(stringBuilder.ToString());
                        }
                    }
                    else
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
                                    ModCommunication.SendMessageTo(new DialogMessage(grid.GridName, $"Ship Information", stringBuilder.ToString()), Context.Player.SteamUserId);
                                }
                                else
                                {
                                    chat.Respond(stringBuilder.ToString());
                                }
                                // Context.Respond("You removed " + grid.GridName + " from the market!");
                            }
                        }
                    }
                }

            });


            Task T = new Task(X);
            T.Start();




        }

        public static void AdminRemove(Hangar Plugin, CommandContext Context, string NameOrSteamID, string GridNameOrNumber)
        {
            Action X = new Action(delegate
            {

                if (!Plugin.Config.PluginEnabled)
                    return;

                Chat chat = new Chat(Context, true);


                if (Utils.AdminTryGetPlayerSteamID(NameOrSteamID, chat, out ulong SteamID))
                {
                    GridMethods methods = new GridMethods(SteamID, Plugin.Config.FolderDirectory);

                    if (!methods.LoadInfoFile(out PlayerInfo Data))
                    {
                        return;
                    }


                    if (Int32.TryParse(GridNameOrNumber, out int result))
                    {
                        if (result > Data.Grids.Count)
                        {
                            chat.Respond("This hangar slot is empty! Select a grid that is in the hangar!");
                            return;
                        }


                        if (result != 0)
                        {

                            GridStamp Grid = Data.Grids[result - 1];
                            Data.Grids.RemoveAt(result - 1);
                            string path = Path.Combine(methods.FolderPath, Grid.GridName + ".sbc");
                            File.Delete(path);
                            chat.Respond(string.Format("{0} was successfully deleted!", Grid.GridName));
                            FileSaver.Save(Path.Combine(methods.FolderPath, "PlayerInfo.json"), Data);
                            return;

                        }
                        else if (result == 0)
                        {
                            int counter = 0;
                            foreach (var grid in Data.Grids)
                            {
                                string path = Path.Combine(methods.FolderPath, grid.GridName + ".sbc");
                                File.Delete(path);
                                counter++;
                            }

                            Data.Grids.Clear();
                            chat.Respond(string.Format("Successfully deleted {0} grids!", counter));
                            FileSaver.Save(Path.Combine(methods.FolderPath, "PlayerInfo.json"), Data);
                            return;

                        }


                    }
                    else
                    {
                        foreach (var grid in Data.Grids)
                        {

                            if (grid.GridName == GridNameOrNumber)
                            {

                                Data.Grids.Remove(grid);
                                string path = Path.Combine(methods.FolderPath, grid.GridName + ".sbc");
                                File.Delete(path);
                                chat.Respond(string.Format("{0} was successfully deleted!", grid.GridName));
                                FileSaver.Save(Path.Combine(methods.FolderPath, "PlayerInfo.json"), Data);
                                return;

                            }
                        }
                    }
                }

            });

            Task T = new Task(X);
            T.Start();

        }

        public static void AdminSaveAll(Hangar Plugin, CommandContext Context)
        {
            Action X = new Action(delegate
            {
                AutoHangar autoHangar = new AutoHangar(Plugin);
                autoHangar.SaveAll();
            });

            Task T = new Task(X);
            T.Start();
        }

    }



    [Torch.Commands.Category("hangarmod")]
    public class AdminCommands : CommandModule
    {

        private Hangar Plugin => (Hangar)Context.Plugin;

        [Command("save", "Saves targeted grid in hangar ignoring limits")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void AdminSave(string GridNameOrEntityID = null)
        {
            Commands.AdminSave(Plugin, Context, GridNameOrEntityID);
        }

        [Command("load", "loads targeted grid from hangar ignoring limits")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void AdminLoad(string NameOrSteamID, string GridNameOrNumber)
        {
            Commands.AdminLoad(Plugin, Context, NameOrSteamID, GridNameOrNumber);
        }


        [Command("sync", "syncs player hangar with the sbcs on disk")]
        [Permission(MyPromoteLevel.Admin)]
        public void AdminSyncPlayer(string NameOrSteamId)
        {
            Commands.AdminSyncPlayer(Plugin, Context, NameOrSteamId);
        }

        [Command("syncall", "syncs all player hangars with the sbcs on disk")]
        [Permission(MyPromoteLevel.Admin)]
        public void AdminSyncAllPlayers()
        {
            Commands.AdminSyncAllPlayers(Plugin, Context);
        }

        [Command("AutoHangar", "Runs AutoHangar based off of configs")]
        [Permission(MyPromoteLevel.Admin)]
        public void RunAuto()
        {
            Commands.AdminRunAutoHangar(Plugin, Context);
        }


        [Command("list", "Lists all the grids in someones hangar.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void List(string NameOrSteamID)
        {
            Commands.AdminList(Plugin, Context, NameOrSteamID);
        }

        [Command("info", "Provides information about the ship in your hangar")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void HangarDetails(string NameOrSteamID, string GridNameOrNumber)
        {
            Commands.AdminDetails(Plugin, Context, NameOrSteamID, GridNameOrNumber);
        }


        [Command("remove", "Removes specified grid from hangar")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void HangarRemove(string NameOrSteamID, string GridNameOrNumber)
        {
            Commands.AdminRemove(Plugin, Context, NameOrSteamID, GridNameOrNumber);
        }


        [Command("SaveAll", "Saves Everygrid in the server to players hangars")]
        [Permission(MyPromoteLevel.Admin)]
        public void SaveAll()
        {
            Commands.AdminSaveAll(Plugin, Context);
        }

    }


    [Torch.Commands.Category("hm")]
    public class SimpAdminCommands : CommandModule
    {

        private Hangar Plugin => (Hangar)Context.Plugin;

        [Command("save", "Saves targeted grid in hangar ignoring limits")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void AdminSave(string GridNameOrEntityID = null)
        {
            Commands.AdminSave(Plugin, Context, GridNameOrEntityID);
        }

        [Command("load", "loads targeted grid from hangar ignoring limits")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void AdminLoad(string NameOrSteamID, string GridNameOrNumber)
        {
            Commands.AdminLoad(Plugin, Context, NameOrSteamID, GridNameOrNumber);
        }


        [Command("sync", "syncs player hangar with the sbcs on disk")]
        [Permission(MyPromoteLevel.Admin)]
        public void AdminSyncPlayer(string NameOrSteamId)
        {
            Commands.AdminSyncPlayer(Plugin, Context, NameOrSteamId);
        }

        [Command("syncall", "syncs all player hangars with the sbcs on disk")]
        [Permission(MyPromoteLevel.Admin)]
        public void AdminSyncAllPlayers()
        {
            Commands.AdminSyncAllPlayers(Plugin, Context);
        }

        [Command("AutoHangar", "Runs AutoHangar based off of configs")]
        [Permission(MyPromoteLevel.Admin)]
        public void RunAuto()
        {
            Commands.AdminRunAutoHangar(Plugin, Context);
        }


        [Command("list", "Lists all the grids in someones hangar.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void List(string NameOrSteamID)
        {
            Commands.AdminList(Plugin, Context, NameOrSteamID);
        }

        [Command("info", "Provides information about the ship in your hangar")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void HangarDetails(string NameOrSteamID, string GridNameOrNumber)
        {
            Commands.AdminDetails(Plugin, Context, NameOrSteamID, GridNameOrNumber);
        }


        [Command("remove", "Removes specified grid from hangar")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void HangarRemove(string NameOrSteamID, string GridNameOrNumber)
        {
            Commands.AdminRemove(Plugin, Context, NameOrSteamID, GridNameOrNumber);
        }


        [Command("SaveAll", "Saves Everygrid in the server to players hangars")]
        [Permission(MyPromoteLevel.Admin)]
        public void SaveAll()
        {
            Commands.AdminSaveAll(Plugin, Context);
        }

    }


}
