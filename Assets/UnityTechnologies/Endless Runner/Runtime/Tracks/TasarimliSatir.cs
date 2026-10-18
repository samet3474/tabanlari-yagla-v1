using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Designed track piece: when a segment carries this component, TrackManager.SpawnObstacle places its obstacles in
/// fixed lanes and lays its coins in the given patterns instead of the random obstacle/coin generation. No Random call
/// is made for these. Segments without this component (Day/Night) keep the original random generation.
/// </summary>
public class TasarimliSatir : MonoBehaviour
{
    // Same spacing as the random coin line (TrackManager.SpawnCoinAndPowerup).
    public const float k_AltinAraligi = 1.5f;

    [System.Serializable]
    public class Satir
    {
        [Tooltip("Distance from the segment entry, in metres.")]
        public float konum;
        [Tooltip("Obstacle prefab (loaded by GUID, so Day/Night prefabs sharing an address cannot be mixed up).")]
        public AssetReference engel;
        [Tooltip("Lane mask: 1 left, 2 middle, 4 right. One instance per lane; all-lane barriers use the middle only (2).")]
        public int seritler;
    }

    public enum AltinTipi { Duz, Yay, Capraz }

    [System.Serializable]
    public class Altin
    {
        public AltinTipi tip;
        [Tooltip("Start/end distance from the segment entry, in metres.")]
        public float baslangic, bitis;
        [Tooltip("Lane at the start and at the end: -1 left, 0 middle, 1 right (equal unless Capraz).")]
        public int serit, bitisSeridi;
        [Tooltip("Duz/Capraz: height above the road. Yay: peak height of the arc (sine), 0 at both ends.")]
        public float yukseklik;
    }

    public Satir[] satirlar = new Satir[0];
    public Altin[] altinlar = new Altin[0];
}
