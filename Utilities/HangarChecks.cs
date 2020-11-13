using Newtonsoft.Json;
using NLog;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace QuantumHangar.Utilities
{
    public class HangarChecks
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public CommandContext Context;
        public Hangar Plugin;
        private Chat chat;
        public bool _Admin;


        private MyCharacter myCharacter;
        private MyIdentity myIdentity;
        private long TargetIdentity { get; set; }
        private ulong PlayerSteamID { get; set; }
        private int MaxHangarSlots;
        public string PlayerHangarPath { get; set; }
        private GridMethods Methods;

        private double LoadRadius;
        private bool LoadFromSavePosition;


        public HangarChecks(CommandContext _Context, Hangar _Plugin, bool Admin = false)
        {
            Context = _Context;
            Plugin = _Plugin;
            chat = new Chat(Context, Admin);
            _Admin = Admin;

            if (Context.Player != null)
            {
                PlayerSteamID = Context.Player.SteamUserId;
            }

            //Sanity distance check (Incase user fucks it up)
            if (Plugin.Config.LoadRadius <= 1)
            {
                LoadRadius = 100;
            }
            else
            {
                LoadRadius = Plugin.Config.LoadRadius;
            }
        }



        public bool InitilizeCharacter()
        {

            if (!Plugin.Config.PluginEnabled)
                return false;

            if (Context.Player == null)
            {
                chat.Respond("You cant do this via console stupid!");
                return false;
            }
            myIdentity = ((MyPlayer)Context.Player).Identity;
            TargetIdentity = myIdentity.IdentityId;

            if (myIdentity.Character == null)
            {
                chat.Respond("Player has no Character");
                return false;
            }

            myCharacter = myIdentity.Character;

            Methods = new GridMethods(PlayerSteamID, Plugin.Config.FolderDirectory, this);
            PlayerHangarPath = Methods.FolderPath;


            MaxHangarSlots = Plugin.Config.NormalHangarAmount;


            if (Context.Player.PromoteLevel == MyPromoteLevel.Scripter)
            {
                MaxHangarSlots = Plugin.Config.ScripterHangarAmount;
            }
            else if (Context.Player.PromoteLevel == MyPromoteLevel.Moderator)
            {
                MaxHangarSlots = Plugin.Config.ScripterHangarAmount * 2;
            }
            else if (Context.Player.PromoteLevel >= MyPromoteLevel.Admin)
            {
                MaxHangarSlots = Plugin.Config.ScripterHangarAmount * 10;
            }



            return true;

        }

        /*
         *  These are the main chat commands
         * 
         * 
         */

        public void SaveGrid()
        {




            if (!IsServerSaving()
                || !InitilizeCharacter()
                || !CheckZoneRestrictions(true)
                || !CheckGravity()
                || CheckEnemyDistance()
                || !Methods.LoadInfoFile(out PlayerInfo Data)
                || !CheckHanagarLimits(Data))
                return;




            //Check Player Timer
            if (!CheckPlayerTimeStamp(ref Data))
            {
                return;
            }



            Result result = GetGrids(myCharacter);

            if (!result.GetGrids)
            {
                return;
            }

            if (!ExtensiveLimitChecker(result, Data))
            {
                return;
            }

            Log.Warn("Checking for exsisting grids in hangar!");
            //Check for existing grids in hangar!
            CheckForExistingGrids(Data, ref result);



            //Check for price
            if (!RequireSaveCurrency(result))
            {
                return;
            }

            if (!BeginSave(result, Data))
            {
                return;
            }
        }

        public void LoadGrid(string GridNameOrNumber, bool ForceLoadAtSavePosition = false)
        {

            LoadFromSavePosition = ForceLoadAtSavePosition;

            //Log.Info("Player Path: " + path);


            if (!IsServerSaving()
                || !InitilizeCharacter()
                || !CheckZoneRestrictions(false)
                || !CheckGravity()
                || !Methods.LoadInfoFile(out PlayerInfo Data))
                return;

            //Check Player Timer
            if (!CheckPlayerTimeStamp(ref Data))
            {
                return;
            }


            if (Data.Grids.Count == 0)
            {
                chat.Respond("You have no grids in your hangar!");
                return;
            }

            int result = 0;
            try
            {
                result = Int32.Parse(GridNameOrNumber);
                //Got result. Check to see if its not an absured number
                if (result < 0)
                {
                    chat.Respond("Jeez! Why so negative! Maybe you should try positive numbers for a change!");
                    return;
                }

                if (result == 0)
                {
                    chat.Respond("OHH COME ON! There is no ZEROTH hangar slot! Start with 1!!");
                    return;
                }

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



            if (result != 0)
            {

                GridStamp Grid = Data.Grids[result - 1];

                if (CheckEnemyDistance(ForceLoadAtSavePosition, Grid.GridSavePosition))
                    return;


                //Check PCU!
                if (!CheckGridLimits(myIdentity, Grid))
                    return;

                if (!LoadFromOriginalPositionCheck(Grid))
                    return;

                //Check to see if the grid is on the market!
                if (!CheckIfOnMarket(Grid, myIdentity))
                    return;

                if (!RequireLoadCurrency(Grid))
                    return;


                //string path = Path.Combine(PlayerHangarPath, Grid.GridName + ".sbc");

                if (!LoadGridFile(Grid.GridName, Data, Grid))
                {
                    return;
                }
            }


            if (GridNameOrNumber != "")
            {
                //Scan containg Grids if the user typed one
                foreach (var grid in Data.Grids)
                {

                    if (grid.GridName == GridNameOrNumber)
                    {
                        //Check BlockLimits
                        if (!CheckGridLimits(myIdentity, grid))
                        {
                            return;
                        }

                        //Check to see if the grid is on the market!
                        if (!CheckIfOnMarket(grid, myIdentity))
                        {
                            return;
                        }

                        string path = Path.Combine(PlayerHangarPath, grid.GridName + ".sbc");
                        if (!LoadGridFile(path, Data, grid))
                        {
                            return;
                        }

                    }
                }
            }
        }

        public void ListGrids()
        {

            if (!InitilizeCharacter()
                || !Methods.LoadInfoFile(out PlayerInfo Data))
                return;

            Log.Warn(Data.Grids.Count);



            if (Data.Grids.Count == 0)
            {
                chat.Respond("You have no grids in your hangar!");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine("You have " + Data.Grids.Count() + "/" + MaxHangarSlots + " stored grids:");
            int count = 1;
            foreach (var grid in Data.Grids)
            {
                sb.AppendLine(" [" + count + "] - " + grid.GridName);
                count++;
            }

            chat.Respond(sb.ToString());
        }

        public void DeleteGrid(string GridNameOrNumber)
        {
            if (!InitilizeCharacter()
                || !Methods.LoadInfoFile(out PlayerInfo Data))
                return;


            if (Data.Grids.Count == 0)
            {
                chat.Respond("You have no grids in your hangar!");
                return;
            }

            int result = -1;
            try
            {
                result = Int32.Parse(GridNameOrNumber);
                //Got result. Check to see if its not an absured number

                if (result > Data.Grids.Count && result < MaxHangarSlots)
                {
                    chat.Respond("This hangar slot is empty! Select a grid that is in your hangar!");
                    return;
                }
                else if (result > MaxHangarSlots)
                {
                    chat.Respond("Invalid number! You only have a max of " + MaxHangarSlots + " slots!");
                    return;
                }
            }
            catch
            {
                //If failed cont to normal string name
            }



            if (result != 0 && result != -1)
            {

                GridStamp Grid = Data.Grids[result - 1];
                Data.Grids.RemoveAt(result - 1);
                string path = Path.Combine(PlayerHangarPath, Grid.GridName + ".sbc");
                File.Delete(path);
                chat.Respond(string.Format("{0} was successfully deleted!", Grid.GridName));
                FileSaver.Save(Path.Combine(PlayerHangarPath, "PlayerInfo.json"), Data);
                return;

            }
            else if (result == 0)
            {
                if (Plugin.Config.requireAdminPermForHangarWipe)
                {
                    chat.Respond("You don't have permission to wipe your entire Hanger!");
                    return;
                }

                int counter = 0;
                foreach (var grid in Data.Grids)
                {
                    string path = Path.Combine(PlayerHangarPath, grid.GridName + ".sbc");
                    File.Delete(path);
                    counter++;
                }

                Data.Grids.Clear();
                chat.Respond(string.Format("Successfully deleted {0} grids!", counter));
                FileSaver.Save(Path.Combine(PlayerHangarPath, "PlayerInfo.json"), Data);
                return;

            }

            if (GridNameOrNumber != "")
            {
                //Scan containg Grids if the user typed one
                foreach (var grid in Data.Grids)
                {

                    if (grid.GridName == GridNameOrNumber)
                    {

                        Data.Grids.Remove(grid);
                        string path = Path.Combine(PlayerHangarPath, grid.GridName + ".sbc");
                        File.Delete(path);
                        chat.Respond(string.Format("{0} was successfully deleted!", grid.GridName));
                        FileSaver.Save(Path.Combine(PlayerHangarPath, "PlayerInfo.json"), Data);
                        return;

                    }
                }
            }

        }

        public void SellGrid(string GridNameOrNumber, string price, string description)
        {
            if (!InitilizeCharacter())
                return;

            if (!Plugin.Config.GridMarketEnabled)
            {
                chat.Respond("GridMarket is not enabled!");
                return;
            }


            if (!Methods.LoadInfoFile(out PlayerInfo Data))
                return;


            if (Int64.TryParse(price, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out long NumPrice))
            {
                if (NumPrice < 0)
                {
                    NumPrice = Math.Abs(NumPrice);
                }
            }
            else
            {
                chat.Respond("Invalid Price Format");
                return;
            }



            if (Int32.TryParse(GridNameOrNumber, out int result))
            {
                if (result > Data.Grids.Count)
                {
                    chat.Respond("This hangar slot is empty! Select a grid that is in your hangar!");
                    return;
                }

                GridStamp Grid = Data.Grids[result - 1];

                if (Grid.GridForSale == true)
                {
                    chat.Respond("Selected grid is already on the market!");
                    return;
                }


                //string path = Path.Combine(IDPath, Grid.GridName + ".sbc");

                Data.Grids[result - 1].GridForSale = true;
                chat.Respond("Preparing grid for market!");


                if (!SellOnMarket(PlayerHangarPath, Grid, Data, NumPrice, description))
                {
                    chat.Respond("Grid doesnt exist in your hangar folder! Contact an Admin!");
                    return;
                }

            }
            else
            {
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
                        if (!SellOnMarket(PlayerHangarPath, grid, Data, NumPrice, description))
                        {
                            chat.Respond("Grid doesnt exist in your hangar folder! Contact an Admin!");
                            return;
                        }

                    }
                }
            }
        }

        public void RemoveOffer(string GridNameOrNumber)
        {
            if (!InitilizeCharacter())
                return;

            if (!Plugin.Config.GridMarketEnabled)
            {
                chat.Respond("GridMarket is not enabled!");
                return;
            }


            if (!Methods.LoadInfoFile(out PlayerInfo Data))
                return;



            if (Int32.TryParse(GridNameOrNumber, out int result))
            {

                if (result > Data.Grids.Count)
                {
                    chat.Respond("This hangar slot is empty! Select a grid that is in your hangar!");
                    return;
                }


                GridStamp Grid = Data.Grids[result - 1];


                //Check to see if the grid is on the market!
                if (!CheckIfOnMarket(Grid, myIdentity))
                {
                    return;
                }
                chat.Respond("You removed " + Grid.GridName + " from the market!");
                FileSaver.Save(Path.Combine(PlayerHangarPath, "PlayerInfo.json"), Data);
            }
            else
            {
                if (GridNameOrNumber != "")
                {
                    //Scan containg Grids if the user typed one
                    foreach (var grid in Data.Grids)
                    {

                        if (grid.GridName == GridNameOrNumber)
                        {

                            //Check to see if the grid is on the market!
                            if (!CheckIfOnMarket(grid, myIdentity))
                            {
                                return;
                            }

                            chat.Respond("You removed " + grid.GridName + " from the market!");
                            FileSaver.Save(Path.Combine(PlayerHangarPath, "PlayerInfo.json"), Data);
                            //File.WriteAllText(Path.Combine(IDPath, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
                        }
                    }
                }
            }
        }

        public void HangarInfo(string GridNameOrNumber)
        {
            if (!InitilizeCharacter())
                return;

            if (!Plugin.Config.GridMarketEnabled)
            {
                chat.Respond("GridMarket is not enabled!");
                return;
            }


            if (!Methods.LoadInfoFile(out PlayerInfo Data))
                return;




            GridStamp Grid = null;
            if (Int32.TryParse(GridNameOrNumber, out int result))
            {
                if (result > Data.Grids.Count || result == 0)
                {
                    chat.Respond("This hangar slot is empty! Select a grid that is in your hangar!");
                    return;
                }

                Grid = Data.Grids[result - 1];
            }
            else
            {
                if (GridNameOrNumber == "")
                {
                    chat.Respond("Invalid Grid name!");
                    return;
                }


                foreach (var grid in Data.Grids)
                {
                    if (grid.GridName == GridNameOrNumber)
                    {

                        Grid = grid;
                    }
                }

            }


            if (Grid == null)
            {
                chat.Respond("Unable to find given grid!");
                return;
            }



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


            ModCommunication.SendMessageTo(new DialogMessage(Grid.GridName, $"Ship Information", stringBuilder.ToString()), PlayerSteamID);
        }

        public void AdminSaveGrid(string GridName = null)
        {
            if (!Plugin.Config.PluginEnabled)
                return;


            MyCharacter character = null;
            Result result = new Result();
            //MyCubeGrid grid;
            if (GridName == null)
            {
                if (Context.Player == null)
                {
                    chat.Respond("You must supply a gridname via console!");
                    return;
                }

                var player = ((MyPlayer)Context.Player).Identity;

                if (player.Character == null)
                {
                    chat.Respond("You have no character to target a grid!");
                    return;
                }

                character = player.Character;

                result = AdminGetGrids(character);
                if (!result.GetGrids)
                {
                    return;
                }
            }
            else
            {
                Hangar.Debug("Admin is running this in console!");
                result = AdminGetGrids(character, GridName);
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

            Methods = new GridMethods(id, Plugin.Config.FolderDirectory, this);
            PlayerHangarPath = Methods.FolderPath;
            PlayerInfo Data = new PlayerInfo();
            //Get PlayerInfo from file
            try
            {
                Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(PlayerHangarPath, "PlayerInfo.json")));
            }
            catch
            {
                //New player. Go ahead and create new. Should not have a timer.
            }


            CheckForExistingGrids(Data, ref result);



            if (Methods.SaveGrids(result.grids, result.GridName, Plugin))
            {
                chat.Respond("Save Complete!");

                //Fill out grid info and store in file
                GetBPDetails(result, Plugin.Config, out GridStamp Grid);
                Data.Grids.Add(Grid);


                //Overwrite file
                FileSaver.Save(Path.Combine(PlayerHangarPath, "PlayerInfo.json"), Data);
                //File.WriteAllText(Path.Combine(path, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
            }
            else
                chat.Respond("Save Failed!");
        }

        public void AdminLoadGrid(string NameOrID, string gridNameOrEntityId)
        {
            if (!Plugin.Config.PluginEnabled)
                return;


            LoadFromSavePosition = true;

            if (Utils.AdminTryGetPlayerSteamID(NameOrID, chat, out ulong SteamID))
            {

                Methods = new GridMethods(SteamID, Plugin.Config.FolderDirectory, this);
                PlayerHangarPath = Methods.FolderPath;
                TargetIdentity = MySession.Static.Players.TryGetIdentityId(SteamID);



                PlayerInfo Data = new PlayerInfo();
                //Get PlayerInfo from file
                try
                {
                    Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(PlayerHangarPath, "PlayerInfo.json")));
                }
                catch
                {
                    //New player. Go ahead and create new. Should not have a timer.

                }

                var playerPosition = Vector3D.Zero;

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

                if (Data.Grids != null && result > 0 && result <= Data.Grids.Count)
                {
                    GridStamp Grid = Data.Grids[result - 1];
                   
                    //string path = Path.Combine(IDPath, Grid.GridName + ".sbc");
                    PlayerSteamID = SteamID;
                    if (!LoadGridFile(Grid.GridName, Data, Grid, true))
                    {
                        return;
                    }

                    return;
                }



                foreach (var grid in Data.Grids)
                {
                    if (grid.GridName == gridNameOrEntityId)
                    {
                        PlayerSteamID = SteamID;
                        if (!LoadGridFile(grid.GridName, Data, grid, true))
                        {
                            return;
                        }
                    }
                }
            }






        }




        /*
         *  Main utility commands
         * 
         * 
         */

        private bool RequireSaveCurrency(Result result)
        {
            //MyObjectBuilder_Definitions(MyParticlesManager)
            Chat chat = new Chat(Context);
            IMyPlayer Player = Context.Player;
            if (!Plugin.Config.RequireCurrency)
            {

                return true;
            }
            else
            {
                long SaveCost = 0;
                switch (Plugin.Config.HangarSaveCostType)
                {
                    case CostType.BlockCount:

                        foreach (MyCubeGrid grid in result.grids)
                        {
                            if (grid.GridSizeEnum == MyCubeSize.Large)
                            {
                                if (grid.IsStatic)
                                {
                                    //If grid is station
                                    SaveCost += Convert.ToInt64(grid.BlocksCount * Plugin.Config.CustomStaticGridCurrency);
                                }
                                else
                                {
                                    //If grid is large grid
                                    SaveCost += Convert.ToInt64(grid.BlocksCount * Plugin.Config.CustomLargeGridCurrency);
                                }
                            }
                            else
                            {
                                //if its a small grid
                                SaveCost += Convert.ToInt64(grid.BlocksCount * Plugin.Config.CustomSmallGridCurrency);
                            }


                        }

                        //Multiply by 
                        break;


                    case CostType.Fixed:

                        SaveCost = Convert.ToInt64(Plugin.Config.CustomStaticGridCurrency);

                        break;


                    case CostType.PerGrid:

                        foreach (MyCubeGrid grid in result.grids)
                        {
                            if (grid.GridSizeEnum == MyCubeSize.Large)
                            {
                                if (grid.IsStatic)
                                {
                                    //If grid is station
                                    SaveCost += Convert.ToInt64(Plugin.Config.CustomStaticGridCurrency);
                                }
                                else
                                {
                                    //If grid is large grid
                                    SaveCost += Convert.ToInt64(Plugin.Config.CustomLargeGridCurrency);
                                }
                            }
                            else
                            {
                                //if its a small grid
                                SaveCost += Convert.ToInt64(Plugin.Config.CustomSmallGridCurrency);
                            }
                        }

                        break;
                }

                Utils.TryGetPlayerBalance(Player.SteamUserId, out long Balance);


                if (Balance >= SaveCost)
                {
                    //Check command status!
                    string command = result.biggestGrid.DisplayName;
                    var confirmationCooldownMap = Plugin.ConfirmationsMap;
                    if (confirmationCooldownMap.TryGetValue(Player.IdentityId, out CurrentCooldown confirmationCooldown))
                    {
                        if (!confirmationCooldown.CheckCommandStatus(command))
                        {
                            //Confirmed command! Update player balance!
                            confirmationCooldownMap.Remove(Player.IdentityId);
                            chat.Respond("Confirmed! Saving grid!");

                            Player.RequestChangeBalance(-1 * SaveCost);
                            return true;
                        }
                        else
                        {
                            chat.Respond("Saving this grid in your hangar will cost " + SaveCost + " SC. Run this command again within 30 secs to continue!");
                            confirmationCooldown.StartCooldown(command);
                            return false;
                        }
                    }
                    else
                    {
                        chat.Respond("Saving this grid in your hangar will cost " + SaveCost + " SC. Run this command again within 30 secs to continue!");
                        confirmationCooldown = new CurrentCooldown();
                        confirmationCooldown.StartCooldown(command);
                        confirmationCooldownMap.Add(Player.IdentityId, confirmationCooldown);
                        return false;
                    }
                }
                else
                {
                    long Remaing = SaveCost - Balance;
                    chat.Respond("You need an additional " + Remaing + " SC to perform this action!");
                    return false;
                }
            }
        }

        private bool IsServerSaving()
        {
            bool Saving = MySession.Static.IsSaveInProgress; ;
            if (Saving)
            {
                chat.Respond("Server has a save in progress... Please wait!");
                return false;
            }

            return true;    
        }

        private bool RequireLoadCurrency(GridStamp Grid)
        {
            //MyObjectBuilder_Definitions(MyParticlesManager)
            Chat chat = new Chat(Context);
            IMyPlayer Player = Context.Player;
            if (!Plugin.Config.RequireCurrency)
            {

                return true;
            }
            else
            {
                long LoadCost = 0;
                switch (Plugin.Config.HangarLoadCostType)
                {
                    case CostType.BlockCount:
                        //If grid is station
                        LoadCost = Convert.ToInt64(Grid.NumberofBlocks * Plugin.Config.LoadStaticGridCurrency);
                        break;


                    case CostType.Fixed:

                        LoadCost = Convert.ToInt64(Plugin.Config.LoadStaticGridCurrency);

                        break;


                    case CostType.PerGrid:

                        LoadCost += Convert.ToInt64(Grid.StaticGrids * Plugin.Config.LoadStaticGridCurrency);
                        LoadCost += Convert.ToInt64(Grid.LargeGrids * Plugin.Config.LoadLargeGridCurrency);
                        LoadCost += Convert.ToInt64(Grid.SmallGrids * Plugin.Config.LoadSmallGridCurrency);


                        break;
                }

                Utils.TryGetPlayerBalance(Player.SteamUserId, out long Balance);


                if (Balance >= LoadCost)
                {
                    //Check command status!
                    string command = Grid.GridName;
                    var confirmationCooldownMap = Plugin.ConfirmationsMap;
                    if (confirmationCooldownMap.TryGetValue(Player.IdentityId, out CurrentCooldown confirmationCooldown))
                    {
                        if (!confirmationCooldown.CheckCommandStatus(command))
                        {
                            //Confirmed command! Update player balance!
                            confirmationCooldownMap.Remove(Player.IdentityId);
                            chat.Respond("Confirmed! Loading grid!");

                            Player.RequestChangeBalance(-1 * LoadCost);
                            return true;
                        }
                        else
                        {
                            chat.Respond("Loading this grid will cost " + LoadCost + " SC. Run this command again within 30 secs to continue!");
                            confirmationCooldown.StartCooldown(command);
                            return false;
                        }
                    }
                    else
                    {
                        chat.Respond("Loading this grid will cost " + LoadCost + " SC. Run this command again within 30 secs to continue!");
                        confirmationCooldown = new CurrentCooldown();
                        confirmationCooldown.StartCooldown(command);
                        confirmationCooldownMap.Add(Player.IdentityId, confirmationCooldown);
                        return false;
                    }
                }
                else
                {
                    long Remaing = LoadCost - Balance;
                    chat.Respond("You need an additional " + Remaing + " SC to perform this action!");
                    return false;
                }
            }
        }

        private bool LoadFromOriginalPositionCheck(GridStamp Grid, bool Admin = false)
        {
            //Need to get grid position (for legacy check. Old grids will have grid position of zero. When they re-save the position will be updated)
            bool LegacyLoadGrid = false;
            if (Grid.GridSavePosition == Vector3D.Zero)
            {
                LegacyLoadGrid = true;
            }

            //If this grid got saved under planet, we need to make sure this will be loaded near player
            if (Grid.ForceSpawnNearPlayer)
            {
                LoadFromSavePosition = false;
                return true;
            }

            bool PositionFlag = true;
            switch (Plugin.Config.LoadType)
            {
                case LoadType.ForceLoadMearPlayer:
                    LoadFromSavePosition = false;
                    break;


                case LoadType.ForceLoadNearOriginalPosition:
                    //Legacy check
                    LoadFromSavePosition = true;

                    if (LegacyLoadGrid)
                    {
                        LoadFromSavePosition = false;
                        chat.Respond("This grid has been saved in a previous version of Hangar. It will be loaded near your player");
                        break;
                    }

                    if (Admin)
                    {
                        return true;
                    }
                        

                    if (Plugin.Config.RequireLoadRadius)
                    {
                        double PlayerDistance = Vector3D.Distance(Context.Player.GetPosition(), Grid.GridSavePosition);

                        if (PlayerDistance > Plugin.Config.LoadRadius)
                        {
                            //Send GPS of Position to player
                            chat.Respond("You must be near where you saved your grid! A GPS has been added to your HUD");
                            string Name = Grid.GridName + " [within " + Plugin.Config.LoadRadius + "m]";
                            Utils.SendGps(Grid.GridSavePosition, Name, myIdentity.IdentityId);
                            PositionFlag = false;
                        }
                    }
                    else
                    {
                        //Send GPS of Position to player
                        chat.Respond("A GPS has been added to your HUD");
                        string Name = Grid.GridName + " Spawn Location";
                        Utils.SendGps(Grid.GridSavePosition, Name, myIdentity.IdentityId);
                    }

                    break;

                case LoadType.Optional:
                    if (LoadFromSavePosition)
                    {
                        //Legacy check
                        if (LegacyLoadGrid)
                        {
                            LoadFromSavePosition = false;
                            chat.Respond("This grid has been saved in a previous version of Hangar. It will be loaded near your player");
                            break;
                        }

                        if (Plugin.Config.RequireLoadRadius)
                        {
                            double PlayerDistance = Vector3D.Distance(Context.Player.GetPosition(), Grid.GridSavePosition);

                            if (PlayerDistance > Plugin.Config.LoadRadius)
                            {
                                //Send GPS of Position to player
                                chat.Respond("You must be near where you saved your grid! A GPS has been added to your HUD");
                                string Name = Grid.GridName + " [within " + Plugin.Config.LoadRadius + "]";
                                Utils.SendGps(Grid.GridSavePosition, Name, myIdentity.IdentityId);
                                PositionFlag = false;
                                break;
                            }
                        }
                    }
                    break;
            }
            return PositionFlag;
        }

        private bool CheckHanagarLimits(PlayerInfo Data)
        {
            if (Data.Grids.Count >= MaxHangarSlots)
            {
                chat.Respond("You have reached your hangar limit!");
                return false;
            }

            return true;

        }

        private bool ExtensiveLimitChecker(Result result, PlayerInfo Data)
        {
            //Begin Single Slot Save!
            Chat chat = new Chat(Context);

            int TotalBlocks = 0;
            int TotalPCU = 0;
            int StaticGrids = 0;
            int LargeGrids = 0;
            int SmallGrids = 0;
            foreach (MyCubeGrid grid in result.grids)
            {
                TotalBlocks += grid.BlocksCount;
                TotalPCU += grid.BlocksPCU;

                if (grid.GridSizeEnum == MyCubeSize.Large)
                {
                    if (grid.IsStatic)
                    {
                        StaticGrids += 1;
                    }
                    else
                    {
                        LargeGrids += 1;
                    }
                }
                else
                {
                    SmallGrids += 1;
                }

            }

            if (Plugin.Config.SingleMaxBlocks != 0)
            {


                if (TotalBlocks > Plugin.Config.SingleMaxBlocks)
                {
                    int remainder = TotalBlocks - Plugin.Config.SingleMaxBlocks;

                    chat.Respond("Grid is " + remainder + " blocks over the max slot block limit! " + TotalBlocks + "/" + Plugin.Config.SingleMaxBlocks);
                    return false;
                }

            }

            if (Plugin.Config.SingleMaxPCU != 0)
            {
                if (TotalPCU > Plugin.Config.SingleMaxPCU)
                {
                    int remainder = TotalPCU - Plugin.Config.SingleMaxBlocks;

                    chat.Respond("Grid is " + remainder + " PCU over the slot hangar PCU limit! " + TotalPCU + "/" + Plugin.Config.SingleMaxPCU);
                    return false;
                }
            }

            if (Plugin.Config.AllowStaticGrids)
            {
                if (Plugin.Config.SingleMaxLargeGrids != 0 && StaticGrids > Plugin.Config.SingleMaxStaticGrids)
                {
                    int remainder = StaticGrids - Plugin.Config.SingleMaxStaticGrids;

                    chat.Respond("You are " + remainder + " static grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (StaticGrids > 0)
                {
                    chat.Respond("Saving Static Grids is disabled!");
                    return false;
                }
            }

            if (Plugin.Config.AllowLargeGrids)
            {
                if (Plugin.Config.SingleMaxLargeGrids != 0 && LargeGrids > Plugin.Config.SingleMaxLargeGrids)
                {
                    int remainder = LargeGrids - Plugin.Config.SingleMaxLargeGrids;

                    chat.Respond("You are " + remainder + " large grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (LargeGrids > 0)
                {
                    chat.Respond("Saving Large Grids is disabled!");
                    return false;
                }
            }

            if (Plugin.Config.AllowSmallGrids)
            {
                if (Plugin.Config.SingleMaxSmallGrids != 0 && SmallGrids > Plugin.Config.SingleMaxSmallGrids)
                {
                    int remainder = LargeGrids - Plugin.Config.SingleMaxLargeGrids;

                    chat.Respond("You are " + remainder + " small grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (SmallGrids > 0)
                {
                    chat.Respond("Saving Small Grids is disabled!");
                    return false;
                }
            }


            //Hangar total limit!
            foreach (GridStamp Grid in Data.Grids)
            {
                TotalBlocks += Grid.NumberofBlocks;
                TotalPCU += Grid.GridPCU;

                StaticGrids += Grid.StaticGrids;
                LargeGrids += Grid.LargeGrids;
                SmallGrids += Grid.SmallGrids;

            }

            if (Plugin.Config.TotalMaxBlocks != 0 && TotalBlocks > Plugin.Config.TotalMaxBlocks)
            {
                int remainder = TotalBlocks - Plugin.Config.TotalMaxBlocks;

                chat.Respond("Grid is " + remainder + " blocks over the total hangar block limit! " + TotalBlocks + "/" + Plugin.Config.TotalMaxBlocks);
                return false;
            }

            if (Plugin.Config.TotalMaxPCU != 0 && TotalPCU > Plugin.Config.TotalMaxPCU)
            {

                int remainder = TotalPCU - Plugin.Config.TotalMaxPCU;
                chat.Respond("Grid is " + remainder + " PCU over the total hangar PCU limit! " + TotalPCU + "/" + Plugin.Config.TotalMaxPCU);
                return false;
            }


            if (Plugin.Config.TotalMaxStaticGrids != 0 && StaticGrids > Plugin.Config.TotalMaxStaticGrids)
            {
                int remainder = StaticGrids - Plugin.Config.TotalMaxStaticGrids;

                chat.Respond("You are " + remainder + " static grid over the total hangar limit!");
                return false;
            }


            if (Plugin.Config.TotalMaxLargeGrids != 0 && LargeGrids > Plugin.Config.TotalMaxLargeGrids)
            {
                int remainder = LargeGrids - Plugin.Config.TotalMaxLargeGrids;

                chat.Respond("You are " + remainder + " large grid over the total hangar limit!");
                return false;
            }


            if (Plugin.Config.TotalMaxSmallGrids != 0 && SmallGrids > Plugin.Config.TotalMaxSmallGrids)
            {
                int remainder = LargeGrids - Plugin.Config.TotalMaxSmallGrids;

                chat.Respond("You are " + remainder + " small grid over the total hangar limit!");
                return false;
            }




            return true;
        }

        private bool CheckPlayerTimeStamp(ref PlayerInfo Data)
        {
            //Check timestamp before continuing!
            if (Data == null)
            {
                //New players
                return true;
            }


            if (Data.Timer != null)
            {
                TimeStamp Old = Data.Timer;
                //There is a time limit!
                TimeSpan Subtracted = DateTime.Now.Subtract(Old.OldTime);
                TimeSpan WaitTimeSpawn = new TimeSpan(0, (int)Plugin.Config.WaitTime, 0);
                TimeSpan Remainder = WaitTimeSpawn - Subtracted;
                //Log.Info("TimeSpan: " + Subtracted.TotalMinutes);
                if (Subtracted.TotalMinutes <= Plugin.Config.WaitTime)
                {
                    //int RemainingTime = (int)Plugin.Config.WaitTime - Convert.ToInt32(Subtracted.TotalMinutes);
                    string Timeformat = string.Format("{0:mm}min & {0:ss}s", Remainder);
                    Chat.Respond("You have " + Timeformat + "  before you can perform this action!", Context);
                    return false;
                }
                else
                {
                    Data.Timer = null;
                    return true;
                }
            }

            return true;

        }

        private bool CheckEnemyDistance(bool LoadingAtSavePoint = false, Vector3D Position = new Vector3D())
        {
            IMyPlayer Player = Context.Player;
            if (!LoadingAtSavePoint)
            {
                Position = Player.GetPosition();
            }

            MyFaction PlayersFaction = MySession.Static.Factions.GetPlayerFaction(Player.IdentityId);
            bool EnemyFoundFlag = false;
            if (Plugin.Config.DistanceCheck > 0)
            {
                //Check enemy location! If under limit return!
                foreach (MyPlayer OnlinePlayer in MySession.Static.Players.GetOnlinePlayers())
                {
                    if (OnlinePlayer.Identity.IdentityId == Context.Player.IdentityId || MySession.Static.IsUserAdmin(OnlinePlayer.Id.SteamId))
                        continue;


                    MyFaction TargetPlayerFaction = MySession.Static.Factions.GetPlayerFaction(OnlinePlayer.Identity.IdentityId);
                    if (PlayersFaction != null && TargetPlayerFaction != null)
                    {
                        if (PlayersFaction.FactionId == TargetPlayerFaction.FactionId)
                            continue;

                        //Neutrals count as allies not friends for some reason
                        MyRelationsBetweenFactions Relation = MySession.Static.Factions.GetRelationBetweenFactions(PlayersFaction.FactionId, TargetPlayerFaction.FactionId).Item1;
                        if (Relation == MyRelationsBetweenFactions.Neutral || Relation == MyRelationsBetweenFactions.Friends)
                            continue;
                    }

                    if (Vector3D.Distance(Position, OnlinePlayer.GetPosition()) == 0)
                    {
                        continue;
                    }

                    if (Vector3D.Distance(Position, OnlinePlayer.GetPosition()) <= Plugin.Config.DistanceCheck)
                    {
                        Chat.Respond("Unable to load grid! Enemy within " + Plugin.Config.DistanceCheck + "m!", Context);
                        EnemyFoundFlag = true;
                    }
                }
            }


            if(Plugin.Config.GridDistanceCheck > 0 && Plugin.Config.GridCheckMinBlock > 0 && EnemyFoundFlag == false)
            {
                BoundingSphereD SpawnSphere = new BoundingSphereD(Position, Plugin.Config.GridDistanceCheck);

                List<MyEntity> entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref SpawnSphere, entities);



                //This is looping through all grids in the specified range. If we find an enemy, we need to break and return/deny spawning
                foreach(MyEntity G in entities)
                {
                    if (!(G is MyCubeGrid))
                        continue;

                    MyCubeGrid Grid = G as MyCubeGrid;

                    if (Grid.BigOwners.Count <= 0 || Grid.CubeBlocks.Count < Plugin.Config.GridCheckMinBlock)
                        continue;

                    

                    if (Grid.BigOwners.Contains(Context.Player.IdentityId))
                        continue;



                    //if the player isnt big owner, we need to scan for faction mates
                    bool FoundAlly = true;
                    foreach(long Owner in Grid.BigOwners)
                    {
                        MyFaction TargetPlayerFaction = MySession.Static.Factions.GetPlayerFaction(Owner);
                        if (PlayersFaction != null && TargetPlayerFaction != null)
                        {
                            if (PlayersFaction.FactionId == TargetPlayerFaction.FactionId)
                                continue;

                            MyRelationsBetweenFactions Relation = MySession.Static.Factions.GetRelationBetweenFactions(PlayersFaction.FactionId, TargetPlayerFaction.FactionId).Item1;
                            if (Relation == MyRelationsBetweenFactions.Enemies)
                            {
                                FoundAlly = false;
                                break;
                            }
                        }
                        else
                        {
                            FoundAlly = false;
                            break;
                        }
                    }


                    if (!FoundAlly)
                    {
                        //Stop loop
                        Chat.Respond("Unable to load grid! Enemy within " + Plugin.Config.GridDistanceCheck + "m!", Context);
                        EnemyFoundFlag = true;
                        break;
                    }
                }
            }


            return EnemyFoundFlag;

        }


        private Result GetGrids(MyCharacter character, string GridName = null)
        {
            List<MyCubeGrid> grids = GridMethods.FindGridList(GridName, character, Plugin.Config);
            Chat chat = new Chat(Context);


            Result Return = new Result();
            Return.grids = grids;
            MyCubeGrid biggestGrid = new MyCubeGrid();

            if (grids == null)
            {
                chat.Respond("Multiple grids found. Try to rename them first or try a different subgrid for identification!");
                Return.GetGrids = false;
                return Return;
            }

            if (grids.Count == 0)
            {
                chat.Respond("No grids found. Check your viewing angle or try the correct name!");
                Return.GetGrids = false;
                return Return;
            }


            foreach (var grid in grids)
            {
                if (biggestGrid.BlocksCount < grid.BlocksCount)
                {
                    biggestGrid = grid;
                }
            }


            if (biggestGrid == null)
            {
                chat.Respond("Grid incompatible!");
                Return.GetGrids = false;
                return Return;
            }

            Return.biggestGrid = biggestGrid;

            long playerId;

            if (biggestGrid.BigOwners.Count == 0)
                playerId = 0;
            else
                playerId = biggestGrid.BigOwners[0];


            if (playerId != Context.Player.IdentityId)
            {
                chat.Respond("You are not the owner of this grid!");
                Return.GetGrids = false;
                return Return;
            }

            Return.GetGrids = true;
            return Return;
        }
        private Result AdminGetGrids(MyCharacter character, string GridName = null)
        {
            List<MyCubeGrid> grids = GridMethods.FindGridList(GridName, character, Plugin.Config);
            Chat chat = new Chat(Context);

            Result Return = new Result();
            Return.grids = grids;
            MyCubeGrid biggestGrid = new MyCubeGrid();

            if (grids == null)
            {
                chat.Respond("Multiple grids found. Try to rename them first or try a different subgrid for identification!");
                Return.GetGrids = false;
                return Return;
            }

            if (grids.Count == 0)
            {
                chat.Respond("No grids found. Check your viewing angle or try the correct name!");
                Return.GetGrids = false;
                return Return;
            }



            foreach (var grid in grids)
            {
                if (biggestGrid == null || biggestGrid.BlocksCount < grid.BlocksCount)
                {
                    biggestGrid = grid;
                }
            }


            if (biggestGrid == null)
            {
                chat.Respond("Grid incompatible!");
                Return.GetGrids = false;
                return Return;
            }

            Return.biggestGrid = biggestGrid;

            long playerId;

            if (biggestGrid.BigOwners.Count == 0)
                playerId = 0;
            else
                playerId = biggestGrid.BigOwners[0];


            //Context.Respond("Preparing " + biggestGrid.DisplayName);
            Return.GetGrids = true;
            return Return;
        }

        private void CheckForExistingGrids(PlayerInfo Data, ref Result result)
        {
            if (Data == null)
            {
                Log.Warn("PlayerInfoData is NULL");
                //If Player info is empty, return true. (No data in hangar)
                return;
            }

            Log.Warn("Checking Grid Name");
            Utils.FormatGridName(Data, result);
        }

        private bool BeginSave(Result result, PlayerInfo Data)
        {

            if (Methods.SaveGrids(result.grids, result.GridName, Plugin))
            {

                TimeStamp stamp = new TimeStamp();
                stamp.OldTime = DateTime.Now;
                stamp.PlayerID = myIdentity.IdentityId;


                //Load player file and update!





                //Fill out grid info and store in file
                //GridStamp Grid = new GridStamp();

                GetBPDetails(result, Plugin.Config, out GridStamp Grid);




                Grid.ServerPort = MySandboxGame.ConfigDedicated.ServerPort;
                Data.Grids.Add(Grid);
                Data.Timer = stamp;

                //Overwrite file
                FileSaver.Save(Path.Combine(PlayerHangarPath, "PlayerInfo.json"), Data);
                chat.Respond("Save Complete!");
                return true;
            }
            else
            {
                Chat.Respond("Export Failed!", Context);
                return false;
            }

        }




        public static bool GetBPDetails(Result result, Settings Config, out GridStamp Grid)
        {
            //CreateNewGridStamp
            Grid = new GridStamp();

            float DisassembleRatio = 0;
            double EstimatedValue = 0;

            Grid.BlockTypeCount.Add("Reactors", 0);
            Grid.BlockTypeCount.Add("Turrets", 0);
            Grid.BlockTypeCount.Add("StaticGuns", 0);
            Grid.BlockTypeCount.Add("Refineries", 0);
            Grid.BlockTypeCount.Add("Assemblers", 0);

            foreach (MyCubeGrid SingleGrid in result.grids)
            {
                if (SingleGrid.GridSizeEnum == MyCubeSize.Large)
                {
                    if (SingleGrid.IsStatic)
                    {
                        Grid.StaticGrids += 1;
                        EstimatedValue += SingleGrid.BlocksCount * Config.StaticGridMarketMultiplier;
                    }
                    else
                    {
                        Grid.LargeGrids += 1;
                        EstimatedValue += SingleGrid.BlocksCount * Config.LargeGridMarketMultiplier;
                    }
                }
                else
                {
                    Grid.SmallGrids += 1;
                    EstimatedValue += SingleGrid.BlocksCount * Config.SmallGridMarketMultiplier;
                }


                foreach (MyCubeBlock SingleBlock in SingleGrid.GetFatBlocks())
                {
                    var Block = (IMyCubeBlock)SingleBlock;


                    if (SingleBlock.BuiltBy != 0)
                    {
                        UpdatePCUCounter(Grid, SingleBlock.BuiltBy, SingleBlock.BlockDefinition.PCU);
                    }

                    if (Block as IMyLargeTurretBase != null)
                    {
                        Grid.BlockTypeCount["Turrets"] += 1;
                    }
                    if (Block as IMySmallGatlingGun != null)
                    {
                        Grid.BlockTypeCount["Turrets"] += 1;
                    }

                    if (Block as IMyGunBaseUser != null)
                    {
                        Grid.BlockTypeCount["StaticGuns"] += 1;
                    }

                    if (Block as IMyRefinery != null)
                    {
                        Grid.BlockTypeCount["Refineries"] += 1;
                    }
                    if (Block as IMyAssembler != null)
                    {
                        Grid.BlockTypeCount["Assemblers"] += 1;
                    }


                    //Main.Debug("Block:" + Block.BlockDefinition + " ratio: " + Block.BlockDefinition.);
                    DisassembleRatio += SingleBlock.BlockDefinition.DeformationRatio;



                    Grid.NumberofBlocks += 1;


                }

                Grid.BlockTypeCount["Reactors"] += SingleGrid.NumberOfReactors;
                Grid.NumberOfGrids += 1;
                Grid.GridMass += SingleGrid.Mass;
                Grid.GridPCU += SingleGrid.BlocksPCU;
            }

            //Get Total Build Percent
            Grid.GridBuiltPercent = DisassembleRatio / Grid.NumberofBlocks;



            //Get default
            Grid.GridName = result.GridName;
            Grid.GridID = result.biggestGrid.EntityId;
            Grid.MarketValue = EstimatedValue;
            Grid.GridSavePosition = result.biggestGrid.PositionComp.GetPosition();

            //Get faction


            //MyPlayer player = MySession.Static.Players.GetPlayerByName(SelectedGrid.Seller);





            return true;
        }


        private static void UpdatePCUCounter(GridStamp Stamp, long Player, int Amount)
        {
            if (Stamp.ShipPCU.ContainsKey(Player))
            {
                Stamp.ShipPCU[Player] += Amount;
            }
            else
            {
                Stamp.ShipPCU.Add(Player, Amount);
            }
        }


        public static bool GetPublicOfferBPDetails(MyObjectBuilder_ShipBlueprintDefinition[] definition, out GridStamp Grid)
        {
            //CreateNewGridStamp
            Grid = new GridStamp();


            float DisassembleRatio = 0;

            Grid.BlockTypeCount.Add("Reactors", 0);
            Grid.BlockTypeCount.Add("Turrets", 0);
            Grid.BlockTypeCount.Add("StaticGuns", 0);
            Grid.BlockTypeCount.Add("Refineries", 0);
            Grid.BlockTypeCount.Add("Assemblers", 0);

            foreach (MyObjectBuilder_ShipBlueprintDefinition d in definition)
            {

                foreach (MyObjectBuilder_CubeGrid grid in d.CubeGrids)
                {
                    if (grid.GridSizeEnum == MyCubeSize.Large)
                    {
                        Grid.LargeGrids += 1;
                    }
                    else
                    {
                        Grid.SmallGrids += 1;
                    }

                    foreach (MyObjectBuilder_CubeBlock Block in grid.CubeBlocks)
                    {

                        if (Block as IMyLargeTurretBase != null)
                        {
                            Grid.BlockTypeCount["Turrets"] += 1;
                        }
                        if (Block as IMySmallGatlingGun != null)
                        {
                            Grid.BlockTypeCount["Turrets"] += 1;
                        }

                        if (Block as IMyGunBaseUser != null)
                        {
                            Grid.BlockTypeCount["StaticGuns"] += 1;
                        }

                        if (Block as IMyRefinery != null)
                        {
                            Grid.BlockTypeCount["Refineries"] += 1;
                        }
                        if (Block as IMyAssembler != null)
                        {
                            Grid.BlockTypeCount["Assemblers"] += 1;
                        }


                        //Main.Debug("Block:" + Block.BlockDefinition + " ratio: " + Block.BlockDefinition.);
                        DisassembleRatio += Block.DeformationRatio;



                        Grid.NumberofBlocks += 1;
                    }
                }
            }

            Grid.GridBuiltPercent = DisassembleRatio / Grid.NumberofBlocks;



            //Get faction


            //MyPlayer player = MySession.Static.Players.GetPlayerByName(SelectedGrid.Seller);





            return true;
        }


        private bool CheckGravity()
        {
            if (!Plugin.Config.AllowInGravity)
            {
                if (!Vector3D.IsZero(MyGravityProviderSystem.CalculateNaturalGravityInPoint(Context.Player.GetPosition())))
                {
                    chat.Respond("Saving & Loading in gravity has been disabled!");
                    return false;
                }
            }
            else
            {
                if (Plugin.Config.MaxGravityAmount == 0)
                {
                    return true;
                }

                float Gravity = MyGravityProviderSystem.CalculateNaturalGravityInPoint(Context.Player.GetPosition()).Length() / 9.81f;
                if (Gravity > Plugin.Config.MaxGravityAmount)
                {
                    //Log.Warn("Players gravity amount: " + Gravity);
                    chat.Respond("You are not permitted to Save/load in this gravity amount. Max amount: " + Plugin.Config.MaxGravityAmount + "g");
                    return false;
                }
            }

            return true;
        }

        private bool CheckZoneRestrictions(bool Save)
        {
            if (Plugin.Config.ZoneRestrictions.Count != 0)
            {
                Vector3D PlayerPosition = Context.Player.GetPosition();

                //Get save point
                int ClosestPoint = -1;
                double Distance = -1;

                for (int i = 0; i < Plugin.Config.ZoneRestrictions.Count(); i++)
                {

                    Vector3D ZoneCenter = new Vector3D(Plugin.Config.ZoneRestrictions[i].X, Plugin.Config.ZoneRestrictions[i].Y, Plugin.Config.ZoneRestrictions[i].Z);

                    double PlayerDistance = Vector3D.Distance(ZoneCenter, PlayerPosition);

                    if (PlayerDistance <= Plugin.Config.ZoneRestrictions[i].Radius)
                    {
                        //if player is within range

                        if (Save && !Plugin.Config.ZoneRestrictions[i].AllowSaving)
                        {
                            chat.Respond("You are not permitted to save grids in this zone");
                            return false;
                        }

                        if (!Save && !Plugin.Config.ZoneRestrictions[i].AllowLoading)
                        {
                            chat.Respond("You are not permitted to load grids in this zone");
                            return false;
                        }
                        return true;
                    }



                    if (Save && Plugin.Config.ZoneRestrictions[i].AllowSaving)
                    {
                        if (ClosestPoint == -1 || PlayerDistance <= Distance)
                        {
                            ClosestPoint = i;
                            Distance = PlayerDistance;
                        }
                    }


                    if (!Save && Plugin.Config.ZoneRestrictions[i].AllowLoading)
                    {
                        if (ClosestPoint == -1 || PlayerDistance <= Distance)
                        {
                            ClosestPoint = i;
                            Distance = PlayerDistance;
                        }
                    }



                }
                Vector3D ClosestZone = new Vector3D();
                try
                {
                    ClosestZone = new Vector3D(Plugin.Config.ZoneRestrictions[ClosestPoint].X, Plugin.Config.ZoneRestrictions[ClosestPoint].Y, Plugin.Config.ZoneRestrictions[ClosestPoint].Z);
                }
                catch (Exception e)
                {

                    chat.Respond("No areas found!");
                    //Log.Warn(e, "No suitable zones found! (Possible Error)");
                    return false;
                }




                if (Save)
                {

                    Utils.SendGps(ClosestZone, Plugin.Config.ZoneRestrictions[ClosestPoint].Name + " (within " + Plugin.Config.ZoneRestrictions[ClosestPoint].Radius + "m)", Context.Player.IdentityId);
                    chat.Respond("Nearest save area has been added to your HUD");
                    return false;
                }
                else
                {

                    Utils.SendGps(ClosestZone, Plugin.Config.ZoneRestrictions[ClosestPoint].Name + " (within " + Plugin.Config.ZoneRestrictions[ClosestPoint].Radius + "m)", Context.Player.IdentityId);
                    //Chat chat = new Chat(Context);
                    chat.Respond("Nearest load area has been added to your HUD");
                    return false;
                }








            }

            return true;
        }


        //Hagar Load

        private bool CheckIfOnMarket(GridStamp Grid, MyIdentity NewPlayer)
        {
            if (!Plugin.Config.GridMarketEnabled)
            {
                //If the grid market was turned off
                return true;
            }

            Chat chat = new Chat(Context);

            if (Grid.GridForSale)
            {
                if (Plugin.Config.RequireRestockFee)
                {
                    double CostAmount = Plugin.Config.RestockAmount;
                    string command = Grid.GridName;

                    var confirmationCooldownMap = Plugin.ConfirmationsMap;

                    if (confirmationCooldownMap.TryGetValue(NewPlayer.IdentityId, out CurrentCooldown confirmationCooldown))
                    {
                        if (!confirmationCooldown.CheckCommandStatus(command))
                        {
                            //Remove grid;

                            confirmationCooldownMap.Remove(NewPlayer.IdentityId);

                            //Update Balance etc
                            List<IMyPlayer> Seller = new List<IMyPlayer>();
                            MyAPIGateway.Players.GetPlayers(Seller, x => x.IdentityId == NewPlayer.IdentityId);

                            Seller[0].TryGetBalanceInfo(out long SellerBalance);
                            if (SellerBalance < CostAmount)
                            {
                                long remainder = Convert.ToInt64(CostAmount - SellerBalance);
                                chat.Respond("You need an additional " + remainder + "sc to perform this action!");
                                return false;
                            }

                            chat.Respond("Confirmed! Removing grid from market");

                            Seller[0].RequestChangeBalance(Convert.ToInt64(-1 * CostAmount));

                            try
                            {

                                MarketList Item = GridMarket.GridList.First(x => x.Name == Grid.GridName);

                                //We dont need to remove the item here anymore. (When the server broadcasts, we can remove it there)
                                //Main.GridList.Remove(Item);



                                //We need to send to all to add one item to the list!
                                CrossServerMessage SendMessage = new CrossServerMessage();
                                SendMessage.Type = CrossServer.MessageType.RemoveItem;
                                SendMessage.List.Add(Item);

                                Plugin.Market.MarketServers.Update(SendMessage);
                                Hangar.Debug("Point4");
                            }
                            catch (Exception e)
                            {
                                Hangar.Debug("Cannot remove grid from market! Perhaps Grid isnt on the market?", e, Hangar.ErrorType.Warn);
                            }

                            Grid.GridForSale = false;

                        }
                        else
                        {
                            chat.Respond("This grid is on the market! Removing it will cost " + CostAmount + "sc. Run this command again within 30 secs to continue!");
                            confirmationCooldown.StartCooldown(command);
                            return false;
                        }

                    }
                    else
                    {
                        chat.Respond("This grid is on the market! Removing it will cost " + CostAmount + "sc. Run this command again within 30 secs to continue!");

                        confirmationCooldown = new CurrentCooldown();
                        confirmationCooldown.StartCooldown(command);

                        confirmationCooldownMap.Add(NewPlayer.IdentityId, confirmationCooldown);
                        return false;
                    }

                }
                else
                {
                    try
                    {
                        if (GridMarket.GridList.Any(x => x.Name == Grid.GridName))
                        {
                            MarketList Item = GridMarket.GridList.First(x => x.Name == Grid.GridName);


                            //We need to send to all to add one item to the list!
                            CrossServerMessage SendMessage = new CrossServerMessage();
                            SendMessage.Type = CrossServer.MessageType.RemoveItem;
                            SendMessage.List.Add(Item);

                            Plugin.Market.MarketServers.Update(SendMessage);

                        }
                        else
                        {
                            Grid.GridForSale = false;
                            return true;
                        }

                    }
                    catch (Exception e)
                    {
                        Hangar.Debug("Cannot remove grid from market! Perhaps Grid isnt on the market?", e, Hangar.ErrorType.Warn);
                    }

                    Grid.GridForSale = false;
                }


            }
            return true;

        }

        private bool CheckGridLimits(MyIdentity NewPlayer, GridStamp Grid)
        {
            //Backwards compatibale
            if (Plugin.Config.OnLoadTransfer)
                return true;



            if (Grid.ShipPCU.Count == 0)
            {
                MyBlockLimits blockLimits = NewPlayer.BlockLimits;

                MyBlockLimits a = MySession.Static.GlobalBlockLimits;

                if (a.PCU <= 0)
                {
                    //PCU Limits on server is 0
                    //Skip PCU Checks
                    Hangar.Debug("PCU Server limits is 0!");
                    return true;
                }

                //Main.Debug("PCU Limit from Server:"+a.PCU);
                //Main.Debug("PCU Limit from Player: " + blockLimits.PCU);
                //Main.Debug("PCU Built from Player: " + blockLimits.PCUBuilt);

                int CurrentPcu = blockLimits.PCUBuilt;
                Hangar.Debug("Current PCU: " + CurrentPcu);

                int MaxPcu = blockLimits.PCU + CurrentPcu;

                int pcu = MaxPcu - CurrentPcu;
                //Main.Debug("MaxPcu: " + pcu);
                Hangar.Debug("Grid PCU: " + Grid.GridPCU);


                Hangar.Debug("Current player PCU:" + CurrentPcu);

                //Find the difference
                if (MaxPcu - CurrentPcu <= Grid.GridPCU)
                {
                    int Need = Grid.GridPCU - (MaxPcu - CurrentPcu);
                    Chat.Respond("PCU limit reached! You need an additional " + Need + " pcu to perform this action!", Context);
                    return false;
                }

                return true;
            }


            foreach (KeyValuePair<long, int> Player in Grid.ShipPCU)
            {

                MyIdentity Identity = MySession.Static.Players.TryGetIdentity(Player.Key);
                if (Identity == null)
                {
                    continue;
                }


                MyBlockLimits blockLimits = Identity.BlockLimits;
                MyBlockLimits a = MySession.Static.GlobalBlockLimits;

                if (a.PCU <= 0)
                {
                    //PCU Limits on server is 0
                    //Skip PCU Checks
                    //Hangar.Debug("PCU Server limits is 0!");
                    continue;
                }

                //Main.Debug("PCU Limit from Server:"+a.PCU);
                //Main.Debug("PCU Limit from Player: " + blockLimits.PCU);
                //Main.Debug("PCU Built from Player: " + blockLimits.PCUBuilt);

                int CurrentPcu = blockLimits.PCUBuilt;
                //Hangar.Debug("Current PCU: " + CurrentPcu);

                int MaxPcu = blockLimits.PCU + CurrentPcu;

                int pcu = MaxPcu - CurrentPcu;
                //Main.Debug("MaxPcu: " + pcu);
                //Hangar.Debug("Grid PCU: " + Grid.GridPCU);


                //Hangar.Debug("Current player PCU:" + CurrentPcu);

                //Find the difference
                if (MaxPcu - CurrentPcu <= Player.Value)
                {
                    int Need = Player.Value - (MaxPcu - CurrentPcu);
                    Chat.Respond("PCU limit reached! " + Identity.DisplayName + " needs an additional " + Need + " PCU to load this grid!", Context);
                    return false;
                }

            }

            return true;
        }


        public bool BlockLimitChecker(MyObjectBuilder_ShipBlueprintDefinition[] shipblueprints)
        {

            if (_Admin)
                return true;


            int BiggestGrid = 0;
            int blocksToBuild = 0;
            //failedBlockType = null;
            //Need dictionary for each player AND their blocks they own. (Players could own stuff on the same grid)
            Dictionary<long, Dictionary<string, int>> BlocksAndOwnerForLimits = new Dictionary<long, Dictionary<string, int>>();


            //Total PCU and Blocks
            int FinalBlocksCount = 0;
            int FinalBlocksPCU = 0;


            Dictionary<string, int> BlockPairNames = new Dictionary<string, int>();
            Dictionary<string, int> BlockSubTypeNames = new Dictionary<string, int>();


            //Go ahead and check if the block limits is enabled server side! If it isnt... continue!
            if (!Plugin.Config.EnableBlackListBlocks)
            {
                return true;
            }


            else
            {
                //If we are using built in server block limits..
                if (Plugin.Config.SBlockLimits)
                {
                    //& the server blocklimits is not enabled... Return true
                    if (MySession.Static.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.NONE)
                    {
                        return true;
                    }


                    //Cycle each grid in the ship blueprints
                    foreach (var shipBlueprint in shipblueprints)
                    {
                        foreach (var CubeGrid in shipBlueprint.CubeGrids)
                        {

                            //Main.Debug("CubeBlocks count: " + CubeGrid.GetType());
                            if (BiggestGrid < CubeGrid.CubeBlocks.Count())
                            {
                                BiggestGrid = CubeGrid.CubeBlocks.Count();
                            }
                            blocksToBuild = blocksToBuild + CubeGrid.CubeBlocks.Count();

                            foreach (MyObjectBuilder_CubeBlock block in CubeGrid.CubeBlocks)
                            {

                                MyDefinitionId defId = new MyDefinitionId(block.TypeId, block.SubtypeId);

                                if (MyDefinitionManager.Static.TryGetCubeBlockDefinition(defId, out MyCubeBlockDefinition myCubeBlockDefinition))
                                {
                                    //Check for BlockPair or SubType?
                                    string BlockName = "";
                                    if (Plugin.Config.SBlockLimits)
                                    {
                                        //Server Block Limits
                                        BlockName = myCubeBlockDefinition.BlockPairName;

                                    }
                                    else
                                    {
                                        //Custom Block SubType Limits
                                        BlockName = myCubeBlockDefinition.Id.SubtypeName;
                                    }

                                    long blockowner2 = 0L;
                                    blockowner2 = block.BuiltBy;

                                    //If the player dictionary already has a Key, we need to retrieve it
                                    if (BlocksAndOwnerForLimits.ContainsKey(blockowner2))
                                    {
                                        //if the dictionary already contains the same block type
                                        Dictionary<string, int> dictforuser = BlocksAndOwnerForLimits[blockowner2];
                                        if (dictforuser.ContainsKey(BlockName))
                                        {
                                            dictforuser[BlockName]++;
                                        }
                                        else
                                        {
                                            dictforuser.Add(BlockName, 1);
                                        }
                                        BlocksAndOwnerForLimits[blockowner2] = dictforuser;
                                    }
                                    else
                                    {
                                        BlocksAndOwnerForLimits.Add(blockowner2, new Dictionary<string, int>
                            {
                                {
                                    BlockName,
                                    1
                                }
                            });
                                    }

                                    FinalBlocksPCU += myCubeBlockDefinition.PCU;


                                    //if()

                                }


                            }

                            FinalBlocksCount += CubeGrid.CubeBlocks.Count;
                        }
                    }




                    if (MySession.Static.MaxGridSize != 0 && BiggestGrid > MySession.Static.MaxGridSize)
                    {
                        Chat.Respond("Biggest grid is over Max grid size! ", Context);
                        return false;
                    }

                    //Need too loop player identities in dictionary. Do this via seperate function
                    if (PlayerIdentityLoop(BlocksAndOwnerForLimits, FinalBlocksCount) == true)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
                else
                {
                    //BlockLimiter
                    if (Hangar.CheckFuture == null)
                    {
                        //BlockLimiter is null!
                        Chat.Respond("Blocklimiter Plugin not installed or Loaded!", Context);
                        Hangar.Debug("BLimiter plugin not installed or loaded! May require a server restart!");
                        return false;
                    }


                    List<MyObjectBuilder_CubeGrid> grids = new List<MyObjectBuilder_CubeGrid>();
                    foreach (var shipBlueprint in shipblueprints)
                    {
                        foreach (var CubeGrid in shipBlueprint.CubeGrids)
                        {
                            grids.Add(CubeGrid);
                        }
                    }

                    Hangar.Debug("Grids count: " + grids.Count());
                    object value = Hangar.CheckFuture.Invoke(null, new object[] { grids.ToArray(), myIdentity.IdentityId });

                    //Convert to value return type
                    bool ValueReturn = (bool)value;
                    if (!ValueReturn)
                    {
                        //Main.Debug("Cannont load grid in due to BlockLimiter Configs!");
                        return true;
                    }
                    else
                    {
                        Chat.Respond("Grid would be over Server-Blocklimiter limits!", Context);
                        Hangar.Debug("Cannont load grid in due to BlockLimiter Configs!");
                        return false;
                    }



                }



            }








        }




        private bool PlayerIdentityLoop(Dictionary<long, Dictionary<string, int>> BlocksAndOwnerForLimits, int blocksToBuild)
        {
            foreach (KeyValuePair<long, Dictionary<string, int>> Player in BlocksAndOwnerForLimits)
            {

                Dictionary<string, int> PlayerBuiltBlocks = Player.Value;
                MyIdentity myIdentity = MySession.Static.Players.TryGetIdentity(Player.Key);

                Chat chat = new Chat(Context);
                if (myIdentity != null)
                {
                    MyBlockLimits blockLimits = myIdentity.BlockLimits;
                    if (MySession.Static.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.PER_FACTION && MySession.Static.Factions.GetPlayerFaction(myIdentity.IdentityId) == null)
                    {
                        chat.Respond("ServerLimits are set PerFaction. You are not in a faction! Contact an Admin!");
                        return false;
                    }

                    if (blockLimits != null)
                    {


                        if (MySession.Static.MaxBlocksPerPlayer != 0 && blockLimits.BlocksBuilt + blocksToBuild > blockLimits.MaxBlocks)
                        {
                            chat.Respond("Cannot load grid! You would be over your Max Blocks!");
                            return false;
                        }

                        //Double check to see if the list is null
                        if (PlayerBuiltBlocks != null)
                        {
                            foreach (KeyValuePair<string, short> ServerBlockLimits in MySession.Static.BlockTypeLimits)
                            {
                                if (PlayerBuiltBlocks.ContainsKey(ServerBlockLimits.Key))
                                {
                                    int TotalNumberOfBlocks = PlayerBuiltBlocks[ServerBlockLimits.Key];

                                    if (blockLimits.BlockTypeBuilt.TryGetValue(ServerBlockLimits.Key, out MyBlockLimits.MyTypeLimitData LimitData))
                                    {
                                        //Grab their existing block count for the block limit
                                        TotalNumberOfBlocks += LimitData.BlocksBuilt;
                                    }

                                    //Compare to see if they would be over!
                                    short ServerLimit = MySession.Static.GetBlockTypeLimit(ServerBlockLimits.Key);
                                    if (TotalNumberOfBlocks > ServerLimit)
                                    {
                                        chat.Respond("Player " + myIdentity.DisplayName + " would be over their " + ServerBlockLimits.Key + " limits! " + TotalNumberOfBlocks + "/" + ServerLimit);
                                        //Player would be over their block type limits
                                        return false;
                                    }
                                }
                            }
                        }

                    }


                }
            }

            return true;
        }
        private bool LoadGridFile(string GridName, PlayerInfo Data, GridStamp Grid, bool admin = false)
        {
            
            if (Methods.LoadGrid(GridName, myCharacter, TargetIdentity, LoadFromSavePosition, chat, Plugin, Grid.GridSavePosition, true, admin))
            {

                chat.Respond("Load Complete!");
                Data.Grids.Remove(Grid);


                if (!admin)
                {
                    TimeStamp stamp = new TimeStamp();
                    stamp.OldTime = DateTime.Now;
                    stamp.PlayerID = myIdentity.IdentityId;
                    Data.Timer = stamp;
                }


                FileSaver.Save(Path.Combine(PlayerHangarPath, "PlayerInfo.json"), Data);
                //File.WriteAllText(Path.Combine(IDPath, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
                return true;
            }
            else
            {
                //chat.Respond("Load Failed!");
                return false;
            }
        }
        private bool SellOnMarket(string IDPath, GridStamp Grid, PlayerInfo Data, long NumPrice, string Description)
        {
            string path = Path.Combine(IDPath, Grid.GridName + ".sbc");
            if (!File.Exists(path))
            {
                //Context.Respond("Grid doesnt exist! Contact an admin!");
                return false;
            }


            MyObjectBuilderSerializer.DeserializeXML(path, out MyObjectBuilder_Definitions myObjectBuilder_Definitions);

            var shipBlueprint = myObjectBuilder_Definitions.ShipBlueprints;
            MyObjectBuilder_CubeGrid grid = shipBlueprint[0].CubeGrids[0];


            byte[] Definition = MyAPIGateway.Utilities.SerializeToBinary(grid);
            GridsForSale GridSell = new GridsForSale();
            GridSell.name = Grid.GridName;
            GridSell.GridDefinition = Definition;


            //Seller faction
            var fc = MyAPIGateway.Session.Factions.GetObjectBuilder();

            MyObjectBuilder_Faction factionBuilder;
            try
            {
                factionBuilder = fc.Factions.First(f => f.Members.Any(m => m.PlayerId == myIdentity.IdentityId));
                if (factionBuilder != null || factionBuilder.Tag != "")
                {
                    Grid.SellerFaction = factionBuilder.Tag;
                }
            }
            catch
            {

                try
                {
                    Hangar.Debug("Player " + myIdentity.DisplayName + " has a bugged faction model! Attempting to fix!");
                    factionBuilder = fc.Factions.First(f => f.Members.Any(m => m.PlayerId == myIdentity.IdentityId));
                    MyObjectBuilder_FactionMember member = factionBuilder.Members.First(x => x.PlayerId == myIdentity.IdentityId);

                    bool IsFounder;
                    bool IsLeader;

                    IsFounder = member.IsFounder;
                    IsLeader = member.IsLeader;

                    factionBuilder.Members.Remove(member);
                    factionBuilder.Members.Add(member);
                }
                catch (Exception a)
                {
                    Hangar.Debug("Welp tbh fix failed! Please why no fix. :(", a, Hangar.ErrorType.Trace);
                }

                //Bugged player!
            }



            MarketList List = new MarketList();
            List.Name = Grid.GridName;
            List.Description = Description;
            List.Seller = myCharacter.DisplayName;
            List.Price = NumPrice;
            List.Steamid = PlayerSteamID;
            List.MarketValue = Grid.MarketValue;
            List.SellerFaction = Grid.SellerFaction;
            List.GridMass = Grid.GridMass;
            List.SmallGrids = Grid.SmallGrids;
            List.LargeGrids = Grid.LargeGrids;
            List.StaticGrids = Grid.StaticGrids;
            List.NumberofBlocks = Grid.NumberofBlocks;
            List.MaxPowerOutput = Grid.MaxPowerOutput;
            List.GridBuiltPercent = Grid.GridBuiltPercent;
            List.JumpDistance = Grid.JumpDistance;
            List.NumberOfGrids = Grid.NumberOfGrids;
            List.BlockTypeCount = Grid.BlockTypeCount;
            List.PCU = Grid.GridPCU;
            List.GridDefinition = Definition;


            //We need to send to all to add one item to the list!
            CrossServerMessage SendMessage = new CrossServerMessage();
            SendMessage.Type = CrossServer.MessageType.AddItem;
            SendMessage.GridDefinition.Add(GridSell);
            SendMessage.List.Add(List);

            Plugin.Market.MarketServers.Update(SendMessage);
            //Plugin.MarketServers.Update(SendMessage);


            FileSaver.Save(Path.Combine(IDPath, "PlayerInfo.json"), Data);
            //File.WriteAllText(Path.Combine(IDPath, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
            return true;
        }

    }
}
