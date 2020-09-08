using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

using System.Collections.Generic;
using System.Linq;
using MEC;
using Exiled.API.Features;
using Exiled.API.Enums;

namespace Blackout
{
    public partial class EventHandlers
    {
        private const string BroadcastExplanation = "<size=60><b>Blackout</b></size>\n<i>Press [`] or [~] for info on how to play.</i>";
        private const string ConsoleExplaination =
            "\nWelcome to Blackout!\n" +
            "In Blackout, you're either a scientist or SCP-049. All the lights will turn off and exits have been locked. " +
            "The only way to get out is by activating all the 079 generators, then going to the Heavy Containment Zone armory " +
            "(that 3 way intersection with the chasm beneath it). " +
            "Commander keycards will replace all existing keycards. When you escape, you will be given weapons to kill all SCP-049s. " +
            "Eliminate all of them before the nuke detonates for a scientist win.";

        private const float Cassie049BreachDelay = 8.25f;

        private bool isRoundStarted;
        private bool escapeReady;

        private Dictionary<Player, Vector3> ghostSpawns;
        private (Player[] ghosts, List<Player> scientists) randomizedPlayers;
        private Vector3[] uspRespawns;

        private Generator079[] activeGenerators;
        private Dictionary<Generator079, float> generatorTimes;

        private List<CoroutineHandle> coroutines = new List<CoroutineHandle>();

        private void GamePrep()
        {
            Pickup[] pickups = Object.FindObjectsOfType<Pickup>();

            uspRespawns = UspSpawnPoints(pickups).ToArray();

            UpdateItems(pickups);
            SetMapBoundaries();

            randomizedPlayers = RandomizePlayers();

            // Set every class to scientist
            foreach (Player player in Player.List)
            {
                SpawnScientist(player, false, false);
                SetItems(player, Blackout.instance.Config.WaitingRoomItems);
            }

            // Set 049 spawn points
            ghostSpawns = GenerateSpawnPoints(randomizedPlayers.ghosts);
            activeGenerators = new Generator079[0];
            generatorTimes = Generator079.Generators.ToDictionary(x => x, x => Blackout.instance.Config.GeneratorTime);
        }

        private void StartGame()
        {
            // Begins looping to display active generators
            RefreshGeneratorsLoop();

            int maxTimeMinutes = Mathf.FloorToInt(Blackout.instance.Config.MaxTime / 60);
            float remainder = Blackout.instance.Config.MaxTime - maxTimeMinutes * 60;
            Timing.CallDelayed(remainder, () => AnnounceTimeLoops(maxTimeMinutes - 1));

            ImprisonSlendies(randomizedPlayers.ghosts);

            foreach (Player player in randomizedPlayers.scientists)
                SetItems(player, Blackout.instance.Config.StartItems);
            
            Timing.CallDelayed(Blackout.instance.Config.GhostDelay - Cassie049BreachDelay, () => FreeGhosts(ghostSpawns));
            UpdateUspRespawns(uspRespawns);

            Timing.CallDelayed(0.3f, () => isRoundStarted = true);
        }

        private void SetMapBoundaries()
        {
            // Lock LCZ elevators
            foreach (Lift elevator in Map.Lifts)
            {
                switch (elevator.elevatorName)
                {
                    case "SCP-049" when elevator.status == Lift.Status.Up:
                    case "ElA" when elevator.status == Lift.Status.Down:
                    case "ElB" when elevator.status == Lift.Status.Down:
                    case "ElA2" when elevator.status == Lift.Status.Down:
                    case "ElB2" when elevator.status == Lift.Status.Down:
                        elevator.UseLift();
                        break;
                }
            }

            Map.Doors.First(x => x.DoorName == "CHECKPOINT_ENT").locked = true;
            Map.Doors.First(x => x.DoorName == "HCZ_ARMORY").locked = true;

            Warhead.IsLocked = true;
        }

        private static void UpdateItems(Pickup[] pickups)
        {
            // Delete all micro HIDs or USPs
            foreach (Pickup gun in pickups.Where(x => 
                x.ItemId == ItemType.MicroHID ||
                x.ItemId == ItemType.GunUSP ||
                x.ItemId == ItemType.GunE11SR
                ))
                gun.Delete();

            foreach (Pickup keycard in pickups.Where(x => -1 < (int)x.ItemId && (int)x.ItemId < 12)) // All keycard items
            {
                keycard.ItemId = ItemType.KeycardNTFCommander;
            }
        }

        private static IEnumerable<Vector3> UspSpawnPoints(IEnumerable<Pickup> allPickups)
        {
            return allPickups.Where(x => x.ItemId == ItemType.GunE11SR).Select(x => x.transform.position);
        }

