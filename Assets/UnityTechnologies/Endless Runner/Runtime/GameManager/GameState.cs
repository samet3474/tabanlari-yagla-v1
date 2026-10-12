using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

#if UNITY_ADS
using UnityEngine.Advertisements;
#endif
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif

/// <summary>
/// Pushed on top of the GameManager during gameplay. Takes care of initializing all the UI and start the TrackManager
/// Also will take care of cleaning when leaving that state.
/// </summary>
public class GameState : AState
{
	static int s_DeadHash = Animator.StringToHash("Dead");

    public Canvas canvas;
    public TrackManager trackManager;

	public AudioClip gameTheme;

    [Header("UI")]
    public Text coinText;
    public Text premiumText;
    public Text scoreText;
	public Text distanceText;
    public Text multiplierText;
	public Text countdownText;
    public RectTransform powerupZone;
	public RectTransform lifeRectTransform;

	public RectTransform pauseMenu;
	public RectTransform wholeUI;
	public Button pauseButton;

    public Image inventoryIcon;

    public GameObject gameOverPopup;
    public Button premiumForLifeButton;
    public GameObject adsForLifeButton;
    public Text premiumCurrencyOwned;

    [Header("Prefabs")]
    public GameObject PowerupIconPrefab;

    [Header("Tutorial")]
    public Text tutorialValidatedObstacles;
    public GameObject sideSlideTuto;
    public GameObject upSlideTuto;
    public GameObject downSlideTuto;
    public GameObject finishTuto;

    public Modifier currentModifier = new Modifier();

    public string adsPlacementId = "rewardedVideo";
#if UNITY_ANALYTICS
    public AdvertisingNetwork adsNetwork = AdvertisingNetwork.UnityAds;
#endif
    public bool adsRewarded = true;

    protected bool m_Finished;
    protected float m_TimeSinceStart;
    protected List<PowerupIcon> m_PowerupIcons = new List<PowerupIcon>();
	protected Image[] m_LifeHearts;

    protected RectTransform m_CountdownRectTransform;
    protected bool m_WasMoving;

    protected bool m_AdsInitialised = false;
    protected bool m_GameoverSelectionDone = false;

    protected int k_MaxLives = 3;

    // First run only: the run itself teaches the four moves, one prompt each, on a track with no obstacles
    // (TrackManager spawns tutorial segments empty), then carries on as a normal run.
    protected bool m_IsTutorial;
    protected int m_TutorialStep;           // -1 until the run starts moving; k_TutorialSteps.Length once all are done
    protected bool m_TutorialMoveStarted;   // the current step's move was seen on the character
    protected bool m_TutorialCompleted;
    protected TutorialOverlay m_TutorialOverlay;

    // The order the moves are taught in. Right then left brings the player back to the middle lane.
    protected static readonly CharacterInputController.MoveInput[] k_TutorialSteps =
    {
        CharacterInputController.MoveInput.Right,
        CharacterInputController.MoveInput.Left,
        CharacterInputController.MoveInput.Up,
        CharacterInputController.MoveInput.Down,
    };

    // A lane change counts once the character is (nearly) on the new lane, a jump once it is back on the ground.
    protected const float k_TutorialArrivalTolerance = 0.02f;

    public int tutorialStep { get { return m_TutorialStep; } }
    public int tutorialStepCount { get { return k_TutorialSteps.Length; } }
    public bool isTutorialRun { get { return m_IsTutorial; } }

    public override void Enter(AState from)
    {
        m_CountdownRectTransform = countdownText.GetComponent<RectTransform>();

        m_LifeHearts = new Image[k_MaxLives];
        for (int i = 0; i < k_MaxLives; ++i)
        {
            m_LifeHearts[i] = lifeRectTransform.GetChild(i).GetComponent<Image>();
        }

        if (MusicPlayer.instance.GetStem(0) != gameTheme)
        {
            MusicPlayer.instance.SetStem(0, gameTheme);
            CoroutineHandler.StartStaticCoroutine(MusicPlayer.instance.RestartAllStems());
        }

        m_AdsInitialised = false;
        m_GameoverSelectionDone = false;

        StartGame();
    }

    public override void Exit(AState to)
    {
        canvas.gameObject.SetActive(false);

        ClearPowerup();
    }

