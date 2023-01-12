using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Torch.API.Plugins;
using Torch.Managers;
using VRage.Game;

namespace QuantumHangar.Utils
{
    public static class PluginDependencies
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly Guid BlockLimiterGuid = new Guid("11fca5c4-01b6-4fc3-a215-602e2325be2b");
        private static readonly Guid GridBackupGuid = new Guid("75e99032-f0eb-4c0d-8710-999808ed970c");
        private static readonly Guid NexusGuid = new Guid("28a12184-0422-43ba-a6e6-2e228611cca5");

        private static MethodInfo _checkFutureLimits;
        private static MethodInfo _backupGridBuilders;
        private static ITorchPlugin _gridBackupRef;

        public static bool BlockLimiterInstalled { get; private set; }
        public static bool NexusInstalled { get; private set; } = false;


        public static void InitPluginDependencies(PluginManager plugins)
        {
            if (plugins.Plugins.TryGetValue(GridBackupGuid, out var gridBackupPlugin))
                AcquireGridBackup(gridBackupPlugin);

            if (plugins.Plugins.TryGetValue(BlockLimiterGuid, out var blockLimiterPlugin))
                AcquireBlockLimiter(blockLimiterPlugin);

            //if (plugins.Plugins.TryGetValue(NexusGuid, out var nexusPlugin))
            //    AcquireNexus(nexusPlugin);
        }

        public static void Dispose()
        {
            if (NexusInstalled) NexusSupport.Dispose();
        }

        private static Type DeclareInstalledPlugin(ITorchPlugin plugin)
        {
            Log.Info("Plugin: " + plugin.Name + " " + plugin.Version + " is installed!");
            return plugin.GetType();
        }


        private static void AcquireGridBackup(ITorchPlugin plugin)
        {
            var gridBackupType = DeclareInstalledPlugin(plugin);
            _backupGridBuilders = gridBackupType.GetMethod("BackupGridsManuallyWithBuilders",
                BindingFlags.Public | BindingFlags.Instance, null,
                new Type[2] { typeof(List<MyObjectBuilder_CubeGrid>), typeof(long) }, null);
            _gridBackupRef = plugin;

            BlockLimiterInstalled = true;
        }

        private static void AcquireBlockLimiter(ITorchPlugin plugin)
        {
            var blockLimiterType = DeclareInstalledPlugin(plugin);
            _checkFutureLimits = blockLimiterType.GetMethod("CheckLimits_future");
        }

        private static void AcquireNexus(ITorchPlugin plugin)
        {
            var nexusMain = DeclareInstalledPlugin(plugin);
            var reflectedServerSideApi = nexusMain?.Assembly.GetType("Nexus.API.PluginAPISync");

            if (reflectedServerSideApi == null)
                return;


            reflectedServerSideApi.GetMethod("ApplyPatching", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, new object[] { typeof(NexusApi), "QuantumHangar" });

            NexusSupport.Init();
            NexusInstalled = true;
        }


        public static void BackupGrid(List<MyObjectBuilder_CubeGrid> grids, long user)
        {
            try
            {
                _backupGridBuilders?.Invoke(_gridBackupRef, new object[] { grids, user });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GridBackup threw an error!");
            }
        }

        public static bool CheckGridLimits(List<MyObjectBuilder_CubeGrid> grids, long againstUser)
        {
            try
            {
                var @return = (bool)_checkFutureLimits?.Invoke(null, new object[] { grids.ToArray(), againstUser })!;
                return @return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "BlockLimiter threw an error!");
                return false;
            }
        }
    }
}