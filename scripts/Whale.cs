
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Whale : UdonSharpBehaviour
{
    // ========================================
    // エリア設定
    // ========================================
    [Header("エリア設定")]
    [Tooltip("クジラが移動できる範囲のサイズ (X, Y, Z)")]
    [SerializeField] private Vector3 areaSize = new Vector3(125f, 50f, 125f);

    [Tooltip("クジラが移動できる範囲の中心座標")]
    [SerializeField] private Vector3 areaCenter;

    // ========================================
    // 移動設定
    // ========================================
    [Header("移動設定")]
    [Tooltip("クジラの前進速度 (m/秒)")]
    [SerializeField] private float moveSpeed = 5.0f;

    [Tooltip("通常時の最大旋回速度 (度/秒)。値が大きいほど素早く向きを変えます")]
    [SerializeField] private float turnSpeedNormal = 10.0f;

    [Tooltip("帰還時の最大旋回速度 (度/秒)。エリア外から戻るときの旋回速度です")]
    [SerializeField] private float turnSpeedReturning = 60.0f;

    // ========================================
    // 回転の滑らかさ設定
    // ========================================
    [Header("回転の滑らかさ")]
    [Tooltip("通常時の回転補間係数。値が大きいほど素早く目標回転に追従します")]
    [SerializeField] private float slerpFactorNormal = 3.0f;

    [Tooltip("帰還時の回転補間係数。値が大きいほど素早く目標回転に追従します")]
    [SerializeField] private float slerpFactorReturning = 5.0f;

    // ========================================
    // S字ウェーブ（体の揺れ）設定
    // ========================================
    [Header("S字ウェーブ（体の揺れ）")]
    [Tooltip("左右の揺れの周波数。値が大きいほど速く揺れます")]
    [SerializeField] private float horizontalWaveFrequency = 0.4f;

    [Tooltip("左右の揺れの振幅 (度)。値が大きいほど大きく揺れます")]
    [SerializeField] private float horizontalWaveAmplitude = 10.0f;

    [Tooltip("上下の揺れの周波数。値が大きいほど速く揺れます")]
    [SerializeField] private float verticalWaveFrequency = 0.3f;

    [Tooltip("上下の揺れの振幅 (度)。値が大きいほど大きく揺れます")]
    [SerializeField] private float verticalWaveAmplitude = 5.0f;

    [Tooltip("帰還時の揺れ倍率 (0〜1)。帰還中は揺れを抑えて方向転換に集中します")]
    [SerializeField] private float returnWaveScale = 0.2f;

    // ========================================
    // 呼吸（浮き沈み）設定
    // ========================================
    [Header("呼吸（浮き沈み）")]
    [Tooltip("呼吸の周期 (秒)。1回の浮き沈みにかかる時間です")]
    [SerializeField] private float breathInterval = 15.0f;

    [Tooltip("呼吸による上下移動の振幅 (m)。値が大きいほど大きく浮き沈みします")]
    [SerializeField] private float breathAmplitude = 4.0f;

    // ========================================
    // デバッグ設定
    // ========================================
    [Header("デバッグ")]
    [Tooltip("デバッグログを出力するかどうか")]
    [SerializeField] private bool enableDebugLog = false;

    // ========================================
    // 内部状態
    // ========================================
    private bool _isReturning = false;
    private Vector3 _returnTarget;
    private Bounds _bounds;

    // ========================================
    // 初期化
    // ========================================
    void Start()
    {
        InitializeBounds();
        InitializePosition();
    }

    private void InitializeBounds()
    {
        _bounds = new Bounds(areaCenter, areaSize);
    }

    private void InitializePosition()
    {
        if (!_bounds.Contains(transform.position))
        {
            transform.position = GetRandomPointInBounds();
            LookAtRandomPoint();
        }
    }

    private void LookAtRandomPoint()
    {
        Vector3 randomTarget = GetRandomPointInBounds();
        Vector3 lookDir = randomTarget - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(lookDir.normalized);
        }
    }

    // ========================================
    // メインループ
    // ========================================
    void Update()
    {
        float time = Time.time;
        Vector3 currentPos = transform.position;
        Vector3 currentForward = transform.forward;

        // エリア判定と帰還モードの更新
        UpdateBoundsCheck(currentPos);

        // 移動方向の決定
        Vector3 targetDir = CalculateTargetDirection(currentPos, currentForward, time);

        // 移動の適用
        ApplyMovement(currentPos, time);

        // 回転の適用
        ApplyRotation(targetDir, time);
    }

    // ========================================
    // エリア判定
    // ========================================
    private void UpdateBoundsCheck(Vector3 currentPos)
    {
        bool inBounds = _bounds.Contains(currentPos);

        if (enableDebugLog)
        {
            Debug.Log($"[Whale] inBounds: {inBounds} | Pos: {currentPos}");
        }

        if (!inBounds)
        {
            if (!_isReturning)
            {
                StartReturning();
            }
            else if (enableDebugLog)
            {
                Debug.Log($"[Whale] エリア外継続中... Target: {_returnTarget} | Distance: {Vector3.Distance(currentPos, _returnTarget):F2}m");
            }
        }
        else
        {
            TryStopReturning(currentPos);
        }
    }

    private void StartReturning()
    {
        _returnTarget = GetRandomPointInBounds();
        _isReturning = true;

        if (enableDebugLog)
            Debug.Log($"[Whale] エリア外検出! returnTarget設定: {_returnTarget}");
    }

    private void TryStopReturning(Vector3 currentPos)
    {
        _isReturning = false;

        if (enableDebugLog)
        {
            Debug.Log("[Whale] エリア内に帰還完了");
        }
    }

    // ========================================
    // 移動方向の計算
    // ========================================
    private Vector3 CalculateTargetDirection(Vector3 currentPos, Vector3 currentForward, float time)
    {
        if (_isReturning)
        {
            return CalculateReturningDirection(currentPos, currentForward);
        }
        else
        {
            return CalculateNormalDirection(currentForward, time);
        }
    }

    private Vector3 CalculateReturningDirection(Vector3 currentPos, Vector3 currentForward)
    {
        // XZ平面での回転計算
        Vector3 forwardXZ = new Vector3(currentForward.x, 0, currentForward.z).normalized;
        Vector3 toTargetXZ = new Vector3(_returnTarget.x - currentPos.x, 0, _returnTarget.z - currentPos.z).normalized;

        float angleToTarget = Vector3.SignedAngle(forwardXZ, toTargetXZ, Vector3.up);
        float maxRotationDeg = turnSpeedReturning * Time.deltaTime;
        float maxRotationRad = Mathf.Deg2Rad * maxRotationDeg;

        Vector3 newDirXZ = Vector3.RotateTowards(forwardXZ, toTargetXZ, maxRotationRad, 0f);

        if (enableDebugLog)
        {
            float actualRotation = Vector3.Angle(forwardXZ, newDirXZ);
            Debug.Log($"[Whale] 帰還モード | 目標角度: {Mathf.Abs(angleToTarget):F1}° | 最大回転: {maxRotationDeg:F2}° | 実回転: {actualRotation:F2}°");
        }

        return newDirXZ.normalized;
    }

    private Vector3 CalculateNormalDirection(Vector3 currentForward, float time)
    {
        // PerlinNoiseによる滑らかな揺らぎ
        float noiseY = (Mathf.PerlinNoise(time * 0.05f, 0) - 0.5f) * 2f;
        float noiseX = (Mathf.PerlinNoise(0, time * 0.05f) - 0.5f) * 1f;

        float maxRotationDeg = turnSpeedNormal * Time.deltaTime;
        Quaternion noiseTurn = Quaternion.Euler(
            noiseX * maxRotationDeg,
            noiseY * maxRotationDeg,
            0
        );

        return noiseTurn * currentForward;
    }

    // ========================================
    // 移動の適用
    // ========================================
    private void ApplyMovement(Vector3 currentPos, float time)
    {
        // 視覚向き（transform.forward）を移動方向として使用
        // これにより「見ている方向」=「進む方向」が一致する
        Vector3 visualForward = transform.forward;
        Vector3 horizontalDir = new Vector3(visualForward.x, 0, visualForward.z).normalized;
        Vector3 horizontalVelocity = horizontalDir * moveSpeed;

        // 呼吸による垂直移動
        float omega = 2 * Mathf.PI / breathInterval;
        float breathVelocity = breathAmplitude * omega * Mathf.Cos(time * omega);

        // 新しい位置を計算
        Vector3 newPos = currentPos;
        newPos += horizontalVelocity * Time.deltaTime;
        newPos.y += breathVelocity * Time.deltaTime;

        // Y座標をエリア内にクランプ
        newPos.y = Mathf.Clamp(newPos.y, _bounds.min.y, _bounds.max.y);

        transform.position = newPos;

        if (enableDebugLog)
        {
            Debug.Log($"[Whale] 移動 | 水平速度: {horizontalVelocity.magnitude:F2}m/s | 呼吸Y速度: {breathVelocity:F2}m/s");
        }
    }

    // ========================================
    // 回転の適用
    // ========================================
    private void ApplyRotation(Vector3 targetDir, float time)
    {
        Vector3 horizontalDir = new Vector3(targetDir.x, 0, targetDir.z).normalized;

        if (horizontalDir.sqrMagnitude < 0.001f) return;

        // 基本回転: 水平移動方向を向く
        Quaternion lookRot = Quaternion.LookRotation(horizontalDir);

        // S字ウェーブによる揺れ
        float waveScale = _isReturning ? returnWaveScale : 1.0f;
        float yawWave = Mathf.Sin(time * horizontalWaveFrequency) * horizontalWaveAmplitude * waveScale;
        float pitchWave = Mathf.Sin(time * verticalWaveFrequency) * verticalWaveAmplitude * waveScale;

        // 回転の合成
        Quaternion waveRot = Quaternion.Euler(pitchWave, yawWave, 0);
        Quaternion finalRot = lookRot * waveRot;

        // 滑らかに回転を補間
        float slerpFactor = _isReturning
            ? Time.deltaTime * slerpFactorReturning
            : Time.deltaTime * slerpFactorNormal;
        transform.rotation = Quaternion.Slerp(transform.rotation, finalRot, slerpFactor);

        if (enableDebugLog)
        {
            Debug.Log($"[Whale] 回転 | Yaw揺れ: {yawWave:F1}° | Pitch揺れ: {pitchWave:F1}° | 揺れ係数: {waveScale:F1}");
        }
    }

    // ========================================
    // ユーティリティ
    // ========================================
    private Vector3 GetRandomPointInBounds()
    {
        return new Vector3(
            Random.Range(_bounds.min.x, _bounds.max.x),
            Random.Range(_bounds.min.y, _bounds.max.y),
            Random.Range(_bounds.min.z, _bounds.max.z)
        );
    }
}
