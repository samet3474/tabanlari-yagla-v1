using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.AddressableAssets;
using UnityEngine.Analytics;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using GameObject = UnityEngine.GameObject;

#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif

/// <summary>
/// The TrackManager handles creating track segments, moving them and handling the whole pace of the game.
/// 
/// The cycle is as follows:
/// - Begin is called when the game starts.
///     - if it's a first run, init the controller, collider etc. and start the movement of the track.
///     - if it's a rerun (after watching ads on GameOver) just restart the movement of the track.
/// - Update moves the character and - if the character reaches a certain distance from origin (given by floatingOriginThreshold) -
/// moves everything back by that threshold to "reset" the player to the origin. This allow to avoid floating point error on long run.
/// It also handles creating the tracks segements when needed.
/// 
/// If the player has no more lives, it pushes the GameOver state on top of the GameState without removing it. That way we can just go back to where
/// we left off if the player watches an ad and gets a second chance. If the player quits, then:
/// 
/// - End is called and everything is cleared and destroyed, and we go back to the Loadout State.
/// </summary>
public class TrackManager : MonoBehaviour
{
    static public TrackManager instance { get { return s_Instance; } }
    static protected TrackManager s_Instance;

    static int s_StartHash = Animator.StringToHash("Start");

    public delegate int MultiplierModifier(int current);
    public MultiplierModifier modifyMultiply;

    [Header("Character & Movements")]
    public CharacterInputController characterController;
    public float minSpeed = 5.0f;
    public float maxSpeed = 10.0f;
    public int speedStep = 4;
    public float laneOffset = 1.0f;

    public bool invincible = false;

    [Header("Objects")]
    public ConsumableDatabase consumableDatabase;
    public MeshFilter skyMeshFilter;

    [Header("Parallax")]
    public Transform parallaxRoot;
    public float parallaxRatio = 0.5f;

    [Header("Tutorial")]
    public ThemeData tutorialThemeData;

    public System.Action<TrackSegment> newSegmentCreated;
    public System.Action<TrackSegment> currentSegementChanged;

    public int trackSeed { get { return m_TrackSeed; } set { m_TrackSeed = value; } }

    public float timeToStart { get { return m_TimeToStart; } }  // Will return -1 if already started (allow to update UI)

    public int score { get { return m_Score; } }
    public int multiplier { get { return m_Multiplier; } }
    public float currentSegmentDistance { get { return m_CurrentSegmentDistance; } }
    public float worldDistance { get { return m_TotalWorldDistance; } }
    public float speed { get { return m_Speed; } }
    public float speedRatio { get { return (m_Speed - minSpeed) / (maxSpeed - minSpeed); } }
    public int currentZone { get { return m_CurrentZone; } }

    public TrackSegment currentSegment { get { return m_Segments[0]; } }
    public List<TrackSegment> segments { get { return m_Segments; } }
    public ThemeData currentTheme { get { return m_CurrentThemeData; } }

    public bool isMoving { get { return m_IsMoving; } }
    public bool isRerun { get { return m_Rerun; } set { m_Rerun = value; } }

    public bool isTutorial { get { return m_IsTutorial; } set { m_IsTutorial = value; } }
    public bool isLoaded { get; set; }
    //used by the obstacle spawning code in the tutorial, as it need to spawn the 1st obstacle in the middle lane
    public bool firstObstacle { get; set; }

    protected float m_TimeToStart = -1.0f;

    // If this is set to -1, random seed is init to system clock, otherwise init to that value
    // Allow to play the same game multiple time (useful to make specific competition/challenge fair between players)
    protected int m_TrackSeed = -1;

    protected float m_CurrentSegmentDistance;
    protected float m_TotalWorldDistance;
    protected bool m_IsMoving;
    protected float m_Speed;

    protected float m_TimeSincePowerup;     // The higher it goes, the higher the chance of spawning one
    protected float m_TimeSinceLastPremium;

    protected int m_Multiplier;

    protected List<TrackSegment> m_Segments = new List<TrackSegment>();
    protected List<TrackSegment> m_PastSegments = new List<TrackSegment>();
    // Segment instantiations still in flight; End() releases them since their spawn coroutine is stopped with the run.
    protected List<AsyncOperationHandle> m_LoadingSegments = new List<AsyncOperationHandle>();
    // Segments are placed in the order they were picked: several spawns start in the same frame and copies of the same
    // prefab finish loading together, so placing on completion would put them side by side (pairs at the run start).
    protected int m_SegmentTicket;
    protected int m_NextSegmentToPlace;
    protected int m_SafeSegementLeft;

    protected ThemeData m_CurrentThemeData;
    protected int m_CurrentZone;
    protected float m_CurrentZoneDistance;
    protected int m_PreviousSegment = -1;

    protected int m_Score;
    protected float m_ScoreAccum;
    protected bool m_Rerun;     // This lets us know if we are entering a game over (ads) state or starting a new game (see GameState)

    protected bool m_IsTutorial; //Tutorial is a special run that don't chance section until the tutorial step is "validated" by the TutorialState.
    
    Vector3 m_CameraOriginalPos = Vector3.zero;
    
