
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FX_WaterRipple : UdonSharpBehaviour
{
    // 1. 定数

    // 2. Inspectorで設定するフィールド
    [Header("水面のエフェクト")]
    [SerializeField] private ParticleSystem waterRingEffect;
    [SerializeField] private LodAudioManager lodAudioManager;

    // 3. 状態管理・内部処理用
    private Vector3 point0;// ベジェ曲線の制御点
    private Vector3 point1;
    private Vector3 point2;

    private float duration;// 移動にかかる時間（秒）
    private float elapsed = 0f;// 経過時間
    private bool isMoving = false;

    private void Start()
    {
        if (waterRingEffect == null)
        {
            Debug.LogError("FX_WaterRipple: パーティクルシステムが設定されていません。");
        }
        StopMovement();// 初期状態では移動・再生を停止
    }

    // Update() で t=0～1 の間で位置を更新
    private void Update()
    {
        if (isMoving)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // p0, p1, p2から二次ベジェ曲線上の位置を計算
            Vector3 newPos = CalculateQuadraticBezier(point0, point1, point2, t);
            transform.position = newPos;
            if (t >= 1.0f)
            {
                isMoving = false;
            }
        }
    }

    /// <summary>
    /// ベジェ曲線の制御点と移動時間をセットする。
    /// 実際の移動に関しては StartMovement() を呼び出す必要がある。
    /// </summary>
    public void SetControlPoints(Vector3 startPoint, Vector3 midPoint, Vector3 endPoint, float newDuration)
    {
        point0 = startPoint;
        point1 = midPoint;
        point2 = endPoint;
        duration = newDuration;
    }

    /// <summary>
    /// SetControlPoints() でセットした制御点を使って移動を開始する。
    /// </summary>
    public void StartMovement()
    {
        // 水面のエフェクトを再生
        if (!waterRingEffect.isPlaying)
        {
            waterRingEffect.Play();
        }

        lodAudioManager.PlayRipple();

        elapsed = 0f;
        isMoving = true;
    }

    public void StopMovement()
    {
        ResetState();

        if (waterRingEffect != null)
        {
            waterRingEffect.Stop();
        }
        lodAudioManager.StopRipple();
    }

    private void ResetState()
    {
        isMoving = false;
        elapsed = 0f;
    }

    // 二次ベジェ曲線を計算する関数
    private Vector3 CalculateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        // x,z 座標のみ計算。y座標はの動きは考慮しない
        float x = u * u * p0.x + 2f * u * t * p1.x + t * t * p2.x;
        float z = u * u * p0.z + 2f * u * t * p1.z + t * t * p2.z;
        float y = p0.y;
        return new Vector3(x, y, z);
    }
}
