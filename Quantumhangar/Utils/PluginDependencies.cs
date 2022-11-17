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
        public static bool NexusInstalled { get; private set; }


        public static void InitPluginDependencies(PluginManager Plugins)
        {
            if (Plugins.Plugins.TryGetValue(GridBackupGuid, out var GridBackupPlugin))
                AcquireGridBackup(GridBackupPlugin);

            if (Plugins.Plugins.TryGetValue(BlockLimiterGuid, out var BlockLimiterPlugin))
                AcquireBlockLimiter(BlockLimiterPlugin);

            if (Plugins.Plugins.TryGetValue(NexusGuid, out var NexusPlugin))
                AcquireNexus(NexusPlugin);
        }

        public static void Dispose()
        {
            if (NexusInstalled) NexusSupport.Dispose();
        }

        private static Type DeclareInstalledPlugin(ITorchPlugin Plugin)
        {
            Log.Info("Plugin: " + Plugin.Name + " " + Plugin.Version + " is installed!");
            return Plugin.GetType();
        }


        private static void AcquireGridBackup(ITorchPlugin Plugin)
        {
            var GridBackupType = DeclareInstalledPlugin(Plugin);
            _backupGridBuilders = GridBackupType.GetMethod("BackupGridsManuallyWithBuilders",
                BindingFlags.Public | BindingFlags.Instance, null,
                new Type[2] { typeof(List<MyObjectBuilder_CubeGrid>), typeof(long) }, null);
            _gridBackupRef = Plugin;

            BlockLimiterInstalled = true;
        }

        private static void AcquireBlockLimiter(ITorchPlugin Plugin)
        {
            var BlockLimiterType = DeclareInstalledPlugin(Plugin);
            _checkFutureLimits = BlockLimiterType.GetMethod("CheckLimits_future");
        }

        private static void AcquireNexus(ITorchPlugin Plugin)
        {
            var NexusMain = DeclareInstalledPlugin(Plugin);
            var ReflectedServerSideAPI = NexusMain?.Assembly.GetType("Nexus.API.PluginAPISync");

            if (ReflectedServerSideAPI == null)
                return;


            ReflectedServerSideAPI.GetMethod("ApplyPatching", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, new object[] { typeof(NexusAPI), "QuantumHangar" });

            NexusSupport.Init();
            NexusInstalled = true;
        }


        public static void BackupGrid(List<MyObjectBuilder_CubeGrid> Grids, long User)
        {
            try
            {
                _backupGridBuilders?.Invoke(_gridBackupRef, new object[] { Grids, User });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GridBackup threw an error!");
            }
        }

        public static bool CheckGridLimits(List<MyObjectBuilder_CubeGrid> Grids, long AgainstUser)
        {
            try
            {
                var Return = (bool)_checkFutureLimits?.Invoke(null, new object[] { Grids.ToArray(), AgainstUser })!;
                return Return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "BlockLimiter threw an error!");
                return false;
            }
        }
    }
}