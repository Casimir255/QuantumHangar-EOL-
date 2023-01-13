using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using Torch.Mod;
using Torch.Mod.Messages;

namespace QuantumHangar.Utils
{
    public class PreviewBoxTimer
    {
        //Following list keeps track of all displays to everyone on the server
        public static List<PreviewBoxTimer> list = new List<PreviewBoxTimer>();


        public int timeDisplayed = 0;
        private static int displayfor = 60; //display interferred boxes for 60 seconds

        public DrawDebug drawobjectMessage;
        private ulong _target;

        public PreviewBoxTimer(ulong target)
        {
            drawobjectMessage = new DrawDebug(target.ToString());
            _target = target;
        }

        public void display()
        {
            PreviewBoxTimer exsisting = list.FirstOrDefault(x => x == this);
            if(exsisting != null )
            {
                exsisting.remove();
                list.Remove(exsisting);
            }



            list.Add(this);
            ModCommunication.SendMessageTo(drawobjectMessage, _target);

            //clear
            drawobjectMessage.drawObjects.Clear();
        }

        public static void Update()
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].timeDisplayed > displayfor)
                {
                    list[i].remove();
                    list.RemoveAt(i);
                    continue;
                }

                list[i].timeDisplayed++;
            }
        }

        private void remove()
        {
            drawobjectMessage.remove = true;
            ModCommunication.SendMessageTo(drawobjectMessage, _target);
        }

        public override bool Equals(object obj)
        {
            if (obj is PreviewBoxTimer timer)
                return timer._target == _target;

            return false;
        }
    }
}
