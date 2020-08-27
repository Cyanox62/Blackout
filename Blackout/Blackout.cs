﻿using Exiled.API.Features;
using PlayerEvents = Exiled.Events.Handlers.Player;
using ServerEvents = Exiled.Events.Handlers.Server;
using Scp049Events = Exiled.Events.Handlers.Scp049;
using HarmonyLib;

namespace Blackout
{
    public class Blackout : Plugin<Config>
    {
        public static RoleType[] ghostSpawnPoints;

        public static Blackout instance;

        public static bool active;
		public static bool toggled;
        public static bool activeNextRound;

        public static bool roundLock;

        public static string[] validRanks;

        private EventHandlers ev;
        private HarmonyLib.Harmony hInstance;

        public override void OnEnabled()
        {
            base.OnEnabled();

            if (!Config.IsEnabled) return;

            ghostSpawnPoints = new[]
            {
                RoleType.Scp096,
                RoleType.Scp93953,
                RoleType.Scp93989
            };

            instance = this;

            ev = new EventHandlers();

            hInstance = new HarmonyLib.Harmony("cyanox.blackout");
            hInstance.PatchAll();

            ServerEvents.RoundStarted += ev.OnRoundStart;
            ServerEvents.RespawningTeam += ev.OnTeamRespawn;
            ServerEvents.RestartingRound += ev.OnRoundRestart;
            ServerEvents.EndingRound += ev.OnCheckRoundEnd;
            ServerEvents.SendingRemoteAdminCommand += ev.OnRACommand;

            Scp049Events.StartingRecall += ev.OnRecallZombie;

            PlayerEvents.InteractingDoor += ev.OnDoorAccess;
            PlayerEvents.Hurting += ev.OnPlayerHurt;
            PlayerEvents.TriggeringTesla += ev.OnPlayerTriggerTesla;
            PlayerEvents.ChangingRole += ev.OnSetRole;
            PlayerEvents.Spawning += ev.OnSpawn;
        }

        public override void OnDisabled()
        {
            base.OnDisabled();

            ServerEvents.RoundStarted -= ev.OnRoundStart;
            ServerEvents.RespawningTeam -= ev.OnTeamRespawn;
            ServerEvents.RestartingRound -= ev.OnRoundRestart;
            ServerEvents.EndingRound -= ev.OnCheckRoundEnd;
            ServerEvents.SendingRemoteAdminCommand -= ev.OnRACommand;

            Scp049Events.StartingRecall -= ev.OnRecallZombie;

            PlayerEvents.InteractingDoor -= ev.OnDoorAccess;
            PlayerEvents.Hurting -= ev.OnPlayerHurt;
            PlayerEvents.TriggeringTesla -= ev.OnPlayerTriggerTesla;
            PlayerEvents.ChangingRole -= ev.OnSetRole;
            PlayerEvents.Spawning -= ev.OnSpawn;

            hInstance.UnpatchAll();
            hInstance = null;

            ev = null;
        }

        public override string Name => "Blackout";
        public override string Author => "Cyanox";
    }
}