    public void StartGame()
    {
        canvas.gameObject.SetActive(true);
        pauseMenu.gameObject.SetActive(false);
        wholeUI.gameObject.SetActive(true);
        // Available during the tutorial too. DisplayTutorial hides it again while a tutorial prompt
        // is up, because that prompt already freezes the game through Pause(false).
        pauseButton.gameObject.SetActive(true);
        gameOverPopup.SetActive(false);

        sideSlideTuto.SetActive(false);
        upSlideTuto.SetActive(false);
        downSlideTuto.SetActive(false);
        finishTuto.SetActive(false);
        tutorialValidatedObstacles.gameObject.SetActive(false);

        if (!trackManager.isRerun)
        {
            m_TimeSinceStart = 0;
            trackManager.characterController.currentLife = trackManager.characterController.maxLife;
        }

        currentModifier.OnRunStart(this);

        m_IsTutorial = !PlayerData.instance.tutorialDone;
        trackManager.isTutorial = m_IsTutorial;
        // Left over from the old obstacle-counting tutorial; nothing listens to segment changes any more.
        trackManager.newSegmentCreated = null;
        trackManager.currentSegementChanged = null;

        if (m_TutorialOverlay == null)
            m_TutorialOverlay = TutorialOverlay.Create(wholeUI, tutorialValidatedObstacles.font, k_TutorialSteps.Length);
        m_TutorialOverlay.Hide();

        if (m_IsTutorial)
            ResetTutorialProgress();

        m_Finished = false;
        m_PowerupIcons.Clear();

        StartCoroutine(trackManager.Begin());
    }

    public override string GetName()
    {
        return "Game";
    }

    public override void Tick()
    {
        if (m_Finished)
        {
            //if we are finished, we check if advertisement is ready, allow to disable the button until it is ready
#if UNITY_ADS
            if (!trackManager.isTutorial && !m_AdsInitialised && Advertisement.IsReady(adsPlacementId))
            {
                adsForLifeButton.SetActive(true);
                m_AdsInitialised = true;
#if UNITY_ANALYTICS
                AnalyticsEvent.AdOffer(adsRewarded, adsNetwork, adsPlacementId, new Dictionary<string, object>
            {
                { "level_index", PlayerData.instance.rank },
                { "distance", TrackManager.instance == null ? 0 : TrackManager.instance.worldDistance },
            });
#endif
            }
            else if(trackManager.isTutorial || !m_AdsInitialised)
                adsForLifeButton.SetActive(false);
#else
            adsForLifeButton.SetActive(false); //Ads is disabled
#endif

            return;
        }

        if (trackManager.isLoaded)
        {
            CharacterInputController chrCtrl = trackManager.characterController;

            m_TimeSinceStart += Time.deltaTime;

            if (chrCtrl.currentLife <= 0)
            {
                pauseButton.gameObject.SetActive(false);
                chrCtrl.CleanConsumable();
                chrCtrl.character.animator.SetBool(s_DeadHash, true);
                chrCtrl.characterCollider.koParticle.gameObject.SetActive(true);
                StartCoroutine(WaitForGameOver());
            }

            // Consumable ticking & lifetime management
            List<Consumable> toRemove = new List<Consumable>();
            List<PowerupIcon> toRemoveIcon = new List<PowerupIcon>();

            for (int i = 0; i < chrCtrl.consumables.Count; ++i)
            {
                PowerupIcon icon = null;
                for (int j = 0; j < m_PowerupIcons.Count; ++j)
                {
                    if (m_PowerupIcons[j].linkedConsumable == chrCtrl.consumables[i])
                    {
                        icon = m_PowerupIcons[j];
                        break;
                    }
                }

                chrCtrl.consumables[i].Tick(chrCtrl);
                if (!chrCtrl.consumables[i].active)
                {
                    toRemove.Add(chrCtrl.consumables[i]);
                    toRemoveIcon.Add(icon);
                }
                else if (icon == null)
                {
                    // If there's no icon for the active consumable, create it!
                    GameObject o = Instantiate(PowerupIconPrefab);

                    icon = o.GetComponent<PowerupIcon>();

                    icon.linkedConsumable = chrCtrl.consumables[i];
                    icon.transform.SetParent(powerupZone, false);

                    m_PowerupIcons.Add(icon);
                }
            }

            for (int i = 0; i < toRemove.Count; ++i)
            {
                toRemove[i].Ended(trackManager.characterController);

                Addressables.ReleaseInstance(toRemove[i].gameObject);
                if (toRemoveIcon[i] != null)
                   Destroy(toRemoveIcon[i].gameObject);

                chrCtrl.consumables.Remove(toRemove[i]);
                m_PowerupIcons.Remove(toRemoveIcon[i]);
            }

            if (m_IsTutorial)
                TutorialUpdate();

            UpdateUI();

            currentModifier.OnRunTick(this);
        }
    }

