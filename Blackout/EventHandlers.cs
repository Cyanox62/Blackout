using Exiled.API.Features;
using System.Linq;
using Exiled.Permissions.Extensions;
using Exiled.Events.EventArgs;
using MEC;

namespace Blackout
{
    public partial class EventHandlers
    {
        public void OnRoundStart()
        {
            if (!Blackout.activeNextRound && !Blackout.toggled) return;

            Blackout.active = true;
			Blackout.activeNextRound = false;
		    isRoundStarted = false;

            GamePrep();

            // Inform players
            Map.Broadcast(10, BroadcastExplanation);
            foreach (Player player in Player.List) player.SendConsoleMessage(ConsoleExplaination, "yellow");

            const float cassieDelay = 8.6f;
            const float flickerDelay = 0.4f;

            // Announcements
            Timing.CallDelayed(Blackout.instance.Config.StartDelay - (cassieDelay + flickerDelay), () =>
            {
                Cassie.Message("LIGHT SYSTEM SCP079RECON6", true, true);

                // Blackout
                Timing.CallDelayed(cassieDelay, () =>
                {
                    coroutines.Add(Timing.RunCoroutine(BlackoutLoop()));

                    // Change role and teleport players
                    Timing.CallDelayed(flickerDelay, () => StartGame()); // 0.4 IS VERY SPECIFIC. DO NOT CHANGE, MAY CAUSE LIGHT FLICKER TO NOT CORRESPOND WITH ROLE CHANGE
                }); // 8.6 IS VERY SPECIFIC. DO NOT CHANGE, MAY CAUSE BLACKOUT TO BE UNCOORDINATED WITH CASSIE
            }); // Cassie and flicker delay is subtracted in order to start the round by that time
        }

        public void OnCheckRoundEnd(EndingRoundEventArgs ev)
        {
            if (Blackout.active && !isRoundStarted)
            {
                ev.IsAllowed = false;
            }
        }

        public void OnRACommand(SendingRemoteAdminCommandEventArgs ev)
        {
            string cmd = ev.Name.ToLower();
            if (cmd == "blackout")
            {
                ev.IsAllowed = false;
                if (ev.Sender.CheckPermission("bo.toggle"))
                {
                    Blackout.toggled = !Blackout.toggled;
                    Blackout.activeNextRound = Blackout.toggled;
                    ev.ReplyMessage = $"Blackout has been toggled {(Blackout.toggled ? "on" : "off")}.";
                    ev.Success = true;
                }
                else
                {
                    ev.ReplyMessage = "You do not have permission to run this command.";
                    ev.Success = false;
                }
            }
        }

        public void OnRoundRestart()
        {
            Blackout.active = false;

            Timing.KillCoroutines(coroutines);
            coroutines.Clear();
        }

        public void OnDoorAccess(InteractingDoorEventArgs ev)
        {
            if (Blackout.active)
            {
				if (isRoundStarted)
				{
					switch (ev.Door.DoorName)
					{
						case "CHECKPOINT_ENT":
							ev.Door.destroyed = false;
							break;

						case "HCZ_ARMORY":
							if ((escapeReady || (escapeReady = Generator079.Generators.All(x => x.remainingPowerup <= 0))) && //if escape is known to be ready, and if not check if it is
								ev.Player.Role == RoleType.Scientist)
							{
								EscapeScientist(ev.Player);
							}
							goto case "CHECKPOINT_ENT";
					}
				}
				else
				{
					ev.Door.NetworkisOpen = false;
				}
            }
        }

        public void OnPlayerTriggerTesla(TriggeringTeslaEventArgs ev)
        {
            if (Blackout.active && Blackout.instance.Config.TeslaFlicker)
            {
                ev.IsTriggerable = false;
            }
        }

        public void OnPlayerHurt(HurtingEventArgs ev)
        {
            if (Blackout.active && ev.DamageType == DamageTypes.Nuke && ev.Target.Team == Team.SCP)
            {
                ev.Amount = 0;
            }
        }

        public void OnSetRole(ChangingRoleEventArgs ev)
		{
			if (Blackout.active && ev.Player.Role == RoleType.Scientist)
			{
				SpawnScientist(ev.Player, true, true);
			}
		}

        public void OnTeamRespawn(RespawningTeamEventArgs ev)
        {
            if (Blackout.active)
            {
                ev.NextKnownTeam = Respawning.SpawnableTeamType.ChaosInsurgency;
                ev.Players.Clear();
            }
        }

        public void OnSpawn(SpawningEventArgs ev)
        {
            if (Blackout.active && ev.Player.Role == RoleType.Scientist)
            {
                ev.Position = Map.GetRandomSpawnPoint(RoleType.Scp049);
            }
        }

        public void OnRecallZombie(StartingRecallEventArgs ev) => ev.IsAllowed = !Blackout.active;
    }
}