    const float k_FloatingOriginThreshold = 10000f;

    protected const float k_CountdownToStartLength = 5f;
    protected const float k_CountdownSpeed = 1.5f;
    protected const float k_StartingSegmentDistance = 2f;
    protected const float k_TemaBaslangicMesafesi = 14f;
    protected const float k_SuperAyakkabiPayi = 0.4f; // bonus çıktığında süper ayakkabı olma olasılığı (tasarlanmış parçalar)
    protected const int k_StartingSafeSegments = 2;
    protected const int k_StartingCoinPoolSize = 256;
    protected const int k_DesiredSegmentCount = 10;
    protected const float k_SegmentRemovalDistance = -30f;
    protected const float k_Acceleration = 0.2f;
    protected const float k_ObstacleSpawnTimeout = 5f;
    // Where the scene's linear fog starts (Main scene: 50-80 m). Track content placed beyond it fades in instead of popping.
    protected const float k_TutorialFillDistance = 50f;
    
    protected void Awake()
    {
        m_ScoreAccum = 0.0f;
        s_Instance = this;
    }

    public void StartMove(bool isRestart = true)
    {
        characterController.StartMoving();
        m_IsMoving = true;
        if (isRestart)
            m_Speed = minSpeed;
    }

    public void StopMove()
    {
        m_IsMoving = false;
    }

    IEnumerator WaitToStart()
    {
        characterController.character.animator.Play(s_StartHash);
        float length = k_CountdownToStartLength;
        m_TimeToStart = length;

        while (m_TimeToStart >= 0)
        {
            yield return null;
            m_TimeToStart -= Time.deltaTime * k_CountdownSpeed;
        }

        m_TimeToStart = -1;

        if (m_Rerun)
        {
            // Make invincible on rerun, to avoid problems if the character died in front of an obstacle
            characterController.characterCollider.SetInvincible();
        }

        characterController.StartRunning();
        StartMove();
    }

    public IEnumerator Begin()
    {
        if (!m_Rerun)
        {
            firstObstacle = true;
            m_CameraOriginalPos = Camera.main.transform.position;
            
            if (m_TrackSeed != -1)
                Random.InitState(m_TrackSeed);
            else
                Random.InitState((int)System.DateTime.Now.Ticks);

            // Since this is not a rerun, init the whole system (on rerun we want to keep the states we had on death)
            m_CurrentSegmentDistance = k_StartingSegmentDistance;
            m_TotalWorldDistance = 0.0f;

            characterController.gameObject.SetActive(true);

            //Addressables 1.0.1-preview
            // Spawn the player
            var op = Addressables.InstantiateAsync(PlayerData.instance.characters[PlayerData.instance.usedCharacter],
                Vector3.zero,
                Quaternion.identity);
            yield return op;
            if (op.Result == null || !(op.Result is GameObject))
            {
                Debug.LogWarning(string.Format("Unable to load character {0}.", PlayerData.instance.characters[PlayerData.instance.usedCharacter]));
                yield break;
            }
            Character player = op.Result.GetComponent<Character>();

            player.SetupAccesory(PlayerData.instance.usedAccessory);

            characterController.character = player;
            characterController.trackManager = this;

            characterController.Init();
            characterController.CheatInvincible(invincible);
            
            //Instantiate(CharacterDatabase.GetCharacter(PlayerData.instance.characters[PlayerData.instance.usedCharacter]), Vector3.zero, Quaternion.identity);
            player.transform.SetParent(characterController.characterCollider.transform, false);
            Camera.main.transform.SetParent(characterController.transform, true);

            if (m_IsTutorial)
                m_CurrentThemeData = tutorialThemeData;
            else
                m_CurrentThemeData = ThemeDatabase.GetThemeData(PlayerData.instance.themes[PlayerData.instance.usedTheme]);

            m_CurrentZone = 0;
            m_CurrentZoneDistance = 0;
            m_PreviousSegment = -1;
            TemaPistiUygula();
            // alçak tema kamerası koşu başında ilk parçanın gerisini (boşluğu) görmesin: karakter ilk parçanın içinden başlar
            if (!m_IsTutorial && m_CurrentThemeData.seritAraligi > 0.0f) m_CurrentSegmentDistance = k_TemaBaslangicMesafesi;

            skyMeshFilter.sharedMesh = m_CurrentThemeData.skyMesh;
            RenderSettings.fogColor = m_CurrentThemeData.fogColor;
            RenderSettings.fog = true;

            gameObject.SetActive(true);
            characterController.gameObject.SetActive(true);
            characterController.coins = 0;
            characterController.premium = 0;

            m_Score = 0;
            m_ScoreAccum = 0;

            m_SafeSegementLeft = m_IsTutorial ? 0 : k_StartingSafeSegments;

            ReleaseOrphanObstacles();

            Coin.coinPool = new Pooler(currentTheme.collectiblePrefab, k_StartingCoinPoolSize);

            PlayerData.instance.StartRunMissions(this);

#if UNITY_ANALYTICS
            AnalyticsEvent.GameStart(new Dictionary<string, object>
            {
                { "theme", m_CurrentThemeData.themeName},
                { "character", player.characterName },
                { "accessory",  PlayerData.instance.usedAccessory >= 0 ? player.accessories[PlayerData.instance.usedAccessory].accessoryName : "none"}
            });
#endif
        }

        characterController.Begin();
        StartCoroutine(WaitToStart());
        isLoaded = true;
    }