	void OnApplicationPause(bool pauseStatus)
	{
		if (pauseStatus) Pause();
	}

    void OnApplicationFocus(bool focusStatus)
    {
        if (!focusStatus) Pause();
    }

    public void Pause(bool displayMenu = true)
	{
		//check if we aren't finished OR if we aren't already in pause (as that would mess states)
		if (m_Finished || AudioListener.pause == true)
			return;

		AudioListener.pause = true;
		Time.timeScale = 0;

		pauseButton.gameObject.SetActive(false);
        pauseMenu.gameObject.SetActive (displayMenu);
		// Also hides the tutorial prompt, which lives under wholeUI.
		wholeUI.gameObject.SetActive(false);
		m_WasMoving = trackManager.isMoving;
		trackManager.StopMove();
	}

	public void Resume()
	{
		Time.timeScale = 1.0f;
		pauseButton.gameObject.SetActive(true);
		pauseMenu.gameObject.SetActive (false);
		wholeUI.gameObject.SetActive(true);
		if (m_WasMoving)
		{
			trackManager.StartMove(false);
		}

		AudioListener.pause = false;
	}

	public void QuitToLoadout()
	{
		// Used by the pause menu to return immediately to loadout, canceling everything.
		Time.timeScale = 1.0f;
		AudioListener.pause = false;
		trackManager.End();
		trackManager.isRerun = false;
        PlayerData.instance.Save();
		manager.SwitchState ("Loadout");
	}

    protected void UpdateUI()
    {
        coinText.text = trackManager.characterController.coins.ToString();
        premiumText.text = trackManager.characterController.premium.ToString();

		for (int i = 0; i < 3; ++i)
		{

			if(trackManager.characterController.currentLife > i)
			{
				m_LifeHearts[i].color = Color.white;
			}
			else
			{
				m_LifeHearts[i].color = Color.black;
			}
		}

        scoreText.text = trackManager.score.ToString();
        multiplierText.text = "x " + trackManager.multiplier;

		distanceText.text = Mathf.FloorToInt(trackManager.worldDistance).ToString() + "m";

		if (trackManager.timeToStart >= 0)
		{
			countdownText.gameObject.SetActive(true);
			countdownText.text = Mathf.Ceil(trackManager.timeToStart).ToString();
			m_CountdownRectTransform.localScale = Vector3.one * (1.0f - (trackManager.timeToStart - Mathf.Floor(trackManager.timeToStart)));
		}
		else
		{
			m_CountdownRectTransform.localScale = Vector3.zero;
		}

        // Consumable
        if (trackManager.characterController.inventory != null)
        {
            inventoryIcon.transform.parent.gameObject.SetActive(true);
            inventoryIcon.sprite = trackManager.characterController.inventory.icon;
        }
        else
            inventoryIcon.transform.parent.gameObject.SetActive(false);
    }

	IEnumerator WaitForGameOver()
	{
		m_Finished = true;
		trackManager.StopMove();

        // Reseting the global blinking value. Can happen if game unexpectly exited while still blinking
        Shader.SetGlobalFloat("_BlinkingValue", 0.0f);

        yield return new WaitForSeconds(2.0f);
        if (currentModifier.OnRunEnd(this))
        {
            if (trackManager.isRerun)
                manager.SwitchState("GameOver");
            else
                OpenGameOverPopup();
        }
	}

    protected void ClearPowerup()
    {
        for (int i = 0; i < m_PowerupIcons.Count; ++i)
        {
            if (m_PowerupIcons[i] != null)
                Destroy(m_PowerupIcons[i].gameObject);
        }

        trackManager.characterController.powerupSource.Stop();

        m_PowerupIcons.Clear();
    }

