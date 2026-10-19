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

	// Süper ayakkabı (SuperAyakkabi.cs): süre boyunca zıplama daha yüksek ve uzun, tramvay tavanına rampasız çıkılır.
	public const float k_SuperAyakkabiSure = 12f;
	const float k_SuperZiplamaYuk = 2.6f;   // 1,2 m → ~3,1 m: tavanın (2,4 m) üstünden geçer
	const float k_SuperZiplamaUzun = 1.7f;
	public float superAyakkabiKalan { get; private set; }
	float m_ZiplamaYukCarpan = 1f, m_ZiplamaUzunCarpan = 1f;
	GameObject[] m_GiyilenAyakkabi;
	Sprite m_AyakkabiIkon;

	[Header("Sounds")]
	public AudioClip slideSound;
	public AudioClip powerUpUseSound;
	public AudioSource powerupSource;

    public enum MoveInput { None, Left, Right, Up, Down }

    // Tutorial: the only move input the current step accepts (None: none at all). Any other swipe is ignored, so a wrong
    // move cannot change the player's lane or pose while a step waits. Normal runs accept every move.
    [HideInInspector] public MoveInput tutorialAllowedMove;
    // Tutorial: keep the accepted move instead of doing it at once; the tutorial does it (ReleaseTutorialMove) when the
    // obstacle is where that move clears it, so a jump or slide swiped a little early is not wasted.
    [HideInInspector] public bool tutorialHoldMove;
    public MoveInput tutorialHeldMove { get { return m_TutorialHeldMove; } }
    protected MoveInput m_TutorialHeldMove;

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

    // Walkable surfaces (tram roofs, ramps): trigger colliders on this layer. The character stands on the highest one
    // under it, climbs only gentle steps (a ramp, not a tram side), and falls when it runs or swipes off one. With no
    // such surface (Day/Night themes) the ground is 0 and the movement is exactly as before.
    public const int k_ZeminKatmani = 11;
    protected const float k_ZeminIsini = 6f;        // ray starts this far above the feet
    protected const float k_TirmanmaAdimi = 0.6f;   // highest step climbed without jumping
    protected const float k_Yercekimi = 30f;        // m/s² when falling off a surface
    protected const float k_KameraTakip = 0.5f;     // share of the surface height the camera rises with
    protected float m_Taban;                        // current standing height
    protected float m_DusmeHizi;
    protected float m_ZiplamaTabani;                // standing height the current jump started from
    protected float m_KameraYukselme;               // camera offset currently applied
    public float zeminYuksekligi { get { return m_Taban; } }

    // Yandan çarpma / tökezleme (Subway Surfers mantığı)
    static int s_TokezHash = Animator.StringToHash("Hit");
    protected const float k_YanEsik = 0.05f;       // şerit değişimi sürüyor sayılması için hedefe kalan en az mesafe (m)
    protected const float k_YanDerinlik = 0.2f;    // karakter merkezi engelin ön yüzünü bu kadar geçtiyse yandan
    protected const float k_TokezSure = 5f;        // bu süre içinde ikinci tökezleme normal çarpma olur (s)
    protected const float k_HizliDusus = 4f;       // havadayken kayınca düşüş ivmesi çarpanı
    protected int m_OncekiSerit = k_StartingLane;
    protected float m_SonTokezleme = -100f;
    [HideInInspector] public int tokezlemeSayisi;

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
		m_Taban = 0f;
		m_DusmeHizi = 0f;
		m_OncekiSerit = k_StartingLane;
		m_SonTokezleme = -100f;
		tokezlemeSayisi = 0;
		m_ZiplamaTabani = 0f;
		AyakkabiCikar();
		// TrackManager puts the camera back to its original place at the end of each run.
		m_KameraYukselme = 0f;

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
        AyakkabiCikar();

        // A run can end mid-jump or mid-slide (quit from the pause menu). The next run re-enables this object before its
        // character is loaded, so stale flags would touch a missing animator and keep the slide collider shrunk.
        if (m_Sliding)
        {
            m_Sliding = false;
            characterCollider.Slide(false);
        }
        m_Jumping = false;
    }

    // Süper ayakkabıyı giydirir (yeniden alınırsa süre baştan başlar): her ayak kemiğine bir model kopyası.
    public void SuperAyakkabiGiy(SuperAyakkabi bonus)
    {
        superAyakkabiKalan = k_SuperAyakkabiSure;
        m_AyakkabiIkon = bonus.ikon;
        if (bonus.alinmaSesi != null && m_Audio != null) m_Audio.PlayOneShot(bonus.alinmaSesi);
        SuperAyakkabi.Goster(m_AyakkabiIkon, 1f);
        if (m_GiyilenAyakkabi != null && m_GiyilenAyakkabi[0] != null) return;
        if (bonus.giyilenModel == null || character == null || character.animator == null) return;
        var ayaklar = new[] { AyakBul(HumanBodyBones.LeftFoot, "LeftFoot"), AyakBul(HumanBodyBones.RightFoot, "RightFoot") };
        m_GiyilenAyakkabi = new GameObject[ayaklar.Length];
        for (int i = 0; i < ayaklar.Length; ++i)
        {
            if (ayaklar[i] == null) continue;
            // karakterin altında (onunla silinir); ayak kemiğini konumca izler, yönü hep koşu yönü (AyakTakip)
            var a = Instantiate(bonus.giyilenModel, character.transform, false);
            a.AddComponent<AyakTakip>().ayak = ayaklar[i];
            m_GiyilenAyakkabi[i] = a;
        }
    }

    Transform AyakBul(HumanBodyBones kemik, string ad)
    {
        Transform t = character.animator.isHuman ? character.animator.GetBoneTransform(kemik) : null;
        if (t != null) return t;
        foreach (var c in character.GetComponentsInChildren<Transform>(true)) if (c.name.EndsWith(ad)) return c;
        return null;
    }

    public void AyakkabiCikar()
    {
        superAyakkabiKalan = 0f;
        SuperAyakkabi.Gizle();
        if (m_GiyilenAyakkabi == null) return;
        foreach (var a in m_GiyilenAyakkabi) if (a != null) Destroy(a);
        m_GiyilenAyakkabi = null;
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
            if (tutorialHoldMove)
            {
                m_TutorialHeldMove = move;
                return;
            }
        }

        PerformMove(move);
    }

    public void ReleaseTutorialMove()
    {
        MoveInput move = m_TutorialHeldMove;
        m_TutorialHeldMove = MoveInput.None;
        PerformMove(move);
    }

    public void ClearTutorialMove()
    {
        m_TutorialHeldMove = MoveInput.None;
    }

    // Tutorial retry: puts the character back on the lane the step starts from.
    public void SetTutorialLane(int lane)
    {
        m_CurrentLane = Mathf.Clamp(lane, 0, 2);
        m_TargetPosition = new Vector3((m_CurrentLane - 1) * trackManager.laneOffset, 0, 0);
    }

    protected void PerformMove(MoveInput move)
    {
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
        if (superAyakkabiKalan > 0f && trackManager != null && trackManager.isMoving)
        {
            superAyakkabiKalan -= Time.deltaTime;
            if (superAyakkabiKalan <= 0f) AyakkabiCikar();
            else SuperAyakkabi.Goster(m_AyakkabiIkon, superAyakkabiKalan / k_SuperAyakkabiSure);
        }
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

        float zemin = ZeminOlc();
        if (!m_Jumping)
            TabanGuncelle(zemin);
        verticalTargetPosition.y = m_Taban;

        if(m_Jumping)
        {
			if (trackManager.isMoving)
			{
                // Same as with the sliding, we want a fixed jump LENGTH not fixed jump TIME. Also, just as with sliding,
                // we slightly modify length with speed to make it more playable.
				float correctJumpLength = jumpLength * m_ZiplamaUzunCarpan * (1.0f + trackManager.speedRatio);
				float ratio = (trackManager.worldDistance - m_JumpStart) / correctJumpLength;
				float jumpY = m_ZiplamaTabani + Mathf.Sin(ratio * Mathf.PI) * jumpHeight * m_ZiplamaYukCarpan;
				// Coming down onto a higher surface (a ramp or a roof) ends the jump there.
				bool landed = ratio > 0.5f && ratio < 1.0f && jumpY <= zemin && zemin - jumpY <= k_TirmanmaAdimi;
				if (ratio >= 1.0f || landed)
				{
					m_Jumping = false;
					character.animator.SetBool(s_JumpingHash, false);
					m_Taban = landed ? zemin : m_ZiplamaTabani;
					m_DusmeHizi = 0f;
					verticalTargetPosition.y = m_Taban;
				}
				else
				{
					verticalTargetPosition.y = jumpY;
				}
			}
			else if(!AudioListener.pause)//use AudioListener.pause as it is an easily accessible singleton & it is set when the app is in pause too
			{
			    verticalTargetPosition.y = Mathf.MoveTowards (verticalTargetPosition.y, m_ZiplamaTabani, k_GroundingSpeed * Time.deltaTime);
				if (Mathf.Approximately(verticalTargetPosition.y, m_ZiplamaTabani))
				{
					character.animator.SetBool(s_JumpingHash, false);
					m_Jumping = false;
					m_Taban = m_ZiplamaTabani;
				}
			}
        }

        characterCollider.transform.localPosition = Vector3.MoveTowards(characterCollider.transform.localPosition, verticalTargetPosition, laneChangeSpeed * Time.deltaTime);
        KameraYukselt();

        // Put blob shadow under the character.
        RaycastHit hit;
        if(Physics.Raycast(characterCollider.transform.position + Vector3.up, Vector3.down, out hit, k_ShadowRaycastDistance, m_ObstacleLayer | (1 << k_ZeminKatmani)))
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

    // Height of the highest walkable surface under the character, relative to the track (0 when there is none).
    protected float ZeminOlc()
    {
        Vector3 ayak = characterCollider.transform.position;
        float iz = transform.position.y;
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(ayak.x, iz + k_ZeminIsini, ayak.z), Vector3.down, out hit, k_ZeminIsini + 1f, 1 << k_ZeminKatmani, QueryTriggerInteraction.Collide))
            return Mathf.Max(0f, hit.point.y - iz);
        return 0f;
    }

    // Standing height when not jumping: up a gentle step at once, down by falling.
    protected void TabanGuncelle(float zemin)
    {
        if (zemin >= m_Taban)
        {
            if (zemin - m_Taban <= k_TirmanmaAdimi)
                m_Taban = zemin;
            m_DusmeHizi = 0f;
        }
        else
        {
            // havadayken aşağı kaydırmak (kayma) hızla yere indirir
            m_DusmeHizi += k_Yercekimi * (m_Sliding ? k_HizliDusus : 1f) * Time.deltaTime;
            m_Taban = Mathf.Max(zemin, m_Taban - m_DusmeHizi * Time.deltaTime);
            if (m_Taban <= zemin)
                m_DusmeHizi = 0f;
        }
    }

    // The camera is a child of this object; it follows part of the standing height so a roof run stays in view.
    protected void KameraYukselt()
    {
        Camera cam = Camera.main;
        if (cam == null || cam.transform.parent != transform)
            return;
        float hedef = Mathf.MoveTowards(m_KameraYukselme, m_Taban * k_KameraTakip, 6f * Time.deltaTime);
        if (Mathf.Approximately(hedef, m_KameraYukselme))
            return;
        cam.transform.localPosition += Vector3.up * (hedef - m_KameraYukselme);
        m_KameraYukselme = hedef;
    }

    public void Jump()
    {
	    if (!m_IsRunning)
		    return;
	    
        if (!m_Jumping)
        {
			if (m_Sliding)
				StopSliding();

			bool super = superAyakkabiKalan > 0f;
			m_ZiplamaYukCarpan = super ? k_SuperZiplamaYuk : 1f;
			m_ZiplamaUzunCarpan = super ? k_SuperZiplamaUzun : 1f;
			float correctJumpLength = jumpLength * m_ZiplamaUzunCarpan * (1.0f + trackManager.speedRatio);
			m_JumpStart = trackManager.worldDistance;
			m_ZiplamaTabani = m_Taban;
			m_DusmeHizi = 0f;
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

        m_OncekiSerit = m_CurrentLane;
        m_CurrentLane = targetLane;
        m_TargetPosition = new Vector3((m_CurrentLane - 1) * trackManager.laneOffset, 0, 0);
    }

    // Şerit aralığı değişince (temaya göre) hedef şerit konumunu yeniden hesapla.
    public void SeritYenile()
    {
        m_TargetPosition = new Vector3((m_CurrentLane - 1) * trackManager.laneOffset, 0, 0);
    }

    // Yandan çarpma: karakter şerit değiştiriyor ve engelin önü karakterin önünün yeterince gerisinde kalmış
    // (karakter engelin yanından içeri giriyor). Önden çarpma (engel tam önünde) her zaman normal çarpmadır.
    public bool YandanCarpti(Collider engel)
    {
        Transform t = characterCollider.transform;
        if (Mathf.Abs(t.localPosition.x - m_TargetPosition.x) < k_YanEsik)
            return false;
        Bounds b = engel.bounds;
        float onu = Vector3.Dot(t.position - b.center, transform.forward) + Vector3.Dot(b.extents, new Vector3(Mathf.Abs(transform.forward.x), Mathf.Abs(transform.forward.y), Mathf.Abs(transform.forward.z)));
        return onu > k_YanDerinlik; // karakterin merkezi engelin ön yüzünü bu kadar geçmiş
    }

    // Eski şeride geri sek ve tökezle. Son tökezlemeden k_TokezSure içinde ikinci kez olursa false (normal çarpma).
    public bool Tokezle()
    {
        if (Time.time - m_SonTokezleme < k_TokezSure)
            return false;
        m_SonTokezleme = Time.time;
        m_CurrentLane = m_OncekiSerit;
        m_TargetPosition = new Vector3((m_CurrentLane - 1) * trackManager.laneOffset, 0, 0);
        character.animator.SetTrigger(s_TokezHash);
        tokezlemeSayisi++;
        return true;
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