    public void End()
    {
        m_IsMoving = false;

        // Attracted coins are destroyed with their segment below; the character is re-enabled before Init() on the
        // next run, so a stale list would be iterated in CharacterCollider.Update.
        characterController.characterCollider.magnetCoins.Clear();

        foreach (TrackSegment seg in m_Segments)
        {
            Addressables.ReleaseInstance(seg.gameObject);
        }

        for (int i = 0; i < m_PastSegments.Count; ++i)
        {
            Addressables.ReleaseInstance(m_PastSegments[i].gameObject);
        }

        // Deactivating this object below stops the SpawnNewSegment coroutines, so segments still loading would
        // otherwise appear later with no owner and stay counted in _spawnedSegments forever.
        for (int i = 0; i < m_LoadingSegments.Count; ++i)
        {
            AsyncOperationHandle op = m_LoadingSegments[i];
            if (!op.IsValid())
                continue;

            if (op.IsDone)
                ReleaseSegmentInstance(op);
            else
                op.Completed += ReleaseSegmentInstance;
        }

        m_LoadingSegments.Clear();
        m_Segments.Clear();
        m_PastSegments.Clear();
        _spawnedSegments = 0;
        m_SegmentTicket = 0;
        m_NextSegmentToPlace = 0;

        // Coins attached to the released segments are destroyed with them; the free ones would leak since
        // the next run creates a new pool.
        if (Coin.coinPool != null)
            Coin.coinPool.Clear();

        characterController.End();

        gameObject.SetActive(false);
        Addressables.ReleaseInstance(characterController.character.gameObject);
        characterController.character = null;

        TemaPistiGeriAl();
        Camera.main.transform.SetParent(null);
        Camera.main.transform.position = m_CameraOriginalPos;

        characterController.gameObject.SetActive(false);

        for (int i = 0; i < parallaxRoot.childCount; ++i)
        {
            _parallaxRootChildren--;
            Destroy(parallaxRoot.GetChild(i).gameObject);
        }

        //if our consumable wasn't used, we put it back in our inventory
        if (characterController.inventory != null)
        {
            PlayerData.instance.Add(characterController.inventory.GetConsumableType());
            characterController.inventory = null;
        }
    }


    private int _parallaxRootChildren = 0;
    private int _spawnedSegments = 0;
    void Update()
    {
        while (_spawnedSegments < (m_IsTutorial ? 4 : k_DesiredSegmentCount))
        {
            StartCoroutine(SpawnNewSegment());
            _spawnedSegments++;
        }

        if (parallaxRoot != null && currentTheme.cloudPrefabs.Length > 0)
        {
            while (_parallaxRootChildren < currentTheme.cloudNumber)
            {
                float lastZ = parallaxRoot.childCount == 0 ? 0 : parallaxRoot.GetChild(parallaxRoot.childCount - 1).position.z + currentTheme.cloudMinimumDistance.z;

                GameObject cloud = currentTheme.cloudPrefabs[Random.Range(0, currentTheme.cloudPrefabs.Length)];
                if (cloud != null)
                {
                    GameObject obj = Instantiate(cloud);
                    obj.transform.SetParent(parallaxRoot, false);

                    obj.transform.localPosition =
                        Vector3.up * (currentTheme.cloudMinimumDistance.y +
                                      (Random.value - 0.5f) * currentTheme.cloudSpread.y)
                        + Vector3.forward * (lastZ + (Random.value - 0.5f) * currentTheme.cloudSpread.z)
                        + Vector3.right * (currentTheme.cloudMinimumDistance.x +
                                           (Random.value - 0.5f) * currentTheme.cloudSpread.x);

                    obj.transform.localScale = obj.transform.localScale * (1.0f + (Random.value - 0.5f) * 0.5f);
                    obj.transform.localRotation = Quaternion.AngleAxis(Random.value * 360.0f, Vector3.up);
                    _parallaxRootChildren++;
                }
            }
        }

        if (!m_IsMoving)
        {
            // geri sayımda da karakter ilk parçanın içindeki başlangıç noktasında dursun (k_TemaBaslangicMesafesi); yoksa alçak
            // tema kamerası parçanın gerisindeki boşluğu görür. Yalnız koşu hiç ilerlememişken.
            if (!m_IsTutorial && m_TotalWorldDistance <= 0.0f && m_Segments.Count > 0 && m_Segments[0] != null && m_CurrentThemeData != null && m_CurrentThemeData.seritAraligi > 0.0f)
            {
                m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out Vector3 basPos, out Quaternion basRot);
                characterController.transform.SetPositionAndRotation(basPos, basRot);
            }
            return;
        }

        float scaledSpeed = m_Speed * Time.deltaTime;
        m_ScoreAccum += scaledSpeed;
        m_CurrentZoneDistance += scaledSpeed;

