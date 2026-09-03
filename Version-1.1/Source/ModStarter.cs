using HarmonyLib;
using Timberborn.ModManagerScene;

namespace Calloatti.BotStorage
{
  public class ModStarter : IModStarter
  {
    public void StartMod(IModEnvironment modEnvironment)
    {
      new Harmony("Calloatti.BotStorage").PatchAll();
    }
  }
}
