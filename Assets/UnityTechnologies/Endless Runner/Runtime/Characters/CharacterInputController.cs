using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
// Aliased because UnityEngine.Touch and UnityEngine.InputSystem.EnhancedTouch.Touch would otherwise be ambiguous.
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Handle everything related to controlling the character. Interact with both the Character (visual, animation) and CharacterCollider
/// </summary>
public class CharacterInputController : MonoBehaviour
{
    static int s_DeadHash = Animator.StringToHash ("Dead");
	static int s_RunStartHash = Animator.StringToHash("runStart");
	static int s_MovingHash = Animator.StringToHash("Moving");
	static int s_JumpingHash = Animator.StringToHash("Jumping");
	static int s_JumpingSpeedHash = Animator.StringToHash("JumpSpeed");
	static int s_SlidingHash = Animator.StringToHash("Sliding");

	public TrackManager trackManager;
	public Character character;
	public CharacterCollider characterCollider;
	public GameObject blobShadow;
	public float laneChangeSpeed = 1.0f;

	public int maxLife = 3;

	public Consumable inventory;

	public int coins { get { return m_Coins; } set { m_Coins = value; } }
	public int premium { get { return m_Premium; } set { m_Premium = value; } }
	public int currentLife { get { return m_CurrentLife; } set { m_CurrentLife = value; } }
	public List<Consumable> consumables { get { return m_ActiveConsumables; } }
	public bool isJumping { get { return m_Jumping; } }
	public bool isSliding { get { return m_Sliding; } }
	// Read-only state for the tutorial: the lane being moved to, and how far the character still is from that lane and
	// from the ground, so a step can check that the requested move really happened and finished.
	public int currentLane { get { return m_CurrentLane; } }
	public Vector3 offsetFromTarget { get { return characterCollider.transform.localPosition - m_TargetPosition; } }

	[Header("Controls")]
	public float jumpLength = 2.0f;     // Distance jumped
	public float jumpHeight = 1.2f;

	public float slideLength = 2.0f;

	[Header("Sounds")]
	public AudioClip slideSound;
	public AudioClip powerUpUseSound;
	public AudioSource powerupSource;

    public enum MoveInput { None, Left, Right, Up, Down }

    // Tutorial: the only move input the current step accepts (None: none at all). Any other swipe is ignored, so a wrong
    // move cannot change the player's lane or pose while a step waits. Normal runs accept every move.
    [HideInInspector] public MoveInput tutorialAllowedMove;

    protected int m_Coins;
    protected int m_Premium;
    protected int m_CurrentLife;

    protected List<Consumable> m_ActiveConsumables = new List<Consumable>();

    protected int m_ObstacleLayer;

	protected bool m_IsInvincible;
	protected bool m_IsRunning;
	
    protected float m_JumpStart;
    protected bool m_Jumping;

	protected bool m_Sliding;
	protected float m_SlideStart;

	protected AudioSource m_Audio;

    protected int m_CurrentLane = k_StartingLane;
    protected Vector3 m_TargetPosition = Vector3.zero;

    protected readonly Vector3 k_StartingPosition = Vector3.forward * 2f;

    protected const int k_StartingLane = 1;
    protected const float k_GroundingSpeed = 80f;
    protected const float k_ShadowRaycastDistance = 100f;
    protected const float k_ShadowGroundOffset = 0.01f;
    protected const float k_TrackSpeedToJumpAnimSpeedRatio = 0.6f;
    protected const float k_TrackSpeedToSlideAnimSpeedRatio = 0.9f;

    protected void Awake ()
    {
        m_Premium = 0;
        m_CurrentLife = 0;
        m_Sliding = false;
        m_SlideStart = 0.0f;
	    m_IsRunning = false;

#if !UNITY_EDITOR && !UNITY_STANDALONE
        // ETouch.activeTouches only reports touches once the enhanced touch support is enabled.
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
#endif
    }

#if !UNITY_STANDALONE
    protected Vector2 m_StartingTouch;
	protected bool m_IsSwiping = false;

    static PointerEventData s_UIPointerData;
    static EventSystem s_UIPointerEventSystem;
    static readonly List<RaycastResult> s_UIRaycastResults = new List<RaycastResult>();

    protected static bool IsOverInteractableUI(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        if (s_UIPointerData == null || s_UIPointerEventSystem != eventSystem)
        {
            s_UIPointerData = new PointerEventData(eventSystem);
            s_UIPointerEventSystem = eventSystem;
        }
        s_UIPointerData.Reset();
        s_UIPointerData.position = screenPosition;

        s_UIRaycastResults.Clear();
        eventSystem.RaycastAll(s_UIPointerData, s_UIRaycastResults);
        for (int i = 0; i < s_UIRaycastResults.Count; ++i)
        {
            Selectable selectable = s_UIRaycastResults[i].gameObject.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsInteractable())
                return true;
        }

        return false;
    }
