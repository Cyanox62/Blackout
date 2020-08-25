using Exiled.API.Interfaces;
using System.Collections.Generic;
using System.ComponentModel;

namespace Blackout
{
	public class Config : IConfig
	{
		[Description("Whether the plugin is enabled or not.")]
		public bool IsEnabled { get; set; } = true;

		//public List<string> RanksTo

		[Description("Items everyone should get while in the waiting room. All items are removed when the game starts.")]
		public List<int> WaitingRoomItems { get; set; } = new List<int>()
		{
			(int)ItemType.GrenadeFlash
		};

		[Description("Items all scientists should get when the game starts.")]
		public List<int> StartItems { get; set; } = new List<int>()
		{
			(int)ItemType.KeycardScientist,
			(int)ItemType.WeaponManagerTablet,
			(int)ItemType.Radio,
			(int)ItemType.Flashlight
		};

		[Description("Items Scientists will be given after successfully escaping.")]
		public List<int> EscapeItems { get; set; } = new List<int>()
		{
			(int)ItemType.GunE11SR,
			(int)ItemType.GrenadeFrag,
			(int)ItemType.GrenadeFrag
		};

		[Description("Percentage of players that should be ghosts.")]
		public int GhostPercent { get; set; } = 10;

		[Description("Time in the waiting room.")]
		public float StartDelay { get; set; } = 30f;
		[Description("Time after the game starts until ghosts are released")]
		public float GhostDelay { get; set; } = 30f;
		[Description("Time before the round end.")]
		public float MaxTime { get; set; } = 720f;
		[Description("Time until a USP spawns in nuke armory.")]
		public float UspTime { get; set; } = 300f;
		[Description("Time required to engage a generator.")]
		public float GeneratorTime { get; set; } = 60f;
		[Description("Refresh rate of generator resuming and broadcasts.")]
		public float GeneratorRefreshRate { get; set; } = 1f;
		[Description("Amount of time between light flickers.")]
		public float FlickerlightDuration { get; set; } = 0f;

		[Description("Minutes remaining that should be announced.")]
		public List<int> AnnounceTimes { get; set; } = new List<int>()
		{
			10,
			7,
			4,
			2,
			1
		};

		[Description("If teslas should activate on light flicker.")]
		public bool TeslaFlicker { get; set; } = true;
	}
}
