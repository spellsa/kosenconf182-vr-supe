using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FishingLodFloat : UdonSharpBehaviour
{
    // 1. 定数
    private const float FLOAT_MOVE_DURATION = 1f; // ウキの移動にかかる時間

    // 2. Inspectorで設定するフィールド
    [SerializeField] private fishing_lod_01 fishingLod01;
    [SerializeField] private LodAudioManager lodAudioManager;

    // 3. 状態を管理するフィールド
    private bool isRodCast = false;
    private bool isFloatMoving = false;
    private bool isTouchingWater = false;
    private Vector3 point0, point1, point2;
    private float floatMoveDuration, floatMoveElapsed;
    private Renderer floatRenderer; // ウキのレンダラー

    private void Start()
    {
        // ウキのレンダラーを取得
        floatRenderer = GetComponent<Renderer>();
        if (floatRenderer == null)
        {
            Debug.LogError("FishingLodFloat: Renderer not found on the object.");
        }

        Hide(); // 初期状態はウキを不可視にする
    }

    // Update() で t=0～1 の間で位置を更新
    private void Update()
    {
        // —— ウキをベジェ曲線で移動 ——//
        if (isFloatMoving)
        {
            floatMoveElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(floatMoveElapsed / floatMoveDuration);
            // 移動が終了したとき
            if (t >= 1f)
            {
                t = 1f;
                isFloatMoving = false;

                if (!isTouchingWater)
                {
                    fishingLod01.ResetFishingByFloatMiss();
                    return;
                }

                lodAudioManager.PlaySplash(); // 水面に落ちた音を再生
            }

            // p0, p1, p2から二次ベジェ曲線上の位置を計算
            transform.position = CalculateQuadraticBezier(point0, point1, point2, t);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("FishingLodFloat: OnTriggerEnter called with " + other.name);
        if (!isRodCast) return; // ロッドが投げられていない間は無視
        if (other.name == "FishingPond_WaterSurface")
        {
            Debug.Log("FishingLodFloat: 水面に触れました。");
            isTouchingWater = true;
        }
    }

    public void SetRodCast(bool value)
    {
        isRodCast = value;
    }

    // ウキの移動を開始するメソッド（ウキの移動時間の設定付き）
    public void StartMovement(Vector3 startPoint, Vector3 midPoint, Vector3 endPoint, float newDuration)
    {
        StartMovementInternal(startPoint, midPoint, endPoint, newDuration);
    }

    // ウキの移動を開始するメソッド（デフォルトの移動時間を使用）
    public void StartMovement(Vector3 startPoint, Vector3 midPoint, Vector3 endPoint)
    {
        StartMovementInternal(startPoint, midPoint, endPoint, FLOAT_MOVE_DURATION);
    }

    // ウキの位置を初期位置にリセットするメソッド
    public void ResetPosition(Vector3 pos)
    {
        // ウキの位置を初期位置にリセット
        transform.position = pos;
        isFloatMoving = false;
        floatMoveElapsed = 0f;
    }

    public void ResetState()
    {
        isRodCast = false;
        isFloatMoving = false;
        isTouchingWater = false;
        floatMoveElapsed = 0f;
        floatMoveDuration = FLOAT_MOVE_DURATION; // デフォルトの移動時間を設定
        Hide();

        transform.position = fishingLod01.transform.position; // 初期位置にリセット
    }

    // ウキを不可視にするメソッド
    public void Hide()
    {
        if (floatRenderer != null)
        {
            floatRenderer.enabled = false;
        }
        else
        {
            Debug.LogError("FishingLodFloat: Rendererがnullのため、不可視にできません。");
        }
    }

    // ウキを可視にするメソッド
    public void Show()
    {
        if (floatRenderer != null)
        {
            floatRenderer.enabled = true;
        }
        else
        {
            Debug.LogError("FishingLodFloat: Rendererがnullのため、可視にできません。");
        }
    }

    // 二次ベジェ曲線を計算する関数
    private Vector3 CalculateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }

    // 内部でウキの移動を開始するためのメソッド（重複コードを避けるため）
    private void StartMovementInternal(Vector3 startPoint, Vector3 midPoint, Vector3 endPoint, float duration)
    {
        // —— Bezier 曲線でウキを飛ばす初期化 ——
        point0 = startPoint;
        point1 = midPoint;
        point2 = endPoint;
        floatMoveDuration = duration;
        floatMoveElapsed = 0f;
        isFloatMoving = true;
    }
}