#endif

    // Cheating functions, use for testing
	public void CheatInvincible(bool invincible)
	{
		m_IsInvincible = invincible;
    }

	public bool IsCheatInvincible()
	{
		return m_IsInvincible;
	}

    public void Init()
    {
        transform.position = k_StartingPosition;
		m_TargetPosition = Vector3.zero;

		m_CurrentLane = k_StartingLane;
		characterCollider.transform.localPosition = Vector3.zero;

        currentLife = maxLife;

		m_Audio = GetComponent<AudioSource>();

		m_ObstacleLayer = 1 << LayerMask.NameToLayer("Obstacle");
    }

	// Called at the beginning of a run or rerun
	public void Begin()
	{
		m_IsRunning = false;
        character.animator.SetBool(s_DeadHash, false);

		characterCollider.Init ();

		m_ActiveConsumables.Clear();
	}

	public void End()
	{
        CleanConsumable();

        // A run can end mid-jump or mid-slide (quit from the pause menu). The next run re-enables this object before its
        // character is loaded, so stale flags would touch a missing animator and keep the slide collider shrunk.
        if (m_Sliding)
        {
            m_Sliding = false;
            characterCollider.Slide(false);
        }
        m_Jumping = false;
    }

    public void CleanConsumable()
    {
        for (int i = 0; i < m_ActiveConsumables.Count; ++i)
        {
            m_ActiveConsumables[i].Ended(this);
            Addressables.ReleaseInstance(m_ActiveConsumables[i].gameObject);
        }

        m_ActiveConsumables.Clear();
    }

    public void StartRunning()
    {   
	    StartMoving();
        if (character.animator)
        {
            character.animator.Play(s_RunStartHash);
            character.animator.SetBool(s_MovingHash, true);
        }
    }

	public void StartMoving()
	{
		m_IsRunning = true;
	}

    public void StopMoving()
    {
	    m_IsRunning = false;
        trackManager.StopMove();
        if (character.animator)
        {
            character.animator.SetBool(s_MovingHash, false);
        }
    }

    // Every swipe/arrow key goes through here. In the tutorial only the move the current step asks for gets through,
    // once: it is consumed on use, and the tutorial hands it back only if it did not start the move.
    public void HandleMoveInput(MoveInput move)
    {
        if (TrackManager.instance.isTutorial)
        {
            if (move == MoveInput.None || move != tutorialAllowedMove)
                return;
            tutorialAllowedMove = MoveInput.None;
        }

        switch (move)
        {
            case MoveInput.Left: ChangeLane(-1); break;
            case MoveInput.Right: ChangeLane(1); break;
            case MoveInput.Up: Jump(); break;
            case MoveInput.Down: Slide(); break;
        }
    }

	protected void Update ()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        // Use key input in editor or standalone

        // Keyboard.current is null when no keyboard device is connected, so it has to be checked every frame.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame)
                HandleMoveInput(MoveInput.Left);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                HandleMoveInput(MoveInput.Right);
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                HandleMoveInput(MoveInput.Up);
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                HandleMoveInput(MoveInput.Down);
        }
