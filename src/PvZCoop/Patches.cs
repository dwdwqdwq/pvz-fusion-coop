using GameLevel;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PvZCoop;

[HarmonyPatch]
public static class Patches
{
	[HarmonyPatch(typeof(UIMgr), "EnterGame")]
	[HarmonyPrefix]
	private static bool EnterGame(LevelType levelType, int levelNumber, int id, string name)
	{
		return Session.OnEnterGame(levelType, levelNumber, id, name);
	}

	[HarmonyPatch(typeof(UIMgr), "EnterMainMenu")]
	[HarmonyPrefix]
	private static void EnterMainMenu()
	{
		Session.OnBackToMenu();
	}

	[HarmonyPatch(typeof(UIMgr), "BackToMenu")]
	[HarmonyPrefix]
	private static void BackToMenu()
	{
		Session.OnBackToMenu();
	}

	[HarmonyPatch(typeof(SeedLibrary), "OnStartGame")]
	[HarmonyPrefix]
	private static bool SeedLibraryStart(SeedLibrary __instance)
	{
		return Session.OnStartClicked(null, delegate
		{
			__instance.OnStartGame();
		}, "SeedLibrary.OnStartGame");
	}

	[HarmonyPatch(typeof(BoardSpawner), "SummonZombies")]
	[HarmonyPrefix]
	private static bool SummonPre(int __0)
	{
		if (PvP.Enabled && !Session.Allow)
		{
			return false;
		}
		if (Sync.IsClient && !Session.Allow)
		{
			return false;
		}
		Sync.OnSummonStart(__0);
		return true;
	}

	[HarmonyPatch(typeof(BoardSpawner), "SummonZombies")]
	[HarmonyPostfix]
	private static void SummonPost()
	{
		Sync.OnSummonEnd();
	}

	[HarmonyPatch(typeof(CreateZombie), "SetZombie")]
	[HarmonyPostfix]
	private static void ZombieCreated(int __0, ZombieType __1, float __2, bool __3, Zombie __result)
	{
		Sync.OnZombieCreated(__result, __0, __1, __2, __3);
	}

	[HarmonyPatch(typeof(Zombie), "TakeDamage")]
	[HarmonyPrefix]
	private static bool ZombieDamage(Zombie __instance)
	{
		if (Sync.IsClient && !Session.Allow)
		{
			return !Sync.IsSynced(__instance);
		}
		return true;
	}

	[HarmonyPatch(typeof(Zombie), "Charred")]
	[HarmonyPrefix]
	private static bool ZombieCharred(Zombie __instance)
	{
		if (Sync.IsClient && !Session.Allow)
		{
			return !Sync.IsSynced(__instance);
		}
		return true;
	}

	[HarmonyPatch(typeof(Zombie), "Die")]
	[HarmonyPrefix]
	private static bool ZombieDie(Zombie __instance, int __0)
	{
		if (Sync.IsClient && !Session.Allow && Sync.IsSynced(__instance))
		{
			return false;
		}
		Sync.OnZombieDie(__instance, __0);
		return true;
	}

	[HarmonyPatch(typeof(Plant), "TakeDamage")]
	[HarmonyPrefix]
	private static bool PlantDamage(Il2CppObjectBase __1)
	{
		if (Sync.IsClient && !Session.Allow)
		{
			return false;
		}
		if (PvP.Enabled && __1 != null)
		{
			Zombie zombie = __1.TryCast<Zombie>();
			if (zombie != null && !zombie.isMindControlled)
			{
				PvP.OnBite();
			}
		}
		return true;
	}

	[HarmonyPatch(typeof(GameLose), "ProcessZombieEnter")]
	[HarmonyPrefix]
	private static bool ZombieEnter(Zombie __0)
	{
		if (!PvP.Enabled || Session.Allow)
		{
			return true;
		}
		if (__0 != null && !__0.isMindControlled && Sync.Running && Party.IsHost)
		{
			PvP.Finish(PvP.Team.Attack);
		}
		return false;
	}