    public void OpenGameOverPopup()
    {
        premiumForLifeButton.interactable = PlayerData.instance.premium >= 3;

        premiumCurrencyOwned.text = PlayerData.instance.premium.ToString();

        ClearPowerup();

        gameOverPopup.SetActive(true);
    }

    public void GameOver()
    {
        manager.SwitchState("GameOver");
    }

    public void PremiumForLife()
    {
        //This check avoid a bug where the video AND premium button are released on the same frame.
        //It lead to the ads playing and then crashing the game as it try to start the second wind again.
        //Whichever of those function run first will take precedence
        if (m_GameoverSelectionDone)
            return;

        m_GameoverSelectionDone = true;

        PlayerData.instance.premium -= 3;
        //since premium are directly added to the PlayerData premium count, we also need to remove them from the current run premium count
        // (as if you had 0, grabbed 3 during that run, you can directly buy a new chance). But for the case where you add one in the playerdata
        // and grabbed 2 during that run, we don't want to remove 3, otherwise will have -1 premium for that run!
        trackManager.characterController.premium -= Mathf.Min(trackManager.characterController.premium, 3);

        SecondWind();
    }

    public void SecondWind()
    {
        trackManager.characterController.currentLife = 1;
        trackManager.isRerun = true;
        StartGame();
    }

    public void ShowRewardedAd()
    {
        if (m_GameoverSelectionDone)
            return;

        m_GameoverSelectionDone = true;

#if UNITY_ADS
        if (Advertisement.IsReady(adsPlacementId))
        {
#if UNITY_ANALYTICS
            AnalyticsEvent.AdStart(adsRewarded, adsNetwork, adsPlacementId, new Dictionary<string, object>
            {
                { "level_index", PlayerData.instance.rank },
                { "distance", TrackManager.instance == null ? 0 : TrackManager.instance.worldDistance },
            });
#endif
            var options = new ShowOptions { resultCallback = HandleShowResult };
            Advertisement.Show(adsPlacementId, options);
        }
        else
        {
#if UNITY_ANALYTICS
            AnalyticsEvent.AdSkip(adsRewarded, adsNetwork, adsPlacementId, new Dictionary<string, object> {
                { "error", Advertisement.GetPlacementState(adsPlacementId).ToString() }
            });
#endif
        }
#else
		GameOver();
#endif
    }

    //=== AD
#if UNITY_ADS

    private void HandleShowResult(ShowResult result)
    {
        switch (result)
        {
            case ShowResult.Finished:
#if UNITY_ANALYTICS
                AnalyticsEvent.AdComplete(adsRewarded, adsNetwork, adsPlacementId);
#endif
                SecondWind();
                break;
            case ShowResult.Skipped:
                Debug.Log("The ad was skipped before reaching the end.");
#if UNITY_ANALYTICS
                AnalyticsEvent.AdSkip(adsRewarded, adsNetwork, adsPlacementId);
#endif
                break;
            case ShowResult.Failed:
                Debug.LogError("The ad failed to be shown.");
#if UNITY_ANALYTICS
                AnalyticsEvent.AdSkip(adsRewarded, adsNetwork, adsPlacementId, new Dictionary<string, object> {
                    { "error", "failed" }
                });
#endif
                break;
        }
    }
#endif


    // One step at a time: show its prompt, let only its move through, and go on as soon as the character has really
    // done that move (on the new lane, back on the ground after the jump, up again after the slide).
    void TutorialUpdate()
    {
        CharacterInputController chr = trackManager.characterController;

        if (m_TutorialStep < 0)
        {
            // Countdown: the player has no control yet and stays in the middle lane.
            if (!trackManager.isMoving)
                return;
            ShowTutorialStep(0);
        }

        if (m_TutorialStep >= k_TutorialSteps.Length)
            return;

        CharacterInputController.MoveInput move = k_TutorialSteps[m_TutorialStep];

        // No moves while the pause menu is up; the step's move comes back with the run.
        if (pauseMenu.gameObject.activeSelf)
        {
            if (!m_TutorialMoveStarted)
                chr.tutorialAllowedMove = CharacterInputController.MoveInput.None;
            return;
        }

        if (!m_TutorialMoveStarted)
        {
            m_TutorialMoveStarted = TutorialMoveStarted(chr, move);
            // The input is consumed on use; if it did not start the move, accept it again.
            chr.tutorialAllowedMove = m_TutorialMoveStarted ? CharacterInputController.MoveInput.None : move;
            if (!m_TutorialMoveStarted)
                return;
        }

        if (!TutorialMoveFinished(chr, move))
            return;

        m_TutorialOverlay.MarkDone(m_TutorialStep);
        if (m_TutorialStep + 1 < k_TutorialSteps.Length)
            ShowTutorialStep(m_TutorialStep + 1);
        else
            FinishTutorialSteps();
    }

