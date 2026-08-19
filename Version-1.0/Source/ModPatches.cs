using HarmonyLib;
using Timberborn.DeteriorationSystem;
using Timberborn.StatusSystem;

namespace Calloatti.BotStorage
{
  [HarmonyPatch(typeof(StatusSubject), nameof(StatusSubject.RegisterStatus))]
  public static class PreventUnstaffedStatusPatch
  {
    public static bool Prefix(StatusSubject __instance, StatusToggle statusToggle)
    {
      if (__instance.GetComponent<BotStorageBuilding>() != null)
      {
        string spriteName = statusToggle.StatusSpecification.SpriteName ?? "";

        if (spriteName.Contains("NoUnemployed"))
        {
          return false;
        }
      }
      return true;
    }
  }

  // Restored: The highly optimized O(1) Deterioration Patch
  [HarmonyPatch(typeof(Deteriorable), nameof(Deteriorable.Tick))]
  public static class DeteriorableTickPatch
  {
    public static bool Prefix(Deteriorable __instance)
    {
      if (BotStorageBuilding.ProtectedBots.ContainsKey(__instance))
      {
        return false;
      }
      return true;
    }
  }
}
