using HarmonyLib;

namespace Blackout.Patches
{
	[HarmonyPatch(typeof(Recontainer079), nameof(Recontainer079.BeginContainment))]
	class Patch1
	{
		public static bool Prefix(NineTailedFoxAnnouncer __instance) => !Blackout.active;
	}
}