        int intScore = Mathf.FloorToInt(m_ScoreAccum);
        if (intScore != 0) AddScore(intScore);
        m_ScoreAccum -= intScore;

        m_TotalWorldDistance += scaledSpeed;
        m_CurrentSegmentDistance += scaledSpeed;

        if (m_CurrentSegmentDistance > m_Segments[0].worldLength)
        {
            m_CurrentSegmentDistance -= m_Segments[0].worldLength;

            // m_PastSegments are segment we already passed, we keep them to move them and destroy them later 
            // but they aren't part of the game anymore 
            m_PastSegments.Add(m_Segments[0]);
            m_Segments.RemoveAt(0);
            _spawnedSegments--;

            if (currentSegementChanged != null) currentSegementChanged.Invoke(m_Segments[0]);
        }

        Vector3 currentPos;
        Quaternion currentRot;
        Transform characterTransform = characterController.transform;

        m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out currentPos, out currentRot);


        // Floating origin implementation
        // Move the whole world back to 0,0,0 when we get too far away.
        bool needRecenter = currentPos.sqrMagnitude > k_FloatingOriginThreshold;

        // Parallax Handling
        if (parallaxRoot != null)
        {
            Vector3 difference = (currentPos - characterTransform.position) * parallaxRatio; ;
            int count = parallaxRoot.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform cloud = parallaxRoot.GetChild(i);
                cloud.position += difference - (needRecenter ? currentPos : Vector3.zero);
            }
        }

        if (needRecenter)
        {
            int count = m_Segments.Count;
            for (int i = 0; i < count; i++)
            {
                m_Segments[i].transform.position -= currentPos;
            }

            count = m_PastSegments.Count;
            for (int i = 0; i < count; i++)
            {
                m_PastSegments[i].transform.position -= currentPos;
            }

            // Recalculate current world position based on the moved world
            m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out currentPos, out currentRot);
        }

        characterTransform.rotation = currentRot;
        characterTransform.position = currentPos;

        if (parallaxRoot != null && currentTheme.cloudPrefabs.Length > 0)
        {
            for (int i = 0; i < parallaxRoot.childCount; ++i)
            {
                Transform child = parallaxRoot.GetChild(i);

                // Destroy unneeded clouds
                if ((child.localPosition - currentPos).z < -50)
                {
                    _parallaxRootChildren--;
                    Destroy(child.gameObject);
                }
            }
        }

        // Still move past segment until they aren't visible anymore.
        for (int i = 0; i < m_PastSegments.Count; ++i)
        {
            if ((m_PastSegments[i].transform.position - currentPos).z < k_SegmentRemovalDistance)
            {
                m_PastSegments[i].Cleanup();
                m_PastSegments.RemoveAt(i);
                i--;
            }
        }

        PowerupSpawnUpdate();

        if (!m_IsTutorial)
        {
            if (m_Speed < maxSpeed)
                m_Speed += k_Acceleration * Time.deltaTime;
            else
                m_Speed = maxSpeed;
        }

        m_Multiplier = 1 + Mathf.FloorToInt((m_Speed - minSpeed) / (maxSpeed - minSpeed) * speedStep);

        if (modifyMultiply != null)
        {
            foreach (MultiplierModifier part in modifyMultiply.GetInvocationList())
            {
                m_Multiplier = part(m_Multiplier);
            }
        }

        if (!m_IsTutorial)
        {
            //check for next rank achieved
            int currentTarget = (PlayerData.instance.rank + 1) * 300;
            if (m_TotalWorldDistance > currentTarget)
            {
                PlayerData.instance.rank += 1;
                PlayerData.instance.Save();
#if UNITY_ANALYTICS
//"level" in our game are milestone the player have to reach : one every 300m
            AnalyticsEvent.LevelUp(PlayerData.instance.rank);
#endif
            }

            PlayerData.instance.UpdateMissions(this);
        }

        MusicPlayer.instance.UpdateVolumes(speedRatio);
    }

    public void PowerupSpawnUpdate()
    {
        m_TimeSincePowerup += Time.deltaTime;
        m_TimeSinceLastPremium += Time.deltaTime;
    }

    // Turns the running tutorial into a normal run without restarting it: segments already spawned stay, new ones
    // come from the player's theme, and coins, acceleration and missions start working.
    public void ContinueAsNormalRun()
    {
        if (!m_IsTutorial)
            return;

        m_IsTutorial = false;

        ThemeData theme = ThemeDatabase.GetThemeData(PlayerData.instance.themes[PlayerData.instance.usedTheme]);
        if (theme != null)
        {
            // No coins are spawned during the tutorial, so the whole pool is free and can be replaced safely.
            if (theme.collectiblePrefab != m_CurrentThemeData.collectiblePrefab)
            {
                Coin.coinPool.Clear();
                Coin.coinPool = new Pooler(theme.collectiblePrefab, k_StartingCoinPoolSize);
            }

            m_CurrentThemeData = theme;
            skyMeshFilter.sharedMesh = theme.skyMesh;
            RenderSettings.fogColor = theme.fogColor;
        }

        m_CurrentZone = 0;
        m_CurrentZoneDistance = 0;
        m_PreviousSegment = -1;
        TemaPistiUygula();

        // The tutorial's segments were spawned empty. The ones that still start beyond the fog get their obstacles and
        // coins now, where they cannot be seen appearing; the nearer ones stay a short obstacle-free run-up, like the
        // safe segments at the start of a normal run. Segments still loading are filled when they arrive.
        float segmentStart = -m_CurrentSegmentDistance;
        for (int i = 0; i < m_Segments.Count; ++i)
        {
            if (segmentStart >= k_TutorialFillDistance)
                SpawnObstacle(m_Segments[i]);
            segmentStart += m_Segments[i].worldLength;
        }
    }

    // Preload: segment prefabs of a theme are loaded once in the background (menu) and kept, so the first run's
    // segments instantiate without waiting for the bundle download. Only themes with designed pieces need it, but
    // loading any theme's prefabs is harmless (they are loaded at run start anyway).
    static readonly HashSet<object> s_OnYuklenen = new HashSet<object>();
    public static void TemaOnYukle(ThemeData tema)
    {
        if (tema == null || tema.zones == null || tema.seritAraligi <= 0.0f) // yalnız özel pistli temalar (İstanbul)
            return;
        foreach (ThemeZone z in tema.zones)
            foreach (AssetReference r in z.prefabList)
            {
                if (r == null || !r.RuntimeKeyIsValid() || !s_OnYuklenen.Add(r.RuntimeKey))
                    continue;
                Addressables.LoadAssetAsync<GameObject>(r.RuntimeKey); // tutamaç bırakılmaz: paket ve önek bellekte kalır
            }
    }

    // Theme-specific track: lane spacing and run camera (ThemeData.seritAraligi / kameraOfset / kameraEgimEk).
    // Themes that leave them at 0 (Day, Night, tutorial) keep the scene values unchanged.
    float m_VarsayilanSerit = -1.0f;
    Vector3 m_UygulananKameraOfset;
    float m_UygulananKameraEgim;

    void TemaPistiUygula()
    {
        if (m_VarsayilanSerit < 0.0f)
            m_VarsayilanSerit = laneOffset;
        laneOffset = m_CurrentThemeData.seritAraligi > 0.0f ? m_CurrentThemeData.seritAraligi : m_VarsayilanSerit;

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null && cam.parent == characterController.transform)
        {
            cam.localPosition += m_CurrentThemeData.kameraOfset - m_UygulananKameraOfset;
            cam.localRotation = Quaternion.Euler(m_CurrentThemeData.kameraEgimEk - m_UygulananKameraEgim, 0.0f, 0.0f) * cam.localRotation;
            m_UygulananKameraOfset = m_CurrentThemeData.kameraOfset;
            m_UygulananKameraEgim = m_CurrentThemeData.kameraEgimEk;
        }
        characterController.SeritYenile();
    }

    void TemaPistiGeriAl()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null)
            cam.localRotation = Quaternion.Euler(-m_UygulananKameraEgim, 0.0f, 0.0f) * cam.localRotation;
        m_UygulananKameraOfset = Vector3.zero;
        m_UygulananKameraEgim = 0.0f;
        if (m_VarsayilanSerit >= 0.0f)
            laneOffset = m_VarsayilanSerit;
    }

    public void ChangeZone()
    {
        m_CurrentZone += 1;
        if (m_CurrentZone >= m_CurrentThemeData.zones.Length)
            m_CurrentZone = 0;

        m_CurrentZoneDistance = 0;
        // Segment indices belong to the zone's own prefab list, so the previous one means nothing in the new zone.
        m_PreviousSegment = -1;
    }

    private readonly Vector3 _offScreenSpawnPos = new Vector3(-100f, -100f, -100f);
    public IEnumerator SpawnNewSegment()
    {
        if (!m_IsTutorial)
        {
            if (m_CurrentThemeData.zones[m_CurrentZone].length < m_CurrentZoneDistance)
                ChangeZone();
        }

        int segmentUse = Random.Range(0, m_CurrentThemeData.zones[m_CurrentZone].prefabList.Length);
        if (segmentUse == m_PreviousSegment) segmentUse = (segmentUse + 1) % m_CurrentThemeData.zones[m_CurrentZone].prefabList.Length;
        m_PreviousSegment = segmentUse;
        int ticket = m_SegmentTicket++;
        AssetReference segmentRef = m_CurrentThemeData.zones[m_CurrentZone].prefabList[segmentUse];

        AsyncOperationHandle segmentToUseOp = segmentRef.InstantiateAsync(_offScreenSpawnPos, Quaternion.identity);
        m_LoadingSegments.Add(segmentToUseOp);
        yield return segmentToUseOp;
        // Wait for the segments picked before this one (End() resets both counters along with these coroutines).
        while (m_NextSegmentToPlace < ticket)
            yield return null;
        RemoveLoadingSegment(segmentToUseOp);
        if (segmentToUseOp.Result == null || !(segmentToUseOp.Result is GameObject))
        {
            Debug.LogWarning(string.Format("Unable to load segment {0}.", segmentRef.Asset != null ? segmentRef.Asset.name : segmentRef.RuntimeKey));
            // This spawn was counted in Update but will never be added to m_Segments.
            _spawnedSegments--;
            m_NextSegmentToPlace++;
            yield break;
        }
        TrackSegment newSegment = (segmentToUseOp.Result as GameObject).GetComponent<TrackSegment>();

        Vector3 currentExitPoint;
        Quaternion currentExitRotation;
        if (m_Segments.Count > 0)
        {
            m_Segments[m_Segments.Count - 1].GetPointAt(1.0f, out currentExitPoint, out currentExitRotation);
        }
        else
        {
            currentExitPoint = transform.position;
            currentExitRotation = transform.rotation;
        }

        newSegment.transform.rotation = currentExitRotation;

        Vector3 entryPoint;
        Quaternion entryRotation;
        newSegment.GetPointAt(0.0f, out entryPoint, out entryRotation);


        Vector3 pos = currentExitPoint + (newSegment.transform.position - entryPoint);
        newSegment.transform.position = pos;
        newSegment.manager = this;

        newSegment.transform.localScale = new Vector3((Random.value > 0.5f ? -1 : 1), 1, 1);
        newSegment.objectRoot.localScale = new Vector3(1.0f / newSegment.transform.localScale.x, 1, 1);

        // Tutorial segments get no random obstacles or coins: GameState places the tutorial's own obstacles one at a time
        // (SpawnObstacleAt). ContinueAsNormalRun fills the segments still far ahead when the tutorial ends.
        if (!m_IsTutorial)
        {
            if (m_SafeSegementLeft <= 0)
                SpawnObstacle(newSegment);
            else
                m_SafeSegementLeft -= 1;
        }

        m_Segments.Add(newSegment);
        m_NextSegmentToPlace++;

        if (newSegmentCreated != null) newSegmentCreated.Invoke(newSegment);
    }


    void RemoveLoadingSegment(AsyncOperationHandle op)
    {
        for (int i = 0; i < m_LoadingSegments.Count; ++i)
        {
            if (m_LoadingSegments[i].Equals(op))
            {
                m_LoadingSegments.RemoveAt(i);
                return;
            }
        }
    }

    static void ReleaseSegmentInstance(AsyncOperationHandle op)
    {
        GameObject obj = op.Result as GameObject;
        if (obj != null)
            Addressables.ReleaseInstance(obj);
    }

    // Obstacle instantiations still in flight when a run ends finish after their spawn coroutine was stopped, so they
    // never get parented to a segment and would stay on the track of the next run.
    void ReleaseOrphanObstacles()
    {
        Obstacle[] obstacles = FindObjectsByType<Obstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < obstacles.Length; ++i)
        {
            if (obstacles[i].GetComponentInParent<TrackSegment>(true) != null)
                continue;

            if (!Addressables.ReleaseInstance(obstacles[i].gameObject))
                Destroy(obstacles[i].gameObject);
        }
    }

    // Places one obstacle prefab at a track distance (m from the run start, same scale as worldDistance) and lane
    // (-1 left, 0 middle, 1 right), the way SimpleBarricade and AllLaneObstacle place theirs: on the segment's path,
    // shifted by laneOffset, parented to the segment's objectRoot (so it moves and is released with the segment).
    // Returns false when no spawned segment covers that distance yet; onSpawned gets null if loading failed or the
    // segment went away meanwhile.
    public bool SpawnObstacleAt(object obstacleKey, float trackDistance, int lane, System.Action<GameObject> onSpawned)
    {
        float segmentStart = m_TotalWorldDistance - m_CurrentSegmentDistance;
        for (int i = 0; i < m_Segments.Count; ++i)
        {
            TrackSegment segment = m_Segments[i];
            if (trackDistance < segmentStart + segment.worldLength)
            {
                float t = (trackDistance - segmentStart) / segment.worldLength;
                StartCoroutine(SpawnObstacleAtRoutine(obstacleKey, segment, t, lane, onSpawned));
                return true;
            }
            segmentStart += segment.worldLength;
        }
        return false;
    }

    IEnumerator SpawnObstacleAtRoutine(object obstacleKey, TrackSegment segment, float t, int lane, System.Action<GameObject> onSpawned)
    {
        Vector3 position;
        Quaternion rotation;
        segment.GetPointAt(t, out position, out rotation);

        AsyncOperationHandle<GameObject> op = Addressables.InstantiateAsync(obstacleKey, position, rotation);
        yield return op;
        GameObject obj = op.Result;
        if (obj == null)
        {
            Debug.LogWarning(string.Format("Unable to load obstacle {0}.", obstacleKey));
            onSpawned(null);
            yield break;
        }
        if (segment == null)
        {
            Addressables.ReleaseInstance(obj);
            onSpawned(null);
            yield break;
        }

        obj.transform.position += obj.transform.right * lane * laneOffset;
        obj.transform.SetParent(segment.objectRoot, true);

        //TODO : remove that hack related to #issue7
        Vector3 oldPos = obj.transform.position;
        obj.transform.position += Vector3.back;
        obj.transform.position = oldPos;

        onSpawned(obj);
    }

    public void SpawnObstacle(TrackSegment segment)
    {
        // Designed pieces (TasarimliSatir) get fixed obstacles and coin patterns; every other segment is random as before.
        TasarimliSatir tasarim = segment.GetComponent<TasarimliSatir>();
        if (tasarim != null)
            StartCoroutine(SpawnTasarimli(segment, tasarim));
        else
            StartCoroutine(SpawnObstaclesThenCollectibles(segment));
    }

    // Obstacles in their fixed lanes (loaded by GUID through the AssetReference), then the coin patterns. No Random call.
    private IEnumerator SpawnTasarimli(TrackSegment segment, TasarimliSatir tasarim)
    {
        int pending = 0;
        System.Action<GameObject> onObstacleDone = o => pending--;
        // Mirrored segments (random flip in SpawnNewSegment) get the left-right mirrored variant of the design.
        int ayna = segment.transform.localScale.x < 0.0f ? -1 : 1;
        foreach (TasarimliSatir.Satir satir in tasarim.satirlar)
        {
            if (satir.engel == null || !satir.engel.RuntimeKeyIsValid())
            {
                Debug.LogWarning(string.Format("Designed row without obstacle on {0}.", segment.name));
                continue;
            }
            float t = satir.konum / segment.worldLength;
            for (int lane = -1; lane <= 1; ++lane)
            {
                if ((satir.seritler & (1 << (lane + 1))) == 0)
                    continue;
                pending++;
                StartCoroutine(SpawnObstacleAtRoutine(satir.engel.RuntimeKey, segment, t, lane * ayna, onObstacleDone));
            }
        }

        float timeout = k_ObstacleSpawnTimeout;
        while (pending > 0 && timeout > 0.0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // The segment may have been released while its obstacles were loading.
        if (segment == null || m_IsTutorial)
            yield break;

        // Bonuslar (mıknatıs, çarpan, kalkan, can…) ve premium, rastgele parçalardaki aynı şans kuralıyla, ama yalnız
        // tasarlanmış altın dizisinin üstünde (güvenli rotada) bir altının yerine çıkar.
        float powerupChance = Mathf.Clamp01(Mathf.Floor(m_TimeSincePowerup) * 0.5f * 0.001f);
        float premiumChance = Mathf.Clamp01(Mathf.Floor(m_TimeSinceLastPremium) * 0.5f * 0.0001f);
        foreach (TasarimliSatir.Altin desen in tasarim.altinlar)
        {
            foreach (var konum in TasarimliSatir.Konumlar(desen))
            {
                if (segment == null)
                    yield break;
                float m = konum.m;
                float lane = konum.serit * ayna; // satırlardaki ayna ile aynı
                float yukseklik = konum.y;

                Vector3 pos;
                Quaternion rot;
                segment.GetPointAtInWorldUnit(m, out pos, out rot);
                pos += rot * (Vector3.right * lane * laneOffset + Vector3.up * yukseklik);

                GameObject toUse = null;
                string bonus = null;
                if (Random.value < powerupChance && currentTheme.superAyakkabi != null && Random.value < k_SuperAyakkabiPayi)
                {
                    // bonus sırası süper ayakkabıya düştü (Subway'deki gibi en sık çıkan bonuslardan): altının yerine
                    m_TimeSincePowerup = 0.0f;
                    powerupChance = 0.0f;
                    toUse = Instantiate(currentTheme.superAyakkabi, pos, rot);
                    toUse.transform.SetParent(segment.transform, true);
                    continue;
                }
                else if (Random.value < powerupChance)
                {
                    int picked = Random.Range(0, consumableDatabase.consumbales.Length);
                    if (consumableDatabase.consumbales[picked].canBeSpawned)
                    {
                        m_TimeSincePowerup = 0.0f;
                        powerupChance = 0.0f;
                        bonus = consumableDatabase.consumbales[picked].gameObject.name;
                    }
                }
                else if (Random.value < premiumChance)
                {
                    m_TimeSinceLastPremium = 0.0f;
                    premiumChance = 0.0f;
                    bonus = currentTheme.premiumCollectible.name;
                }

                if (bonus != null)
                {
                    AsyncOperationHandle op = Addressables.InstantiateAsync(bonus, pos, rot);
                    yield return op;
                    if (op.Result == null || !(op.Result is GameObject) || segment == null)
                    {
                        if (op.Result is GameObject orphan) Addressables.ReleaseInstance(orphan);
                        Debug.LogWarning(string.Format("Unable to load collectable {0}.", bonus));
                        continue;
                    }
                    toUse = op.Result as GameObject;
                    toUse.transform.SetParent(segment.transform, true);
                }
                else
                {
                    toUse = Coin.coinPool.Get(pos, rot);
                    toUse.transform.SetParent(segment.collectibleTransform, true);
                }

                //TODO : remove that hack related to #issue7
                Vector3 oldPos = toUse.transform.position;
                toUse.transform.position += Vector3.back;
                toUse.transform.position = oldPos;
            }
        }
    }

    // Coins avoid obstacles with Physics.CheckSphere, so they must only be placed once every obstacle of the
    // segment has actually been instantiated (obstacles load and instantiate asynchronously through Addressables).
    private IEnumerator SpawnObstaclesThenCollectibles(TrackSegment segment)
    {
        if (segment.possibleObstacles.Length != 0)
        {
            int pending = segment.obstaclePositions.Length;
            System.Action onObstacleDone = () => pending--;

            for (int i = 0; i < segment.obstaclePositions.Length; ++i)
            {
                AssetReference assetRef = segment.possibleObstacles[Random.Range(0, segment.possibleObstacles.Length)];
                StartCoroutine(SpawnFromAssetReference(assetRef, segment, i, onObstacleDone));
            }

            // Safety net only: every spawn reports completion even when it fails, so this should never be reached.
            float timeout = k_ObstacleSpawnTimeout;
            while (pending > 0 && timeout > 0.0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        // The segment may have been released while its obstacles were loading.
        if (segment == null)
            yield break;

        yield return SpawnCoinAndPowerup(segment);
    }

    private IEnumerator SpawnFromAssetReference(AssetReference reference, TrackSegment segment, int posIndex, System.Action onDone)
    {
        try
        {
            AsyncOperationHandle op = Addressables.LoadAssetAsync<GameObject>(reference);
            yield return op;
            GameObject obj = op.Result as GameObject;
            if (obj != null)
            {
                Obstacle obstacle = obj.GetComponent<Obstacle>();
                if (obstacle != null)
                    yield return obstacle.Spawn(segment, segment.obstaclePositions[posIndex]);
            }
        }
        finally
        {
            onDone();
        }
    }

    public IEnumerator SpawnCoinAndPowerup(TrackSegment segment)
    {
        if (!m_IsTutorial)
        {
            const float increment = 1.5f;
            float currentWorldPos = 0.0f;
            int currentLane = Random.Range(0, 3);

            float powerupChance = Mathf.Clamp01(Mathf.Floor(m_TimeSincePowerup) * 0.5f * 0.001f);
            float premiumChance = Mathf.Clamp01(Mathf.Floor(m_TimeSinceLastPremium) * 0.5f * 0.0001f);

            while (currentWorldPos < segment.worldLength)
            {
                Vector3 pos;
                Quaternion rot;
                segment.GetPointAtInWorldUnit(currentWorldPos, out pos, out rot);


                bool laneValid = true;
                int testedLane = currentLane;
                while (Physics.CheckSphere(pos + ((testedLane - 1) * laneOffset * (rot * Vector3.right)), 0.4f, 1 << 9))
                {
                    testedLane = (testedLane + 1) % 3;
                    if (currentLane == testedLane)
                    {
                        // Couldn't find a valid lane.
                        laneValid = false;
                        break;
                    }
                }

                currentLane = testedLane;

                if (laneValid)
                {
                    pos = pos + ((currentLane - 1) * laneOffset * (rot * Vector3.right));


                    GameObject toUse = null;
                    if (Random.value < powerupChance)
                    {
                        int picked = Random.Range(0, consumableDatabase.consumbales.Length);

                        //if the powerup can't be spawned, we don't reset the time since powerup to continue to have a high chance of picking one next track segment
                        if (consumableDatabase.consumbales[picked].canBeSpawned)
                        {
                            // Spawn a powerup instead.
                            m_TimeSincePowerup = 0.0f;
                            powerupChance = 0.0f;

                            AsyncOperationHandle op = Addressables.InstantiateAsync(consumableDatabase.consumbales[picked].gameObject.name, pos, rot);
                            yield return op;
                            if (op.Result == null || !(op.Result is GameObject))
                            {
                                Debug.LogWarning(string.Format("Unable to load consumable {0}.", consumableDatabase.consumbales[picked].gameObject.name));
                                yield break;
                            }
                            toUse = op.Result as GameObject;
                            toUse.transform.SetParent(segment.transform, true);
                        }
                    }
                    else if (Random.value < premiumChance)
                    {
                        m_TimeSinceLastPremium = 0.0f;
                        premiumChance = 0.0f;

                        AsyncOperationHandle op = Addressables.InstantiateAsync(currentTheme.premiumCollectible.name, pos, rot);
                        yield return op;
                        if (op.Result == null || !(op.Result is GameObject))
                        {
                            Debug.LogWarning(string.Format("Unable to load collectable {0}.", currentTheme.premiumCollectible.name));
                            yield break;
                        }
                        toUse = op.Result as GameObject;
                        toUse.transform.SetParent(segment.transform, true);
                    }
                    else
                    {
                        toUse = Coin.coinPool.Get(pos, rot);
                        toUse.transform.SetParent(segment.collectibleTransform, true);
                    }

                    if (toUse != null)
                    {
                        //TODO : remove that hack related to #issue7
                        Vector3 oldPos = toUse.transform.position;
                        toUse.transform.position += Vector3.back;
                        toUse.transform.position = oldPos;
                    }
                }

                currentWorldPos += increment;
            }
        }
    }

    public void AddScore(int amount)
    {
        int finalAmount = amount;
        m_Score += finalAmount * m_Multiplier;
    }
}