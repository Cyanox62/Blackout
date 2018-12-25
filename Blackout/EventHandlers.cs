﻿using System.Collections.Generic;
using System.Linq;
using scp4aiur;
using Smod2;
using Smod2.API;
using Smod2.EventHandlers;
using Smod2.Events;
using Smod2.EventSystem.Events;
using UnityEngine;
using Item = Smod2.API.Item;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Blackout
{
    public class EventHandlers : IEventHandlerRoundStart, IEventHandlerDoorAccess, IEventHandlerTeamRespawn,
        IEventHandlerPlayerHurt, IEventHandlerSummonVehicle, IEventHandlerWarheadStopCountdown, 
        IEventHandlerRoundRestart, IEventHandlerPlayerTriggerTesla, IEventHandlerPlayerDie, 
        IEventHandlerElevatorUse, IEventHandlerWarheadStartCountdown
    {
        private static int curRound;
        private bool escapeReady;
        private Broadcast broadcast;
        private MTFRespawn cassie;
        private string[] activeGenerators;

        public EventHandlers()
        {
            curRound = int.MinValue;
        }

        public void OnRoundStart(RoundStartEvent ev)
        {
            Plugin.validRanks = Plugin.instance.GetConfigList("bo_ranks");
            Plugin.giveFlashlights = Plugin.instance.GetConfigBool("bo_flashlights");
            Plugin.percentLarrys = Plugin.instance.GetConfigFloat("bo_slendy_percent");
            Plugin.maxTime = Plugin.instance.GetConfigInt("bo_max_time");
            Plugin.respawnTime = Plugin.instance.GetConfigInt("bo_respawn_time");
            Plugin.uspTime = Plugin.instance.GetConfigInt("bo_usp_time");

            if (Plugin.activeNextRound)
            {
                Plugin.active = true;
                Plugin.activeNextRound = false;

                foreach (Elevator elevator in ev.Server.Map.GetElevators())
                {
                    switch (elevator.ElevatorType)
                    {
                        case ElevatorType.LiftA:
                        case ElevatorType.LiftB:
                            if (elevator.ElevatorStatus == ElevatorStatus.Down)
                            {
                                elevator.Use();
                            }
                            break;
                    }
                }

                Smod2.API.Item[] guardCards = ev.Server.Map.GetItems(ItemType.GUARD_KEYCARD, true)
                    .Concat(ev.Server.Map.GetItems(ItemType.SENIOR_GUARD_KEYCARD, true)).ToArray();

                foreach (Smod2.API.Item item in guardCards)
                {
                    ev.Server.Map.SpawnItem(ItemType.MTF_COMMANDER_KEYCARD, item.GetPosition(), Vector.Zero);
                    item.Remove();
                }

                List<Player> players = ev.Server.GetPlayers();
                List<Player> possibleSlendies = players.ToList();
                int larryCount = Mathf.FloorToInt(players.Count * Plugin.percentLarrys);
                if (larryCount == 0)
                {
                    larryCount = 1;
                }

                Player[] slendies = new Player[larryCount];
                for (int i = 0; i < larryCount; i++)
                {
                    slendies[i] = possibleSlendies[Random.Range(0, possibleSlendies.Count)];
                    possibleSlendies.Remove(slendies[i]);
                }

                foreach (Player player in possibleSlendies)
                {
                    ev.Server.Round.Stats.ClassDEscaped = 0;

                    player.ChangeRole(Role.SCIENTIST, false, false);
                    foreach (Smod2.API.Item item in player.GetInventory())
                    {
                        item.Remove();
                    }

                    player.GiveItem(ItemType.SCIENTIST_KEYCARD);
                    player.GiveItem(ItemType.RADIO);
                    player.GiveItem(ItemType.WEAPON_MANAGER_TABLET);

                    player.Teleport(ev.Server.Map.GetRandomSpawnPoint(Role.SCP_049));
                }

                if (Plugin.giveFlashlights)
                {
                    foreach (Player player in possibleSlendies)
                    {
                        player.GiveItem(ItemType.FLASHLIGHT);
                    }
                }

                List<Role> availableSpawns = Plugin.larrySpawnPoints.ToList();
                foreach (Player player in slendies)
                {
                    Role spawnRole = availableSpawns[Random.Range(0, availableSpawns.Count)];
                    availableSpawns.Remove(spawnRole);
                    if (availableSpawns.Count == 0)
                    {
                        availableSpawns = Plugin.larrySpawnPoints.ToList();
                    }

                    player.ChangeRole(Role.SCP_049);
                    player.Teleport(ev.Server.Map.GetRandomSpawnPoint(spawnRole));
                }

                broadcast = Object.FindObjectOfType<Broadcast>();
                cassie = PlayerManager.localPlayer.GetComponent<MTFRespawn>();

                broadcast.CallRpcAddElement("This is Blackout, a custom gamemode. If you have never played it, please press [`] or [~] for more info.", 10, false);
                foreach (Player player in players)
                {
                    player.SendConsoleMessage("\nWelcome to Blackout!\n" +
                                              "In Blackout, you're either a scientist or 049. All the lights will turn off and exits have been locked.\n" +
                                              "The only way to get out is by activating all the 079 generators, then going to the Heavy Containment Zone armory (that 3 way intersection with the chasm beneath it).\n" +
                                              "Commander keycards will spawn in 096 (like usual) and nuke.");
                }

                int timerRound = curRound;
                Timing.In(inaccuracy =>
                {
                    if (timerRound == curRound)
                    {
                        cassie.CallRpcPlayCustomAnnouncement("LIGHT SYSTEM SCP079RECON6", false);

                        Timing.In(x => TenSecondBlackout(timerRound, x), 8.7f + inaccuracy);
                    }
                }, 30f);

                ev.Server.Map.GetItems(ItemType.MICROHID, true).First().Remove();

                Smod2.API.Item rifleSpawn = ev.Server.Map.GetItems(ItemType.E11_STANDARD_RIFLE, true).First();
                Vector nukeWepSpawn = rifleSpawn.GetPosition();
                rifleSpawn.Remove();
                Timing.In(x =>
                {
                    if (timerRound == curRound)
                    {
                        cassie.CallRpcPlayCustomAnnouncement("WEAPON NOW AVAILABLE", false);

                        ev.Server.Map.SpawnItem(ItemType.USP, nukeWepSpawn, Vector.Zero);
                    }
                }, Plugin.uspTime);

                activeGenerators = new string[0];

                Timing.In(x => RefreshGenerators(curRound, x), 1f);
                Timing.In(x => AnnounceTime(Plugin.maxTime - 1, curRound, x), 60);
            }
        }

        private void AnnounceTime(int minutes, int timerRound, float inaccuracy = 0)
        {
            if (timerRound == curRound)
            {
                string cassieLine = $"{minutes} MINUTE{(minutes == 1 ? "" : "S")} REMAINING";

                if (minutes == 1)
                {
                    cassieLine += " . ALPHA WARHEAD AUTOMATIC REACTIVATION SYSTEM ENGAGED.";
                    const float cassieDelay = 8f;

                    Timing.In(x =>
                    {
                        AlphaWarheadController.host.NetworktimeToDetonation = 60 - cassieDelay + inaccuracy;
                        AlphaWarheadController.host.StartDetonation();
                    }, cassieDelay);
                }
                else
                {
                    Timing.In(x => AnnounceTime(--minutes, timerRound, x), 60 + inaccuracy);
                }

                cassie.CallRpcPlayCustomAnnouncement(cassieLine, false);
            }
        }
        
        private void RefreshGenerators(int timerRound, float inaccuracy = 0)
        {
            if (timerRound != curRound)
            {
                return;
            }

            Timing.In(x => RefreshGenerators(timerRound, x), 5 + inaccuracy);

            string[] newActiveGenerators =
                Generator079.generators.Where(x => x.isTabletConnected).Select(x => x.curRoom).ToArray();

            if (!activeGenerators.SequenceEqual(newActiveGenerators))
            {
                foreach (string generator in newActiveGenerators.Except(activeGenerators))
                {
                    broadcast.CallRpcAddElement($"{generator} engaging", 5, false);
                }

                activeGenerators = newActiveGenerators;
            }
        }

        private void TenSecondBlackout(int timerRound, float inaccuracy = 0)
        {
            if (timerRound == curRound)
            {
                Timing.In(x => TenSecondBlackout(timerRound, x), 11 + inaccuracy);

                Generator079.generators[0].CallRpcOvercharge();
            }
        }

        public void OnDoorAccess(PlayerDoorAccessEvent ev)
        {
            if (Plugin.active)
            {
                switch (ev.Door.Name)
                {
                    case "CHECKPOINT_ENT":
                        ev.Allow = false;
                        ev.Destroy = false;
                        break;

                    case "HCZ_ARMORY":
                        if ((escapeReady || (escapeReady = Generator079.generators.All(x => x.remainingPowerup <= 0))) && //if escape is known to be ready, and if not check if it is
                            ev.Player.TeamRole.Role == Role.SCIENTIST)
                        {
                            ev.Player.SetRank("silver", "ESCAPED");

                            foreach (Smod2.API.Item item in ev.Player.GetInventory()) //drop items before converting
                            {
                                item.Drop();
                            }
                            ev.Player.ChangeRole(Role.NTF_SCIENTIST, false, false, false); //convert empty, ready to give items

                            PluginManager.Manager.Server.Round.Stats.ScientistsEscaped++;
                        }
                        goto case "CHECKPOINT_ENT";
                }
            }
        }

        public void OnTeamRespawn(TeamRespawnEvent ev)
        {
            if (Plugin.active)
            {
                ev.SpawnChaos = true;
                ev.PlayerList.Clear();
            }
        }

        public void OnPlayerHurt(PlayerHurtEvent ev)
        {
            if (Plugin.active && (ev.DamageType == DamageType.SCP_049 || ev.DamageType == DamageType.SCP_049_2))
            {
                ev.Damage = 99999f;
            }
        }

        public void OnSummonVehicle(SummonVehicleEvent ev)
        {
            if (Plugin.active)
            {
                ev.AllowSummon = false;
            }
        }

        public void OnStopCountdown(WarheadStopEvent ev)
        {
            if (Plugin.active)
            {
                ev.Cancel = true;
            }
        }

        public void OnRoundRestart(RoundRestartEvent ev)
        {
            Plugin.active = false;
            Plugin.respawnActive = false;

            curRound++;
        }

        public void OnPlayerTriggerTesla(PlayerTriggerTeslaEvent ev)
        {
            if (Plugin.active && ev.Player.TeamRole.Role == Role.SCIENTIST)
            {
                ev.Triggerable = false;
            }
        }

        public void OnPlayerDie(PlayerDeathEvent ev)
        {
            if (Plugin.active && ev.Player.TeamRole.Role == Role.SCIENTIST)
            {
                Timing.In(inaccuracy =>
                {
                    if (Plugin.active && Plugin.respawnActive)
                    {
                        ev.Player.ChangeRole(Role.SCIENTIST, false, false, false);
                        ev.Player.Teleport(PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_049));
                    }
                }, Plugin.respawnTime);
            }
        }

        public void OnElevatorUse(PlayerElevatorUseEvent ev)
        {
            if (Plugin.active)
            {
                switch (ev.Elevator.ElevatorType)
                {
                    case ElevatorType.LiftA:
                    case ElevatorType.LiftB:
                        ev.AllowUse = false;
                        break;
                }
            }
        }

        public void OnStartCountdown(WarheadStartEvent ev)
        {
            if (Plugin.active)
            {
                ev.OpenDoorsAfter = false;
            }
        }
    }
}
