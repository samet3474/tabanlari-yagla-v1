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

    // First run only: the run itself teaches the four moves. Each step puts one real obstacle of the normal game on the
    // track ahead (random obstacles and coins stay off during the tutorial, see TrackManager.SpawnNewSegment), asks for
    // the move that gets past it, and counts only once the move was made, the move has finished and the obstacle was
    // passed without a hit. A hit works as in a normal run (the character stumbles), without losing a life; the step is
    // then tried again with a new obstacle. Afterwards the same run carries on as a normal run.
    protected bool m_IsTutorial;
    protected int m_TutorialStep;            // current step; k_TutorialSteps.Length once all are done
    protected bool m_TutorialPromptVisible;  // this attempt's prompt is up and the step's move is accepted
    protected bool m_TutorialMoveStarted;    // the step's move was seen on the character
    protected int m_TutorialAttempts;        // tries of the current step (1 = first)
    protected int m_TutorialFailures;        // crashes over the whole tutorial
    protected bool m_TutorialCompleted;
    protected TutorialOverlay m_TutorialOverlay;

    // Per step: whether its obstacle is wanted and where (track distance, same scale as TrackManager.worldDistance),
    // whether placing it has started, the instance once placed, and a request id so an instance that arrives after a
    // retry cancelled its request is released instead of used.
    protected readonly bool[] m_TutorialObstacleWanted = new bool[4];
    protected readonly float[] m_TutorialObstacleDistance = new float[4];
    protected readonly bool[] m_TutorialObstacleRequested = new bool[4];
    protected readonly GameObject[] m_TutorialObstacle = new GameObject[4];
    protected readonly int[] m_TutorialObstacleRequestId = new int[4];

    // The order the moves are taught in. Right then left brings the player back to the middle lane.
    protected static readonly CharacterInputController.MoveInput[] k_TutorialSteps =
    {
        CharacterInputController.MoveInput.Right,
        CharacterInputController.MoveInput.Left,
        CharacterInputController.MoveInput.Up,
        CharacterInputController.MoveInput.Down,
    };

    // Obstacles of the normal game (Day theme: the ones the tutorial segments and normal runs use), as Addressables
    // GUID keys. Bin: SimpleBarricade, one lane wide. Low barrier: AllLaneObstacle, jumped over. High barrier:
    // AllLaneObstacle, slid under. Their own colliders and the normal crash handling are used unchanged.
    protected const string k_BinObstacleKey = "22facfbe232a90b45b681c638f2e490d";         // ObstacleBin
    protected const string k_LowBarrierObstacleKey = "d68ac797bf384f84bbca159dc2a6e4d7";  // ObstacleLowBarrier
    protected const string k_HighBarrierObstacleKey = "b19a0bd8ede69fe4683dff84150a047b"; // ObstacleHighBarrier
    protected static readonly string[] k_TutorialObstacleKeys = { k_BinObstacleKey, k_BinObstacleKey, k_LowBarrierObstacleKey, k_HighBarrierObstacleKey };
    // Obstacle lane (-1 left, 0 middle, 1 right), and the character lane (0..2) each step starts from and leads to.
    protected static readonly int[] k_TutorialObstacleLanes = { 0, 1, 0, 0 };
    protected static readonly int[] k_TutorialStartLanes = { 1, 2, 1, 1 };
    protected static readonly int[] k_TutorialTargetLanes = { 2, 1, 1, 1 };

    // Distances in metres; the tutorial runs at the minimum speed, 10 m/s.
    protected const float k_TutorialFirstObstacleDistance = 28f; // from the run start: already in view during the countdown
    protected const float k_TutorialNextObstacleDistance = 45f;  // ahead of the character when placed: at the fog line
    protected const float k_TutorialObstacleSpacing = 30f;       // at least this far after the previous step's obstacle
    protected const float k_TutorialRetryObstacleDistance = 40f; // after a crash (the run is stopped while it is placed)
    protected const float k_TutorialPromptDistance = 14f;        // the prompt opens: 1.4 s before the obstacle
    protected const float k_TutorialPassMargin = 1.2f;           // passed: the character is this far beyond it (depths)
    // A jump or slide swiped early is held and done when it clears the obstacle: the jump arc (sin over jumpLength)
    // is highest at the obstacle at ~0.6 of its length; a slide lasts slideLength, so it must start within that.
    protected const float k_TutorialJumpReleaseRatio = 0.6f;
    protected const float k_TutorialSlideReleaseMargin = 2.0f;
    // If the move has not come by then, the run slows down (not stopped) until it does; a lane change is quick.
    protected const float k_TutorialLaneLateDistance = 6f;
    protected const float k_TutorialSlowMotionScale = 0.35f;
    // A lane change counts once the character is (nearly) on the new lane, a jump once it is back on the ground.
    protected const float k_TutorialArrivalTolerance = 0.02f;

    public int tutorialStep { get { return m_TutorialStep; } }
    public int tutorialStepCount { get { return k_TutorialSteps.Length; } }
    public bool isTutorialRun { get { return m_IsTutorial; } }
    public bool tutorialPromptVisible { get { return m_TutorialPromptVisible; } }
    public bool tutorialMoveStarted { get { return m_TutorialMoveStarted; } }
    public int tutorialAttempts { get { return m_TutorialAttempts; } }
    public int tutorialFailures { get { return m_TutorialFailures; } }
    public GameObject GetTutorialObstacle(int step) { return m_TutorialObstacle[step]; }
    // Distance (m) from the character to a step's obstacle position; float.MaxValue when none is wanted.
    public float GetTutorialObstacleDistance(int step)
    {
        return m_TutorialObstacleWanted[step] ? m_TutorialObstacleDistance[step] - trackManager.worldDistance : float.MaxValue;
    }

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
        SubwayHud.Uygula(this);
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
			// eğitimde yönergeler kesilmesin: anında; koşuda Subway gibi 3-2-1
			if (trackManager.isTutorial) trackManager.StartMove(false); else trackManager.DevamGeriSayim();
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

        scoreText.text = trackManager.score.ToString("000000");
        multiplierText.text = "x" + trackManager.multiplier;

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


    void TutorialUpdate()
    {
        if (m_TutorialStep >= k_TutorialSteps.Length)
            return;

        CharacterInputController chr = trackManager.characterController;
        int step = m_TutorialStep;
        CharacterInputController.MoveInput move = k_TutorialSteps[step];

        // No moves while the pause menu is up (Pause() froze time); the step's move comes back with the run.
        if (pauseMenu.gameObject.activeSelf)
        {
            if (!m_TutorialMoveStarted)
                chr.tutorialAllowedMove = CharacterInputController.MoveInput.None;
            return;
        }

        PlaceTutorialObstacle(step);
        if (step + 1 < k_TutorialSteps.Length)
            PlaceTutorialObstacle(step + 1);

        // A crash, handled as in a normal run (CharacterCollider: the character stumbles and the run restarts; in the
        // tutorial no life is lost): try this step again with a new obstacle.
        if (chr.characterCollider.tutorialHitObstacle)
        {
            RetryTutorialStep();
            return;
        }

        // No prompt for an obstacle that is not on the track yet.
        if (m_TutorialObstacle[step] == null)
            return;

        float distance = m_TutorialObstacleDistance[step] - trackManager.worldDistance;

        if (!m_TutorialPromptVisible)
        {
            if (!trackManager.isMoving || distance > k_TutorialPromptDistance)
                return;
            ShowTutorialPrompt(step);
        }

        if (!m_TutorialMoveStarted)
        {
            if (chr.tutorialHeldMove != CharacterInputController.MoveInput.None && distance <= TutorialReleaseDistance(move))
                chr.ReleaseTutorialMove();

            m_TutorialMoveStarted = TutorialMoveStarted(chr, step);
            if (m_TutorialMoveStarted)
            {
                // The next step's obstacle goes on the track now, at the fog line, so it is in view well before its
                // prompt; not earlier, so after a crash here the next obstacle is never already close behind.
                if (step + 1 < k_TutorialSteps.Length)
                    WantTutorialObstacle(step + 1, Mathf.Max(trackManager.worldDistance + k_TutorialNextObstacleDistance,
                        m_TutorialObstacleDistance[step] + k_TutorialObstacleSpacing));
            }
            else if (chr.tutorialAllowedMove == CharacterInputController.MoveInput.None && chr.tutorialHeldMove == CharacterInputController.MoveInput.None)
            {
                // The input was used but did not start the move (e.g. while the character could not move): accept it again.
                chr.tutorialAllowedMove = move;
            }
        }

        bool slowMotion = !m_TutorialMoveStarted && chr.tutorialHeldMove == CharacterInputController.MoveInput.None
            && trackManager.isMoving && distance <= TutorialLateDistance(move);
        Time.timeScale = slowMotion ? k_TutorialSlowMotionScale : 1.0f;

        // The step counts only once the obstacle is behind the character, without a hit (checked above), and the move is over.
        if (distance > -k_TutorialPassMargin)
            return;
        if (!m_TutorialMoveStarted)
        {
            // Past the obstacle without the move and without a crash: cannot happen with the obstacle's collider on;
            // kept so the tutorial can never get stuck on a step.
            RetryTutorialStep();
            return;
        }
        if (!TutorialMoveFinished(chr, move))
            return;

        m_TutorialOverlay.MarkDone(step);
        if (step + 1 < k_TutorialSteps.Length)
        {
            m_TutorialStep = step + 1;
            m_TutorialAttempts = 1;
            StartTutorialAttempt();
            m_TutorialOverlay.ShowPraise(Loc.Get("tutorial.good", "GÜZEL!"));
        }
        else
            FinishTutorialSteps();
    }

    float TutorialReleaseDistance(CharacterInputController.MoveInput move)
    {
        CharacterInputController chr = trackManager.characterController;
        float speedScale = 1.0f + trackManager.speedRatio; // same scaling as CharacterInputController.Jump/Slide
        switch (move)
        {
            case CharacterInputController.MoveInput.Up: return chr.jumpLength * speedScale * k_TutorialJumpReleaseRatio;
            case CharacterInputController.MoveInput.Down: return chr.slideLength * speedScale - k_TutorialSlideReleaseMargin;
            default: return float.MaxValue;
        }
    }

    float TutorialLateDistance(CharacterInputController.MoveInput move)
    {
        switch (move)
        {
            case CharacterInputController.MoveInput.Up:
            case CharacterInputController.MoveInput.Down:
                return TutorialReleaseDistance(move);
            default:
                return k_TutorialLaneLateDistance;
        }
    }

    static bool TutorialMoveStarted(CharacterInputController chr, int step)
    {
        switch (k_TutorialSteps[step])
        {
            case CharacterInputController.MoveInput.Right:
            case CharacterInputController.MoveInput.Left:
                return chr.currentLane == k_TutorialTargetLanes[step];
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

    void ShowTutorialPrompt(int step)
    {
        m_TutorialPromptVisible = true;

        CharacterInputController.MoveInput move = k_TutorialSteps[step];
        CharacterInputController chr = trackManager.characterController;
        chr.tutorialAllowedMove = move;
        chr.tutorialHoldMove = move == CharacterInputController.MoveInput.Up || move == CharacterInputController.MoveInput.Down;

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

    // A clean attempt of the current step: no prompt yet, no move accepted, no crash carried over from before.
    void StartTutorialAttempt()
    {
        m_TutorialPromptVisible = false;
        m_TutorialMoveStarted = false;
        Time.timeScale = 1.0f;

        CharacterInputController chr = trackManager.characterController;
        chr.tutorialAllowedMove = CharacterInputController.MoveInput.None;
        chr.tutorialHoldMove = false;
        chr.ClearTutorialMove();
        chr.characterCollider.tutorialHitObstacle = false;
    }

    void RetryTutorialStep()
    {
        int step = m_TutorialStep;
        m_TutorialFailures += 1;
        m_TutorialAttempts += 1;

        // The next step's obstacle may already be on its way: it comes again once this step is done.
        if (step + 1 < k_TutorialSteps.Length)
            CancelTutorialObstacle(step + 1);

        // The crashed-into obstacle stays where it is (its collider was turned off by the crash) and goes away with its
        // segment; a new one is placed ahead, and the character goes back to the lane the step starts from.
        m_TutorialObstacle[step] = null;
        m_TutorialObstacleRequestId[step] += 1;
        WantTutorialObstacle(step, trackManager.worldDistance + k_TutorialRetryObstacleDistance);
        trackManager.characterController.SetTutorialLane(k_TutorialStartLanes[step]);

        StartTutorialAttempt();
        m_TutorialOverlay.ShowRetry(Loc.Get("tutorial.retry", "TEKRAR DENE!"));
    }

    void WantTutorialObstacle(int step, float trackDistance)
    {
        m_TutorialObstacleWanted[step] = true;
        m_TutorialObstacleDistance[step] = trackDistance;
        m_TutorialObstacleRequested[step] = false;
    }

    void CancelTutorialObstacle(int step)
    {
        m_TutorialObstacleRequestId[step] += 1;
        if (m_TutorialObstacle[step] != null)
            Addressables.ReleaseInstance(m_TutorialObstacle[step]);
        m_TutorialObstacle[step] = null;
        m_TutorialObstacleWanted[step] = false;
        m_TutorialObstacleRequested[step] = false;
    }

    // Starts placing a wanted obstacle once a spawned segment covers its distance (segments load asynchronously).
    void PlaceTutorialObstacle(int step)
    {
        if (!m_TutorialObstacleWanted[step] || m_TutorialObstacleRequested[step] || m_TutorialObstacle[step] != null)
            return;

        int requestId = ++m_TutorialObstacleRequestId[step];
        m_TutorialObstacleRequested[step] = trackManager.SpawnObstacleAt(k_TutorialObstacleKeys[step], m_TutorialObstacleDistance[step],
            k_TutorialObstacleLanes[step], obj =>
            {
                if (requestId != m_TutorialObstacleRequestId[step] || !m_IsTutorial)
                {
                    if (obj != null)
                        Addressables.ReleaseInstance(obj);
                    return;
                }
                if (obj == null)
                    m_TutorialObstacleRequested[step] = false; // try again
                else
                    m_TutorialObstacle[step] = obj;
            });
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
        m_TutorialPromptVisible = false;
        Time.timeScale = 1.0f;

        CharacterInputController chr = trackManager.characterController;
        chr.tutorialAllowedMove = CharacterInputController.MoveInput.None;
        chr.tutorialHoldMove = false;
        chr.ClearTutorialMove();

        PlayerData.instance.tutorialDone = true;
        PlayerData.instance.Save();

        trackManager.ContinueAsNormalRun();
    }

    // Tutorial progress lives on objects that persist between runs, so an abandoned or replayed tutorial must start clean.
    void ResetTutorialProgress()
    {
        m_TutorialStep = 0;
        m_TutorialAttempts = 1;
        m_TutorialFailures = 0;
        m_TutorialCompleted = false;

        for (int i = 0; i < k_TutorialSteps.Length; ++i)
        {
            // Instances from an abandoned run went away with its segments; late arrivals are released by their callback.
            m_TutorialObstacleRequestId[i] += 1;
            m_TutorialObstacle[i] = null;
            m_TutorialObstacleWanted[i] = false;
            m_TutorialObstacleRequested[i] = false;
        }
        // The first obstacle is placed during the countdown, ahead of the start.
        WantTutorialObstacle(0, k_TutorialFirstObstacleDistance);

        StartTutorialAttempt();
    }
}
