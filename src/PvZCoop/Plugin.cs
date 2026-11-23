using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace PvZCoop;

[BepInPlugin("pvzfusion.coop", "PvZ Fusion Co-op", "0.12.13")]
public class Plugin : BasePlugin
{
	public const string Guid = "pvzfusion.coop";

	public const string Version = "0.12.13";

	public static ManualLogSource Logger;

	public override void Load()
	{
		Logger = base.Log;
        LanguageSelection.Init(Config);
        PlantGloveSync.Init();
        MineStateSync.Init();
        PlantIdentity.Init();
        PlantStateSync.Init();
        ChomperSync.Init();
        ZombieDeathEffects.Init();
        Party.Local = true;
        Party.Port = Config.Bind<int>("Network", "Port", 27650, "TCP server port (same co-op version on both PCs).").Value;
        if (Party.Port < 1 || Party.Port > 65535) Party.Port = 27650;
		Session.Init();
		Sync.Init();
        DeckSharing.Init();SharedSun.Init();SunVisuals.Init();
        VictorySync.Init();CampaignProgress.Init();ZombieVisuals.Init();ZombieEffects.Init();
        PlacementPreview.Init();
		Unlocks.Init();
        PvP.Enabled = false;
		Harmony harmony = new Harmony("pvzfusion.coop");
		harmony.PatchAll(typeof(Patches));
		Sync.PatchCannons(harmony);
        ZombieDeathEffects.Install(harmony);
        MineAuthority.Install(harmony);
        ChomperSync.Install(harmony);
        Ukrainian.Init(harmony);if(LanguageSelection.UkrainianEnabled)MenuArtwork.Init(harmony);
		ClassInjector.RegisterTypeInIl2Cpp<CoopBehaviour>();
		AddComponent<CoopBehaviour>();
		base.Log.LogInfo("PvZ Co-op loaded");
	}
}


