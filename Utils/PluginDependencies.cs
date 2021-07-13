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
        private static Guid BlockLimiterGUID = new Guid("11fca5c4-01b6-4fc3-a215-602e2325be2b");
        private static Guid GridBackupGUID = new Guid("75e99032-f0eb-4c0d-8710-999808ed970c");
        private static Guid NexusGUID = new Guid("28a12184-0422-43ba-a6e6-2e228611cca5");

        private static MethodInfo CheckFutureLimits;
        private static MethodInfo BackupGridBuilders;
        private static ITorchPlugin GridBackupRef;

        public static bool BlockLimiterInstalled { get; private set; } = false;
        public static bool NexusInstalled { get; private set; } = false;


        public static void InitPluginDependencies(PluginManager Plugins)
        {
            if (Plugins.Plugins.TryGetValue(GridBackupGUID, out ITorchPlugin GridBackupPlugin))
                AquireGridBackup(GridBackupPlugin);

            if (Plugins.Plugins.TryGetValue(BlockLimiterGUID, out ITorchPlugin BlockLimiterPlugin))
                AquireBlockLimiter(BlockLimiterPlugin);

            if (Plugins.Plugins.TryGetValue(NexusGUID, out ITorchPlugin NexusPlugin))
                AquireNexus(NexusPlugin);
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


        private static void AquireGridBackup(ITorchPlugin Plugin)
        {
           Type GridBackupType = DeclareInstalledPlugin(Plugin);
           BackupGridBuilders = GridBackupType.GetMethod("BackupGridsManuallyWithBuilders", BindingFlags.Public | BindingFlags.Instance, null, new Type[2] { typeof(List<MyObjectBuilder_CubeGrid>), typeof(long) }, null);
           GridBackupRef = Plugin;

           BlockLimiterInstalled = true;
        }

        private static void AquireBlockLimiter(ITorchPlugin Plugin)
        {
            Type BlockLimiterType = DeclareInstalledPlugin(Plugin);
            CheckFutureLimits = BlockLimiterType.GetMethod("CheckLimits_future");
        }

        private static void AquireNexus(ITorchPlugin Plugin)
        {
            Type NexusMain = DeclareInstalledPlugin(Plugin);
            Type ReflectedServerSideAPI = NexusMain?.Assembly.GetType("Nexus.API.PluginAPISync");

            if (ReflectedServerSideAPI == null)
                return;


            ReflectedServerSideAPI.GetMethod("ApplyPatching", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { typeof(NexusAPI), "QuantumHangar" });

            NexusSupport.Init();
            NexusInstalled = true;
        }


        public static void BackupGrid(List<MyObjectBuilder_CubeGrid> Grids, long User)
        {
            try
            {
                BackupGridBuilders?.Invoke(GridBackupRef, new object[] { Grids, User });
            }catch(Exception ex)
            {
                Log.Error(ex, "GridBackup threw an error!");
            }
        }

        public static bool CheckGridLimits(List<MyObjectBuilder_CubeGrid> Grids, long AgainstUser)
        {
            try
            {
                bool Return = (bool)CheckFutureLimits?.Invoke(null, new object[] { Grids.ToArray(), AgainstUser });
                return Return;

            }catch(Exception ex)
            {
                Log.Error(ex, "BlockLimiter threw an error!");
                return false;
            }
        }


    }
}