	[HarmonyPatch(typeof(UIMgr), "EnterLoseMenu")]
	[HarmonyPrefix]
	private static bool LoseMenu()
	{
		if (!PvP.Enabled || Session.Allow)
		{
			return true;
		}
		if (Sync.Running && Party.IsHost)
		{
			PvP.Finish(PvP.Team.Attack);
		}
		return false;
	}

	[HarmonyPatch(typeof(DroppedItem), "Start")]
	[HarmonyPrefix]
	private static bool DroppedItemStart(DroppedItem __instance)
	{
		if (!PvP.IsAttacker)
		{
			return true;
		}
		Object.Destroy(__instance.gameObject);
		return false;
	}

	[HarmonyPatch(typeof(Zombie), "DropItem")]
	[HarmonyPrefix]
	private static bool DropItem()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(Zombie), "DropKilledAward")]
	[HarmonyPrefix]
	private static bool DropAward()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(SaveMgr), "LoadBoard")]
	[HarmonyPrefix]
	private static bool LoadBoard()
	{
		return !PvP.Enabled;
	}

	[HarmonyPatch(typeof(SaveMgr), "LoadPlant")]
	[HarmonyPrefix]
	private static bool LoadPlant()
	{
		return !PvP.Enabled;
	}

	[HarmonyPatch(typeof(SaveMgr), "SaveBoard")]
	[HarmonyPostfix]
	private static void SaveBoard()
	{
		if (PvP.Enabled)
		{
			PvP.WipeSavedLawn();
		}
	}

	[HarmonyPatch(typeof(CreatePlant), "LimTravel")]
	[HarmonyPostfix]
	private static void LimTravel(PlantType theSeedType, ref bool __result)
	{
		if (PvP.Enabled)
		{
			__result = false;
		}
	}

	[HarmonyPatch(typeof(Board), "NewZombieUpdate")]
	[HarmonyPrefix]
	private static bool WaveClock()
	{
		return !PvP.Enabled;
	}

	[HarmonyPatch(typeof(Mouse), "ClickZombieCard")]
	[HarmonyPrefix]
	private static bool ClickZombieCard(IZECard __0)
	{
		return PvP.CanPickZombieCard(__0);
	}

	[HarmonyPatch(typeof(ZombieDataManager), "GetZombieData")]
	[HarmonyPostfix]
	private static void ZombieData(ZombieDataManager.ZombieData __result)
	{
		Prices.Apply(__result);
		PvP.PriceZombie(__result);
	}

	[HarmonyPatch(typeof(PlantDataManager), "GetPlantData")]
	[HarmonyPostfix]
	private static void PlantData(PlantDataManager.PlantData __result)
	{
		Prices.Apply(__result);
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetZombieByCard")]
	[HarmonyPrefix]
	private static bool PlaceZombiePre(Mouse __instance, out (int sun, int cost, bool placing) __state)
	{
		IZECard theIZECardOnMouse = __instance.theIZECardOnMouse;
		__state = (sun: (Board.Instance != null) ? Board.Instance.theSun : 0, cost: (theIZECardOnMouse != null) ? theIZECardOnMouse.theSeedCost : 0, placing: false);
		bool flag = PvP.OnPlaceZombie(__instance);
		__state.placing = flag && PvP.Placing;
		return flag;
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetZombieByCard")]
	[HarmonyPostfix]
	private static void PlaceZombiePost((int sun, int cost, bool placing) __state)
	{
		PvP.AfterPlaceZombie(__state.sun, __state.cost, __state.placing);
	}

	[HarmonyPatch(typeof(CreateZombie), "SetZombie")]
	[HarmonyPrefix]
	private static bool SetZombieLocal(ref Zombie __result)
	{
		if (!Sync.BlockLocalZombie())
		{
			return true;
		}
		__result = null;
		return false;
	}

	[HarmonyPatch(typeof(BungiZombie), "SetTarget")]
	[HarmonyPostfix]
	private static void BungeeTarget(BungiZombie __instance)
	{
		Sync.OnBungeeTarget(__instance);
	}

	[HarmonyPatch(typeof(CreateZombie), "SetZombie")]
	[HarmonyPrefix]
	private static void SetZombiePre(ref bool __3)
	{
		if (PvP.Placing)
		{
			__3 = false;
		}
	}

	[HarmonyPatch(typeof(CreateZombie), "SetZombieWithMindControl")]
	[HarmonyPrefix]
	private static bool SetZombieMind(CreateZombie __instance, int __0, ZombieType __1, float __2, ref Zombie __result)
	{
		if (!PvP.Placing)
		{
			return true;
		}
		__result = __instance.SetZombie(__0, __1, __2, false);
		return false;
	}

	[HarmonyPatch(typeof(Shovel), "Use")]
	[HarmonyPrefix]
	private static bool ShovelAllowed()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(Glove), "Use")]
	[HarmonyPrefix]
	private static bool GloveAllowed()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(Shovel), "OnUpdate")]
	[HarmonyPrefix]
	private static bool ShovelUpdate()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(Glove), "OnUpdate")]
	[HarmonyPrefix]
	private static bool GloveUpdate()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(InGameTool), "Update")]
	[HarmonyPrefix]
	private static bool ToolUpdate(InGameTool __instance)
	{
		if (PvP.IsAttacker)
		{
			if (!(__instance.TryCast<Shovel>() != null))
			{
				return !(__instance.TryCast<Glove>() != null);
			}
			return false;
		}
		return true;
	}

	[HarmonyPatch(typeof(Shovel), "OnClick")]
	[HarmonyPrefix]
	private static bool ShovelClick(ref bool __result)
	{
		if (!PvP.IsAttacker)
		{
			return true;
		}
		__result = false;
		return false;
	}

	[HarmonyPatch(typeof(InGameTool), "OnClick")]
	[HarmonyPrefix]
	private static bool ToolClick(InGameTool __instance, ref bool __result)
	{
		if (!PvP.IsAttacker || (__instance.TryCast<Shovel>() == null && __instance.TryCast<Glove>() == null))
		{
			return true;
		}
		__result = false;
		return false;
	}

	[HarmonyPatch(typeof(Plant), "Crashed")]
	[HarmonyPrefix]
	private static bool PlantCrashed()
	{
		if (Sync.IsClient)
		{
			return Session.Allow;
		}
		return true;
	}

	[HarmonyPatch(typeof(PlantDataManager), "IsUnlocked")]
	[HarmonyPostfix]
	private static void IsUnlocked(PlantType __0, ref bool __result)
	{
		bool? flag = Unlocks.Override(__0);
		if (flag.HasValue)
		{
			__result = flag.Value;
		}
	}

	[HarmonyPatch(typeof(PlantDataManager), "CheckIfPlantUnlock")]
	[HarmonyPostfix]
	private static void CheckIfPlantUnlock(PlantType __0, ref bool __result)
	{
		bool? flag = Unlocks.Override(__0);
		if (flag.HasValue)
		{
			__result = flag.Value;
		}
	}

	[HarmonyPatch(typeof(AdvantureConfig), "CheckPlantUnlock")]
	[HarmonyPostfix]
	private static void AdvCheckPlantUnlock(PlantType __0, ref bool __result)
	{
		bool? flag = Unlocks.Override(__0);
		if (flag.HasValue)
		{
			__result = flag.Value;
		}
	}

	[HarmonyPatch(typeof(CreatePlant), "SetPlant")]
	[HarmonyPrefix]
	private static void SetPlantPre(int __0, int __1, PlantType __2, Vector2 __4, bool __5, bool __6)
	{
		PvP.Placing_Plant++;
		Sync.OnSetPlantPre(__0, __1, __2, __4, __5, __6);
	}

	[HarmonyPatch(typeof(CreatePlant), "SetPlant")]
	[HarmonyPostfix]
	private static void SetPlantPost(Plant __result)
	{
		if (PvP.Placing_Plant > 0)
		{
			PvP.Placing_Plant--;
		}
		Sync.OnSetPlantPost(__result);
	}

	[HarmonyPatch(typeof(Plant), "Die")]
	[HarmonyPrefix]
	private static void PlantDie(Plant __instance, Plant.DieReason __0)
	{
		Sync.OnPlantDie(__instance, __0);
	}

	[HarmonyPatch(typeof(Plant), "MoveBox")]
	[HarmonyPrefix]
	private static void PlantMove(Plant __instance, int __0, int __1)
	{
		Sync.OnPlantMove(__instance, __0, __1);
	}

	[HarmonyPatch(typeof(Mouse), "MouseClick")]
	[HarmonyPrefix]
	private static bool ClickPre()
	{
        if(PartnerBank.Hovering || !SharedSun.InterceptMouseClick())return false;
		Sync.BeginAction();
        return true;
	}

	[HarmonyPatch(typeof(Mouse), "MouseClick")]
	[HarmonyPostfix]
	private static void ClickPost()
	{
		Sync.EndAction();
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetPlantByCard")]
	[HarmonyPrefix]
	private static void PlantCardPre()
	{
		Sync.BeginAction();
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetPlantByCard")]
	[HarmonyPostfix]
	private static void PlantCardPost()
	{
		Sync.EndAction();
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetPlantByGlove")]
	[HarmonyPrefix]
	private static void GlovePre()
	{
		Sync.BeginAction();
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetPlantByGlove")]
	[HarmonyPostfix]
	private static void GlovePost()
	{
		Sync.EndAction();
	}

	[HarmonyPatch(typeof(Mouse), "PutDownItem")]
	[HarmonyPrefix]
	private static void PutDownPre()
	{
		Sync.BeginAction();
	}

	[HarmonyPatch(typeof(Mouse), "PutDownItem")]
	[HarmonyPostfix]
	private static void PutDownPost()
	{
		Sync.EndAction();
	}

	[HarmonyPatch(typeof(Shovel), "Use")]
	[HarmonyPrefix]
	private static void ShovelPre()
	{
		Sync.BeginAction();
	}

	[HarmonyPatch(typeof(Shovel), "Use")]
	[HarmonyPostfix]
	private static void ShovelPost()
	{
		Sync.EndAction();
	}

	[HarmonyPatch(typeof(Mouse), "ClickOnCard")]
	[HarmonyPrefix]
	private static bool ClickOnCard(CardUI __0)
	{
		return !PartnerBank.IsPartnerCard(__0) && SharedSun.SelectCard(__0,false);
	}

	[HarmonyPatch(typeof(Mouse), "ClickCard")]
	[HarmonyPrefix]
	private static bool ClickCard(CardUI __0, ref bool __result)
	{
		if (!PartnerBank.IsPartnerCard(__0) && SharedSun.SelectCard(__0,true))
		{
			return true;
		}
		__result = false;
		return false;
	}

	[HarmonyPatch(typeof(CreateItem), "SetCoin")]
	[HarmonyPostfix]
	private static void CoinCreated(GameObject __result)
	{
		Sync.OnCoinCreated(__result);
	}

	[HarmonyPatch(typeof(Board), "GetSun")]
	[HarmonyPrefix]
	private static bool GetSun(ref float __0, bool __1)
	{
		return Sync.OnGetSun(ref __0, __1);
	}

	[HarmonyPatch(typeof(Mower), "StartMove")]
	[HarmonyPrefix]
	private static bool MowerStart(Mower __instance)
	{
		if (!Sync.Running || Session.Allow)
		{
			return true;
		}
		if (Sync.IsClient)
		{
			return false;
		}
		Sync.OnMowerStart(__instance);
		return true;
	}

	[HarmonyPatch(typeof(UIMgr), "EnterPauseMenu")]
	[HarmonyPostfix]
	private static void Paused()
	{
		Sync.OnPause(paused: true);
	}

	[HarmonyPatch(typeof(UIMgr), "BackToGame")]
	[HarmonyPostfix]
	private static void Resumed()
	{
		Sync.OnPause(paused: false);
	}

	[HarmonyPatch(typeof(BoardVictory), "Win")]
	[HarmonyPrefix]
	private static bool Win()
	{
		if (PvP.Enabled && !Session.Allow)
		{
			return false;
		}
		if (!Sync.Running || Session.Allow)
		{
			return true;
		}
		if (Sync.IsClient)
		{
			return false;
		}
		Sync.SendResult(win: true);
		return true;
	}

	[HarmonyPatch(typeof(GameLose), "HandleGameLose")]
	[HarmonyPrefix]
	private static bool Lose()
	{
		if (PvP.Enabled && !Session.Allow)
		{
			if (Sync.Running && Party.IsHost)
			{
				PvP.Finish(PvP.Team.Attack);
			}
			return false;
		}
		if (!Sync.Running || Session.Allow)
		{
			return true;
		}
		if (Sync.IsClient)
		{
			return false;
		}
		Sync.SendResult(win: false);
		return true;
	}

    [HarmonyPatch(typeof(CardSlotManager),"HandleExtraCardSibling")]
    [HarmonyPrefix]
    private static bool ExtraSibling(CardUI __0)=>DeckSharing.KeepNativeSibling(__0);
    [HarmonyPatch(typeof(InGameUI),"AddCardToBank")]
    [HarmonyPrefix]
    private static bool AddDeckCard(CardUI __0,ref bool __result){if(DeckSharing.BeforeAdd(__0))return true;__result=false;return false;}
    [HarmonyPatch(typeof(InGameUI),"AddCardToBank")]
    [HarmonyPostfix]
    private static void AddedDeckCard(bool __result){if(__result)DeckSharing.Changed();}
    [HarmonyPatch(typeof(InGameUI),"RemoveCardFromBank")]
    [HarmonyPostfix]
    private static void RemovedDeckCard(bool __result){if(__result)DeckSharing.Changed();}

	[HarmonyPatch(typeof(InGameBtn), "OnMouseUpAsButton")]
	[HarmonyPrefix]
	private static bool InGameBtnClick(InGameBtn __instance)
	{
		return Session.OnStartClicked(__instance.gameObject, delegate
		{
			__instance.OnMouseUpAsButton();
		}, "InGameBtn");
	}

	[HarmonyPatch(typeof(UIButton), "OnMouseUpAsButton")]
	[HarmonyPrefix]
	private static bool UIButtonClick(UIButton __instance)
	{
		return Session.OnStartClicked(__instance.gameObject, delegate
		{
			__instance.OnMouseUpAsButton();
		}, "UIButton");
	}

	[HarmonyPatch(typeof(UIButton), "OnMouseUp")]
	[HarmonyPrefix]
	private static bool UIButtonUp(UIButton __instance)
	{
		return Session.OnStartClicked(__instance.gameObject, delegate
		{
			__instance.OnMouseUp();
		}, "UIButton.OnMouseUp");
	}

	[HarmonyPatch(typeof(UIButton), "OnClicked")]
	[HarmonyPrefix]
	private static bool UIButtonClicked(UIButton __instance)
	{
		return Session.OnStartClicked(__instance.gameObject, delegate
		{
			__instance.OnClicked();
		}, "UIButton.OnClicked");
	}

	[HarmonyPatch(typeof(TheButton), "OnPointerClick")]
	[HarmonyPrefix]
	private static bool TheButtonClick(TheButton __instance, PointerEventData eventData)
	{
		return Session.OnStartClicked(__instance.gameObject, delegate
		{
			__instance.OnPointerClick(eventData);
		}, "TheButton");
	}
}