        private void UpdateUspRespawns(IEnumerable<Vector3> spawns)
        {
            GameObject host = GameObject.Find("Host");
            Inventory inventory = host.GetComponent<Inventory>();
            WeaponManager.Weapon usp = host.GetComponent<WeaponManager>().weapons.First(x => x.inventoryID == ItemType.GunUSP);

            Timing.CallDelayed(Blackout.instance.Config.UspTime, () =>
            {
                Cassie.Message("U S P NOW AVAILABLE", true, true);

                PlayerMovementSync.FindSafePosition(Map.Doors.FirstOrDefault(x => x.DoorName.Trim() == "NUKE_ARMORY").transform.position, out Vector3 safepos, true);
                {
                    // Spawn USPs with random sight, heavy barrel, and flashlight :ok_hand:
                    inventory.SetPickup(ItemType.GunUSP, usp.maxAmmo, safepos, Quaternion.Euler(0, 0, 0), Random.Range(0, usp.mod_sights.Length), 2, 1);
                }
            });
        }

        private Dictionary<Player, Vector3> GenerateSpawnPoints(IEnumerable<Player> ghosts)
        {
            List<RoleType> availableSpawns = Blackout.ghostSpawnPoints.ToList();
            return ghosts.ToDictionary(x => x, x =>
            {
                // Get role and remove it from pool
                RoleType spawnRole = availableSpawns[Random.Range(0, availableSpawns.Count)];
                availableSpawns.Remove(spawnRole);

                // Fill pool if it overflows
                if (availableSpawns.Count == 0)
                {
                    availableSpawns.AddRange(Blackout.ghostSpawnPoints);
                }

                // Set point to random point from role
                return Map.GetRandomSpawnPoint(spawnRole);
            });
        }

        private (Player[] ghosts, List<Player> scientists) RandomizePlayers()
        {
            List<Player> possibleGhosts = Player.List.ToList();

            // Get percentage of 049s based on players
            int ghostCount = Mathf.FloorToInt(possibleGhosts.Count * Blackout.instance.Config.GhostPercent / 100);
            if (ghostCount == 0)
                ghostCount = 1;

            // Get random 049s
            Player[] ghosts = new Player[ghostCount];
            for (int i = 0; i < ghostCount; i++)
            {
                ghosts[i] = possibleGhosts[Random.Range(0, possibleGhosts.Count)];
                possibleGhosts.Remove(ghosts[i]);
            }
            return (ghosts, possibleGhosts);
        }

        private void ImprisonSlendies(IEnumerable<Player> ghosts)
        {
            foreach (Player ghost in ghosts)
            {
                ghost.SetRole(RoleType.Scp049);

                //Teleport to 106 as a prison
                Timing.CallDelayed(0.3f, () => ghost.Position = Map.GetRandomSpawnPoint(RoleType.Scp106));

                //ghost.Broadcast(10, $"You will be released in {(int)(Blackout.instance.Config.GhostDelay - Cassie049BreachDelay)} seconds");
            }
        }

        private void FreeGhosts(Dictionary<Player, Vector3> ghosts)
        {
            Cassie.Message("CAUTION . SCP 0 4 9 CONTAINMENT BREACH IN PROGRESS", true, true);

            Timing.CallDelayed(Cassie049BreachDelay, () =>
            {
                foreach (KeyValuePair<Player, Vector3> ghost in ghosts) ghost.Key.Position = ghost.Value;
            });
        }

        private void SpawnScientist(Player player, bool isScientist, bool initInv)
        {
            Timing.CallDelayed(0.3f, () =>
            {
                if (!isScientist) player.SetRole(RoleType.Scientist);

                if (!isRoundStarted)
                {
                    SetItems(player, Blackout.instance.Config.WaitingRoomItems);
                }
                else if (initInv)
                {
                    SetItems(player, Blackout.instance.Config.StartItems);
                }
            });
        }

        private void GiveItems(Player player, IEnumerable<int> items)
        {
            WeaponManager manager = player.GameObject.GetComponent<WeaponManager>();

            foreach (int item in items)
            {
                int i = WeaponManagerIndex(manager, item);

                if (item < 31)
                {
                    int flashlight;

                    switch (item)
                    {
                        case (int)ItemType.GunE11SR:
                            flashlight = 4;
                            break;

                        case (int)ItemType.GunProject90:
                        case (int)ItemType.GunUSP:
                        case (int)ItemType.GunCOM15:
                            flashlight = 1;
                            break;

                        default:
                            player.AddItem((ItemType)item);
                            continue;
                    }

                    player.Inventory.AddNewItem((ItemType)item, manager.weapons[i].maxAmmo, manager.modPreferences[i, 0], manager.modPreferences[i, 1], flashlight);
                }
            }
        }

        private void RemoveItems(Player player) => player.ClearInventory();

        private void SetItems(Player player, IEnumerable<int> items)
        {
            RemoveItems(player);
            GiveItems(player, items);
        }

