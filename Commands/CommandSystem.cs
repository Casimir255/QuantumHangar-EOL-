using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace QuantumHangar.Commands
{
    public static class CommandSystem
    {

        /* Need an action system/Queue*/


        public static void RunTask(Action Invoker)
        {
            Task Run = new Task(Invoker);
            Run.Start();
        }

        



    }
}
