using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace QuantumHangar
{
    public class ParallelSpawner
    {
        private readonly int _maxCount;
        private readonly MyObjectBuilder_CubeGrid[] _grids;
        private readonly Action<HashSet<IMyCubeGrid>> _callback;
        private readonly HashSet<IMyCubeGrid> _spawned;

        public ParallelSpawner(MyObjectBuilder_CubeGrid[] grids, Action<HashSet<IMyCubeGrid>> callback = null)
        {
            _grids = grids;
            _maxCount = grids.Length;
            _callback = callback;
            _spawned = new HashSet<IMyCubeGrid>();
        }

        public void Start()
        {
            foreach (var o in _grids)
                MyAPIGateway.Entities.CreateFromObjectBuilderParallel(o, false, Increment);
        }

        public void Increment(IMyEntity entity)
        {
            var grid = (IMyCubeGrid)entity;
            _spawned.Add(grid);

            if (_spawned.Count < _maxCount)
                return;

            foreach (var g in _spawned)
            {
                MyAPIGateway.Entities.AddEntity(g, true);
            }

            _callback?.Invoke(_spawned);
        }
    }


    public class Chat
    {
        private CommandContext _context;

        public Chat(CommandContext context)
        {
            _context = context;
        }

        public void Respond(string response)
        {
            _context.Respond(response, Color.Yellow, "Hangar");
        }

        public static void Respond(string response, CommandContext context)
        {
            context.Respond(response, Color.Yellow, "Hangar");
        }
    }
}