#else
        // Use touch input on mobile
        if (ETouch.activeTouches.Count == 1)
        {
			ETouch touch = ETouch.activeTouches[0];

			if(m_IsSwiping)
			{
				Vector2 diff = touch.screenPosition - m_StartingTouch;

				// Put difference in Screen ratio, but using only width, so the ratio is the same on both
                // axes (otherwise we would have to swipe more vertically...)
				diff = new Vector2(diff.x/Screen.width, diff.y/Screen.width);

				if(diff.magnitude > 0.01f) //we set the swip distance to trigger movement to 1% of the screen width
				{
					if(Mathf.Abs(diff.y) > Mathf.Abs(diff.x))
						HandleMoveInput(diff.y < 0 ? MoveInput.Down : MoveInput.Up);
					else
						HandleMoveInput(diff.x < 0 ? MoveInput.Left : MoveInput.Right);
						
					m_IsSwiping = false;
				}
            }

        	// Input check is AFTER the swip test, that way if TouchPhase.Ended happen a single frame after the Began Phase
			// a swipe can still be registered (otherwise, m_IsSwiping will be set to false and the test wouldn't happen for that began-Ended pair)
			if(touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
			{
				m_StartingTouch = touch.screenPosition;
				// A touch that starts on a button (pause, inventory) is a UI tap, not a swipe.
				m_IsSwiping = !IsOverInteractableUI(touch.screenPosition);
			}
			else if(touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
			{
				m_IsSwiping = false;
			}
        }
#endif

        Vector3 verticalTargetPosition = m_TargetPosition;

		if (m_Sliding)
		{
            // Slide time isn't constant but the slide length is (even if slightly modified by speed, to slide slightly further when faster).
            // This is for gameplay reason, we don't want the character to drasticly slide farther when at max speed.
			float correctSlideLength = slideLength * (1.0f + trackManager.speedRatio);
			float ratio = (trackManager.worldDistance - m_SlideStart) / correctSlideLength;
			if (ratio >= 1.0f)
			{
                // We slid to (or past) the required length, go back to running
				StopSliding();
			}
		}

        if(m_Jumping)
        {
			if (trackManager.isMoving)
			{
                // Same as with the sliding, we want a fixed jump LENGTH not fixed jump TIME. Also, just as with sliding,
                // we slightly modify length with speed to make it more playable.
				float correctJumpLength = jumpLength * (1.0f + trackManager.speedRatio);
				float ratio = (trackManager.worldDistance - m_JumpStart) / correctJumpLength;
				if (ratio >= 1.0f)
				{
					m_Jumping = false;
					character.animator.SetBool(s_JumpingHash, false);
				}
				else
				{
					verticalTargetPosition.y = Mathf.Sin(ratio * Mathf.PI) * jumpHeight;
				}
			}
			else if(!AudioListener.pause)//use AudioListener.pause as it is an easily accessible singleton & it is set when the app is in pause too
			{
			    verticalTargetPosition.y = Mathf.MoveTowards (verticalTargetPosition.y, 0, k_GroundingSpeed * Time.deltaTime);
				if (Mathf.Approximately(verticalTargetPosition.y, 0f))
				{
					character.animator.SetBool(s_JumpingHash, false);
					m_Jumping = false;
				}
			}
        }

        characterCollider.transform.localPosition = Vector3.MoveTowards(characterCollider.transform.localPosition, verticalTargetPosition, laneChangeSpeed * Time.deltaTime);

        // Put blob shadow under the character.
        RaycastHit hit;
        if(Physics.Raycast(characterCollider.transform.position + Vector3.up, Vector3.down, out hit, k_ShadowRaycastDistance, m_ObstacleLayer))
        {
            blobShadow.transform.position = hit.point + Vector3.up * k_ShadowGroundOffset;
        }
        else
        {
            Vector3 shadowPosition = characterCollider.transform.position;
            shadowPosition.y = k_ShadowGroundOffset;
            blobShadow.transform.position = shadowPosition;
        }
	}

    public void Jump()
    {
	    if (!m_IsRunning)
		    return;
	    
        if (!m_Jumping)
        {
			if (m_Sliding)
				StopSliding();

			float correctJumpLength = jumpLength * (1.0f + trackManager.speedRatio);
			m_JumpStart = trackManager.worldDistance;
            float animSpeed = k_TrackSpeedToJumpAnimSpeedRatio * (trackManager.speed / correctJumpLength);

            character.animator.SetFloat(s_JumpingSpeedHash, animSpeed);
            character.animator.SetBool(s_JumpingHash, true);
			m_Audio.PlayOneShot(character.jumpSound);
			m_Jumping = true;
        }
    }

    public void StopJumping()
    {
        if (m_Jumping)
        {
            character.animator.SetBool(s_JumpingHash, false);
            m_Jumping = false;
        }
    }

	public void Slide()
	{
		if (!m_IsRunning)
			return;
		
		if (!m_Sliding)
		{

		    if (m_Jumping)
		        StopJumping();

            float correctSlideLength = slideLength * (1.0f + trackManager.speedRatio); 
			m_SlideStart = trackManager.worldDistance;
            float animSpeed = k_TrackSpeedToJumpAnimSpeedRatio * (trackManager.speed / correctSlideLength);

			character.animator.SetFloat(s_JumpingSpeedHash, animSpeed);
			character.animator.SetBool(s_SlidingHash, true);
			m_Audio.PlayOneShot(slideSound);
			m_Sliding = true;

			characterCollider.Slide(true);
		}
	}

	public void StopSliding()
	{
		if (m_Sliding)
		{
			character.animator.SetBool(s_SlidingHash, false);
			m_Sliding = false;

			characterCollider.Slide(false);
		}
	}

	public void ChangeLane(int direction)
    {
		if (!m_IsRunning)
			return;

        int targetLane = m_CurrentLane + direction;

        if (targetLane < 0 || targetLane > 2)
            // Ignore, we are on the borders.
            return;

        m_CurrentLane = targetLane;
        m_TargetPosition = new Vector3((m_CurrentLane - 1) * trackManager.laneOffset, 0, 0);
    }

    public void UseInventory()
    {
        if(inventory != null && inventory.CanBeUsed(this))
        {
            UseConsumable(inventory);
            inventory = null;
        }
    }

    public void UseConsumable(Consumable c)
    {
		characterCollider.audio.PlayOneShot(powerUpUseSound);

        for(int i = 0; i < m_ActiveConsumables.Count; ++i)
        {
            if(m_ActiveConsumables[i].GetType() == c.GetType())
            {
				// If we already have an active consumable of that type, we just reset the time
                m_ActiveConsumables[i].ResetTime();
                Addressables.ReleaseInstance(c.gameObject);
                return;
            }
        }

        // If we didn't had one, activate that one 
        c.transform.SetParent(transform, false);
        c.gameObject.SetActive(false);

        m_ActiveConsumables.Add(c);
        StartCoroutine(c.Started(this));
    }
}
