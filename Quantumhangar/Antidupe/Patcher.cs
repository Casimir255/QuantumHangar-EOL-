using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Torch.Managers.PatchManager;

namespace QuantumHangar.Utilities
{
    public class Patcher
    {
        private static Hangar Plugin { get; set; }



        public void Apply(PatchContext ctx, Hangar plugin)
        {
            var SaveMethod = typeof(MySession).GetMethod("Save", BindingFlags.Public | BindingFlags.Instance, null,
            new Type[] { typeof(MySessionSnapshot).MakeByRefType(), typeof(string) }, null);
            if (SaveMethod == null)
            {
                throw new InvalidOperationException("Couldn't find Save");
            }
            ctx.GetPattern(SaveMethod).Suffixes.Add(Method(nameof(AfterSave)));
            Plugin = plugin;
        }

        private static MethodInfo Method(string name)
        {
            return typeof(Patcher).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        }

        private static void AfterSave(bool __result)
        {
            if (__result)
            {

            }
        }
    }



}