        private void EscapeScientist(Player player)
        {
            if (!string.IsNullOrEmpty(player.ReferenceHub.serverRoles.HiddenBadge)) player.BadgeHidden = false;

            string rank = player.RankName;
            player.RankName = $"(ESCAPED){(string.IsNullOrWhiteSpace(rank) ? "" : $" {rank}")}";

            player.Broadcast(5, "<b><size=60>You have escaped!</size></b>\n<i>Use your weapons to kill SCP-049!</i>");

            // Drop items before converting
            player.Inventory.ServerDropAll();

            SetItems(player, Blackout.instance.Config.EscapeItems);

            player.Ammo[(int)AmmoType.Nato556] = 250;
            player.Ammo[(int)AmmoType.Nato762] = 250;
            player.Ammo[(int)AmmoType.Nato9] = 250;

            RoundSummary.escaped_scientists++;
        }

        private static int WeaponManagerIndex(WeaponManager manager, int item)
        {
            // Get weapon index in WeaponManager
            int weapon = -1;
            for (int i = 0; i < manager.weapons.Length; i++)
            {
                if ((int)manager.weapons[i].inventoryID == item)
                {
                    weapon = i;
                }
            }

            return weapon;
        }

        private void AnnounceTimeLoops(int minutes)
        {
            if (minutes == 0)
            {
                return;
            }

            string cassieLine = Blackout.instance.Config.AnnounceTimes.Contains(minutes) ? $"{minutes} MINUTE{(minutes == 1 ? "" : "S")} REMAINING" : "";

            if (minutes == 1)
            {
                if (!string.IsNullOrWhiteSpace(cassieLine))
                {
                    cassieLine += " . ";
                }

                cassieLine += "ALPHA WARHEAD AUTOMATIC REACTIVATION SYSTEM ENGAGED";
                const float nukeStart = 50f; // Makes sure that the nuke starts when the siren is almost silent so it sounds like it just started

                Timing.CallDelayed(60 - nukeStart, () =>
                {
                    Warhead.Start();
                    Warhead.DetonationTimer = nukeStart;
                });
            }
            else
            {
                Timing.CallDelayed(60, () => AnnounceTimeLoops(--minutes));
            }

            if (!string.IsNullOrWhiteSpace(cassieLine))
            {
                Cassie.Message(cassieLine, true, true);
            }
        }

        private static string GetGeneratorName(string roomName)
        {
            Log.Warn(roomName);
            roomName = roomName.Substring(4).Trim().ToUpper();

            if (roomName.Length > 0 && (roomName[0] == '$' || roomName[0] == '!'))
                roomName = roomName.Substring(1);

            switch (roomName)
            {
                case "457":
                    return "096";

                case "ROOM3AR":
                    return "ARMORY";

                case "TESTROOM":
                    return "939";

                case "EZ_CHECKPOINT":
                    return "CHECKPOINT";

                default:
                    return roomName;
            }
        }

        private void RefreshGeneratorsLoop()
        {
            Generator079[] currentActiveGenerators = Generator079.Generators.Where(x => x.NetworkisTabletConnected).ToArray();

            if (!activeGenerators.SequenceEqual(currentActiveGenerators))
            {
                Generator079[] newlyActivated = currentActiveGenerators.Except(activeGenerators).ToArray();
                Generator079[] newlyShutdown = activeGenerators.Except(currentActiveGenerators).ToArray();
                activeGenerators = currentActiveGenerators;

                foreach (Generator079 generator in newlyActivated)
                {
                    generator.NetworkremainingPowerup = generatorTimes[generator];

                    Map.Broadcast(5, $"<i>Generator {GetGeneratorName(generator.CurRoom)} powering up...</i>");
                }

                foreach (Generator079 generator in newlyShutdown)
                {
                    generatorTimes[generator] = generator.NetworkremainingPowerup;

                    if (generator.NetworkremainingPowerup > 0) Map.Broadcast(5, $"<i>Generator {GetGeneratorName(generator.CurRoom)} was shut down</i>");
                }
            }

            Timing.CallDelayed(Blackout.instance.Config.GeneratorRefreshRate, () => RefreshGeneratorsLoop());
        }

        private IEnumerator<float> BlackoutLoop()
        {
            while (Round.IsStarted)
            {
                Generator079.Generators[0].ServerOvercharge(11 + Blackout.instance.Config.FlickerlightDuration, true);

                if (Blackout.instance.Config.TeslaFlicker)
                {
                    foreach (TeslaGate gate in Map.TeslaGates)
                    {
                        gate.ServerSideCode();
                    }
                }

                yield return Timing.WaitForSeconds(11 + Blackout.instance.Config.FlickerlightDuration + 0.1f);

                if (Map.ActivatedGenerators == 5)
                {
                    Map.Broadcast(10, "<i>All generators have been activated!\nOpen the Armory or the Entrance Checkpoint to escape!</i>");
                    if (Blackout.instance.Config.LightsTurnOn) yield break;
                }
            }
        }
    }
}
