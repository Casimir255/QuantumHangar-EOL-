using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Utils;

namespace HangarStoreMod
{
    class Debug
    {
        private static bool EnableDebug = false;
        public static void Write(string msg)
        {
            if (EnableDebug)
            {
                MyAPIGateway.Utilities.ShowMessage("QuantumHangarMOD", msg);
                MyLog.Default.WriteLineAndConsole("QuantumHangarMOD: " + msg);
            }

        }

    }
}