    static bool TutorialMoveStarted(CharacterInputController chr, CharacterInputController.MoveInput move)
    {
        switch (move)
        {
            case CharacterInputController.MoveInput.Right: return chr.currentLane == 2;
            case CharacterInputController.MoveInput.Left: return chr.currentLane == 1;
            case CharacterInputController.MoveInput.Up: return chr.isJumping;
            case CharacterInputController.MoveInput.Down: return chr.isSliding;
            default: return false;
        }
    }

    static bool TutorialMoveFinished(CharacterInputController chr, CharacterInputController.MoveInput move)
    {
        switch (move)
        {
            case CharacterInputController.MoveInput.Right:
            case CharacterInputController.MoveInput.Left:
            case CharacterInputController.MoveInput.Up:
                // On the target lane and on the ground.
                return !chr.isJumping && chr.offsetFromTarget.sqrMagnitude <= k_TutorialArrivalTolerance * k_TutorialArrivalTolerance;
            case CharacterInputController.MoveInput.Down:
                return !chr.isSliding;
            default:
                return false;
        }
    }

    void ShowTutorialStep(int step)
    {
        m_TutorialStep = step;
        m_TutorialMoveStarted = false;

        CharacterInputController.MoveInput move = k_TutorialSteps[step];
        trackManager.characterController.tutorialAllowedMove = move;

        string text;
        Vector2 direction;
        switch (move)
        {
            case CharacterInputController.MoveInput.Right: text = Loc.Get("tutorial.right", "SAĞA KAYDIR"); direction = Vector2.right; break;
            case CharacterInputController.MoveInput.Left: text = Loc.Get("tutorial.left", "SOLA KAYDIR"); direction = Vector2.left; break;
            case CharacterInputController.MoveInput.Up: text = Loc.Get("tutorial.up", "YUKARI KAYDIR"); direction = Vector2.up; break;
            default: text = Loc.Get("tutorial.down", "AŞAĞI KAYDIR"); direction = Vector2.down; break;
        }
        m_TutorialOverlay.ShowStep(step, text, direction);
    }

    void FinishTutorialSteps()
    {
        m_TutorialStep = k_TutorialSteps.Length;
        m_TutorialOverlay.ShowReady(Loc.Get("tutorial.ready", "HAZIRSIN!"));
        CompleteTutorial();
    }

    // Still bound to the old end-of-tutorial panel's button in the scene. The panel is no longer shown, but if it ever
    // is, finishing the tutorial keeps the player in the run instead of sending them to the menu.
    public void FinishTutorial()
    {
        finishTuto.SetActive(false);
        CompleteTutorial();
    }

    // Saves the tutorial as done and turns the current run into a normal endless run, without going through the menu.
    void CompleteTutorial()
    {
        if (m_TutorialCompleted)
            return;

        m_TutorialCompleted = true;
        m_IsTutorial = false;
        trackManager.characterController.tutorialAllowedMove = CharacterInputController.MoveInput.None;

        PlayerData.instance.tutorialDone = true;
        PlayerData.instance.Save();

        trackManager.ContinueAsNormalRun();
    }

    // Tutorial progress lives on objects that persist between runs, so an abandoned or replayed tutorial must start clean.
    void ResetTutorialProgress()
    {
        m_TutorialStep = -1;
        m_TutorialMoveStarted = false;
        m_TutorialCompleted = false;

        CharacterInputController chrCtrl = trackManager.characterController;
        chrCtrl.tutorialAllowedMove = CharacterInputController.MoveInput.None;
        chrCtrl.characterCollider.tutorialHitObstacle = false;
    }
}
