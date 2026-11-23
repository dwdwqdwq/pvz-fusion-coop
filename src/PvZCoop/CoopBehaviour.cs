using System;
using UnityEngine;

namespace PvZCoop;

public class CoopBehaviour : MonoBehaviour
{
	private bool _onMainMenu;

	private MainMenu _menu;

	private float _nextMenuCheck;

	private bool _syncErrorLogged;

	private bool _uiErrorLogged;

	private bool _typingBroken;

	public CoopBehaviour(IntPtr ptr)
		: base(ptr)
	{
	}

	private void Start()
	{
		Party.Init();
		Ui.Init(base.gameObject);
	}

	private void Update()
	{
		Ukrainian.Tick();
		Party.Tick();
        PlantIdentity.Tick();
        PlantGloveSync.Tick();
        SharedSun.Tick();DeckSharing.Tick();
		if (Party.InParty && !Application.runInBackground)
		{
			Application.runInBackground = true;
		}
		TravelRules.Tick();
		SeedButtons.Tick();
		if (_onMainMenu && Input.GetKeyDown(KeyCode.F7))
		{
			try
			{
				PriceDump.Run();
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError("price dump: " + ex);
			}
		}
		Session.Tick();
		Unlocks.Tick();
		try
		{
			SunVisuals.Tick();
			VictorySync.Tick();
			ZombieVisuals.Tick();
			ZombieEffects.Tick();
            ZombieDeathEffects.Tick();
			Sync.Tick();
            MineStateSync.Tick();
            PlantStateSync.Tick();
            ChomperSync.Tick();
		}
		catch (Exception ex2)
		{
			if (!_syncErrorLogged)
			{
				_syncErrorLogged = true;
				Plugin.Logger.LogError("Sync.Tick: " + ex2);
			}
		}
		if (Time.unscaledTime >= _nextMenuCheck)
		{
			_nextMenuCheck = Time.unscaledTime + 0.5f;
			_menu = UnityEngine.Object.FindObjectOfType<MainMenu>();
			_onMainMenu = _menu != null && _menu.isActiveAndEnabled;
            // MainMenu can remain alive while a level loads. Session transitions
            // are reset by the explicit native menu hooks, never by visibility.
			if (_onMainMenu)
			{
				PvP.OnMainMenu();
			}
		}
		if (!_typingBroken)
		{
			try
			{
				Screens.ReadTyping(_onMainMenu);
			}
			catch (Exception ex3)
			{
				_typingBroken = true;
				Plugin.Logger.LogWarning("Keyboard typing unavailable, use Paste: " + ex3.Message);
			}
		}
	}

	private void LateUpdate()
	{
		try
		{
			LanguageSelection.Tick(_menu, _onMainMenu);
            TeamUi.Tick();
            Screens.Tick(_menu, _onMainMenu);
            CoopPause.Tick();
            Sync.RenderZombieMotion();
            PlacementPreview.Tick();
		}
		catch (Exception ex)
		{
			if (!_uiErrorLogged)
			{
				_uiErrorLogged = true;
				Plugin.Logger.LogError("UI: " + ex);
			}
		}
	}

	private void OnApplicationQuit()
	{
		Party.Shutdown();
	}
}

