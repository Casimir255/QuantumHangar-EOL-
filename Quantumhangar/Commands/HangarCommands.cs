using QuantumHangar.HangarChecks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

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
}