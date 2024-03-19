using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.World;
using Torch.Commands;

namespace QuantumHangar.Utils
{
    public static class CommandCooldownChecker
    {
        public static bool FailsAlliancePreChecks(CommandContext Context, out Guid allianceId)
        {
            allianceId = Guid.Empty;
            if (Hangar.Alliances == null)
            {
                Context?.Respond("Alliances is not installed!");
                return true;
            }

            var faction = MySession.Static.Factions.GetPlayerFaction(Context.Player.IdentityId);
            if (faction == null)
            {
                Context.Respond("Players without a faction cannot use alliance hanger!");
                return true;
            }

            var methodInput = new object[] { faction.Tag };

            allianceId = (Guid)(Hangar.GetAllianceId?.Invoke(null, methodInput));
            if (allianceId == null || allianceId == Guid.Empty)
            {
                Context?.Respond("Players without an alliance cannot use alliance hanger!");
                return true;
            }

            if (Hangar.AllianceAttempts.TryGetValue(allianceId, out var timer))
            {
                if (DateTime.Now < timer)
                {
                    Context.Respond("Cannot use this for 5 seconds.");
                    return true;
                }

                Hangar.AllianceAttempts[allianceId] = DateTime.Now.AddSeconds(5);
            }
            else
            {
                Hangar.AllianceAttempts[allianceId] = DateTime.Now.AddSeconds(5);
            }

            return false;
        }

        public static bool FailsFactionPreChecks(CommandContext Context)
        {
            var playersFaction = MySession.Static.Factions.TryGetPlayerFaction(Context.Player.IdentityId);
            if (playersFaction == null)
            {
                Context.Respond("Need a faction to use faction hangar.");
                return true;
            }

            if (Hangar.FactionAttempts.TryGetValue(playersFaction.FactionId, out var timer))
            {
                if (DateTime.Now < timer)
                {
                    Context.Respond("Cannot use this for 5 seconds.");
                    return true;
                }

                Hangar.FactionAttempts[playersFaction.FactionId] = DateTime.Now.AddSeconds(5);
            }
            else
            {
                Hangar.FactionAttempts[playersFaction.FactionId] = DateTime.Now.AddSeconds(5);
            }

            return false;
        }
    }
}
