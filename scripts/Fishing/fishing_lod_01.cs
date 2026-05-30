using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Core;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class fishing_lod_01 : UdonSharpBehaviour
{
    // 1. 定数
    private const float MIN_TIME_TO_FISH_APPEAR = 5f;
    private const float MAX_TIME_TO_FISH_APPEAR = 12f;
    private const float MIN_TIME_TO_FISH_APPROACH_ROD = 3f;
    private const float MAX_TIME_TO_FISH_APPROACH_ROD = 6f;
    private const float MAX_POWER_CYCLE_DURATION = 2.0f;
    private const float MAX_CAST_DISTANCE = 10f;
    private const float WATER_SURFACE_Y = 41.0f;
    private const float ROD_RESET_TIMEOUT = 30f; // ドロップされてから何秒経った場合にロッドをリセットするか

    // 2. Inspectorで設定するフィールド
    [SerializeField] private GameObject fishingLodFloat;
    [SerializeField] private GameObject bendBone; // 釣り竿を曲げるためのボーン
    [SerializeField] private GameObject tipBone; // 釣り竿の先端のボーン
    [SerializeField] private FishingLodFloat fishingLodFloatScript;
    [SerializeField] private Slider castPowerSlider;
    [SerializeField] private ParticleSystem waterRingEffect;
    [SerializeField] private FX_WaterRipple fX_WaterRipple;
    [SerializeField] private LineRenderer fishingLineRenderer;
    [SerializeField] private UdonBehaviour fishingManagerUdon;
    [SerializeField] private float floatMoveDuration = 2f; // ウキの移動にかかる時間
    [SerializeField] private LodAudioManager lodAudioManager;

    [SerializeField] private Vector3 fishingZoneSize; // 釣り竿が有効な釣りゾーンのサイズ
    [SerializeField] private Vector3 fishingZoneCenter;

    [SerializeField] private VRCPickup pickup;

    // 3. 状態管理・内部処理用
    private Vector3 initialRodPosition;
    private Quaternion initialRodRotation;
    private Vector3 fishingLineEntryPoint; // 釣り糸が実際に水面に入った位置

    private float clickDownTime = 0f; // クリックが押された開始時間
    private float clickHoldDuration = 0f; // クリックが押されている間の時間
    private float castPower = 0f;

    private bool hasCastRod = false;
    private bool canCastRod = true;
    private float elapsedTimeSinceCast = 0f;
    private float timeToFishAppear = 0f;
    private float timeToFishApproachRod = 0f;
    private bool resetCanCastRodOnClickReleased = false; // クリックが離されたときにcanCastRodをtrueに戻すためのフラグ
    private bool isChangingCastPower = false; // 釣りパワーが変更中かどうか

    private float timeSinceDrop = 0f;
    private bool isRodDropped = false;

    private Bounds fishingAreaBounds;

    private void Start()
    {
        initialRodPosition = transform.position;
        initialRodRotation = transform.rotation;
        fishingAreaBounds = new Bounds(fishingZoneCenter, fishingZoneSize);
    }

    private void Update()
    {
        if (!Networking.IsOwner(gameObject)) return;

        // 釣り竿が釣りゾーン外に出ている場合は釣り竿をリセットする
        if (IsRodOutOfFishingZone())
        {
            CancelFishing();
            ResetLodPosition();
            return;
        }

        CheckRodDropTimeout();

        // 一度ロッドが投げられたら
        if (hasCastRod)
        {
            elapsedTimeSinceCast += Time.deltaTime;
            DrawFishingLine(); // 釣り糸を描画する

            // 魚が竿に近づき終わってから1秒経過したとき
            if (elapsedTimeSinceCast >= timeToFishAppear + timeToFishApproachRod + 1f)
            {
                fX_WaterRipple.StartMovement();
                InitializeRodState();
            }
            // 魚が現れる時間になっている、かつ現在のパーティクルが再生中でないとき
            else if (elapsedTimeSinceCast >= timeToFishAppear)
            {
                // 水面のエフェクトが再生中でないとき
                if (!waterRingEffect.isPlaying)
                {
                    fX_WaterRipple.StartMovement();
                }
            }
        }
        // ロッドが投げられていないとき
        else
        {
            if (isChangingCastPower)
            {
                UpdateCastPowerSlider();
            }

            EndFishingLineDrawing(); // 釣り糸の描画を終了する
        }
    }

    // ロッドがインタラクトされたとき
    public override void Interact()
    {
        InitializeRodState();
        isRodDropped = false;
    }

    // ロッドが離されたとき
    public override void OnDrop()
    {
        InitializeRodState();
        isRodDropped = true;
    }

    // 長押しでもなんでもとりあえずuseされたとき
    public override void OnPickupUseDown()
    {
        Debug.Log("OnPickupUseDownが呼ばれました");
        float fullTimeToFish = timeToFishAppear + timeToFishApproachRod;// 魚が出現してから竿に近づくまでの合計時間

        // 竿をスロー可能でかつクリックが押されたとき
        if (canCastRod)
        {
            // クリックが押された時間を記録する
            StartCastTimer();
            return;
        }

        // ロッドを投げてから指定秒経過した状態でUseされたときには魚を釣る
        // >=にすると初期化後にクリックするとすぐに釣り上げられてしまうので注意
        if (elapsedTimeSinceCast > fullTimeToFish)
        {
            CatchFish();
            return;
        }

        // 指定時間前にクリックした場合には釣りをキャンセルする
        if (hasCastRod && elapsedTimeSinceCast < fullTimeToFish)
        {
            CancelFishing();
            return;
        }
    }

    // クリックが離されたとき（長押し・単押し）
    public override void OnPickupUseUp()
    {
        Debug.Log("OnPickupUseUpが呼ばれました");

        // まだロッドを投げていない状態でUseされたときには竿を投げる
        if (!hasCastRod && canCastRod)
        {
            CastRod();
            return;
        }

        // クリックが離されたときにcanCastRodをtrueに戻す
        if (resetCanCastRodOnClickReleased)
        {
            canCastRod = true;
            resetCanCastRodOnClickReleased = false; // フラグをリセット
        }
    }

    // 釣り竿を投げる処理
    private void CastRod()
    {
        hasCastRod = true;
        canCastRod = false; // 投げた直後は再投げ禁止
        castPowerSlider.value = 0f;
        fishingLodFloatScript.Show();
        fishingLodFloatScript.SetRodCast(true);
        bendBone.transform.localRotation = Quaternion.Euler(30f, 0f, 0f); // ボーンを曲げる

        clickHoldDuration = Time.time - clickDownTime; // クリックが押されていた時間を計算
        Vector3 rodForward = transform.forward;// 竿の前ベクトルを取得

        // クリックが押されていた時間に応じて釣り糸のエントリーポイントを計算
        castPower = (clickHoldDuration % MAX_POWER_CYCLE_DURATION) / MAX_POWER_CYCLE_DURATION; //0~1に正規化したウキを投げる力を計算
        fishingLineEntryPoint = transform.position + (MAX_CAST_DISTANCE * castPower * rodForward);
        fishingLineEntryPoint.y = WATER_SURFACE_Y;

        timeToFishAppear = Random.Range(MIN_TIME_TO_FISH_APPEAR, MAX_TIME_TO_FISH_APPEAR); // 3~5秒の間でランダムに時間を設定
        timeToFishApproachRod = Random.Range(MIN_TIME_TO_FISH_APPROACH_ROD, MAX_TIME_TO_FISH_APPROACH_ROD); // 5~8秒の間でランダムに時間を設定
        //timeToFishAppear = 2f; // デバッグ用に固定値
        //timeToFishApproachRod = 2f; // デバッグ用に固定値

        // 水面のエフェクトの座標を設定する（まだアニメーションは再生しない）
        Vector3 animationStartPoint = GetRandomizedXZ(fishingLineEntryPoint, 6f);
        Vector3 animationMidPoint = GetRandomizedXZ(fishingLineEntryPoint, 6f);
        Vector3 animationEndPoint = fishingLineEntryPoint;
        fX_WaterRipple.SetControlPoints(animationStartPoint, animationMidPoint, animationEndPoint, timeToFishApproachRod);

        // ウキをクリックした時間分だけ前に投げる
        fishingLodFloatScript.StartMovement(
            transform.position, // 初期位置
            (transform.position + fishingLineEntryPoint) / 2f + Vector3.up * 1.5f, // 中間位置は少し上に
            fishingLineEntryPoint, // 最終的な位置
            floatMoveDuration // ウキの移動にかかる時間を設定
        );

        Debug.Log("OnPickupUseUp: クリックが押されていた時間: " + clickHoldDuration);
        Debug.Log("ウキを投げる強さ: " + castPower);
        Debug.Log("竿の前ベクトル: " + rodForward);
        Debug.Log("魚が現れるまでの時間：" + timeToFishAppear + "　魚が竿に近づくまでの時間：" + timeToFishApproachRod);
    }

    // 魚を釣る
    private void CatchFish()
    {
        InitializeRodState();
        canCastRod = false; // 釣り上げた直後は投げ禁止
        resetCanCastRodOnClickReleased = true; // クリックが離されたときにcanCastRodをtrueに戻すためのフラグ
                                               // これをしないと釣った直後に勝手にもう一度竿が投げられてしまう

        lodAudioManager.PlayCatch(); // 釣り上げ音

        string playerName = Networking.LocalPlayer.displayName;
        fishingManagerUdon.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "FishCaught", transform.position, fishingLineEntryPoint, playerName);
    }

    // 釣りをキャンセルする
    private void CancelFishing()
    {
        InitializeRodState();
        canCastRod = false; // 釣りがキャンセルされた直後は投げ禁止
        resetCanCastRodOnClickReleased = true; // 同フレームで再度竿が投げられないようにする
        Debug.Log("釣りがキャンセルされました。");
    }

    public void ResetFishingByFloatMiss()
    {
        InitializeRodState();
        canCastRod = true;
        Debug.Log("ウキが水面から外れたため、釣りをリセットします。");
    }

    // 釣り竿が一定時間インタラクトされていない状態かチェックし、
    // 一定時間経過していたら釣り竿をリセットする
    private void CheckRodDropTimeout()
    {
        if (isRodDropped)
        {
            timeSinceDrop += Time.deltaTime;
            if (timeSinceDrop >= ROD_RESET_TIMEOUT)
            {
                ResetLodPosition();
                InitializeRodState();
            }
        }
        else
        {
            timeSinceDrop = 0f; // リセット
        }
    }

    // クリックされている間の時間を測るタイマーを開始する
    private void StartCastTimer()
    {
        // クリックが押された時間を記録する
        clickDownTime = Time.time;
        clickHoldDuration = 0f; // 初期化
        isChangingCastPower = true;
    }

    // スライダーの値を更新する
    private void UpdateCastPowerSlider()
    {
        // クリックが押されている間の時間を更新
        float duration = Time.time - clickDownTime;

        // スライダーの値を更新
        float sliderValue = (duration % MAX_POWER_CYCLE_DURATION) / MAX_POWER_CYCLE_DURATION;
        castPowerSlider.value = sliderValue;
    }

    // 釣り竿の先端から水面に向かって釣り糸を描画する
    private void DrawFishingLine()
    {
        fishingLineRenderer.enabled = true; // 釣り糸の描画を有効にする
        fishingLineRenderer.positionCount = 2;
        fishingLineRenderer.SetPosition(0, tipBone.transform.position);
        fishingLineRenderer.SetPosition(1, fishingLodFloatScript.transform.position);
    }

    // 釣り糸の描画を終了する
    private void EndFishingLineDrawing()
    {
        fishingLineRenderer.enabled = false;
        fishingLineRenderer.positionCount = 0; // 描画する頂点数を0にする
    }

    // 釣り竿が有効な釣りゾーンの外に出ているかどうかを判定する
    private bool IsRodOutOfFishingZone()
    {
        return !fishingAreaBounds.Contains(transform.position);
    }

    /// <summary>
    /// ロッドに関する変数の初期化、ウキの非表示、水面エフェクトの停止、スライダーの初期化を行います。
    /// </summary>
    private void InitializeRodState()
    {
        // 変数を初期化する
        hasCastRod = false;
        canCastRod = true;
        resetCanCastRodOnClickReleased = false;
        isChangingCastPower = false;
        timeToFishAppear = 0f;
        timeToFishApproachRod = 0f;
        elapsedTimeSinceCast = 0f;
        clickDownTime = 0f;
        clickHoldDuration = 0f;
        timeSinceDrop = 0f;
        isRodDropped = false;

        bendBone.transform.localRotation = Quaternion.Euler(0f, 0f, 0f); // ボーンの回転をリセット
        castPowerSlider.value = 0f; // スライダーの値をリセット

        fishingLodFloatScript.ResetState();
        fX_WaterRipple.StopMovement();// 水面エフェクトを停止する
    }

    /// <summary>
    /// 釣り竿の強制ドロップを実行し、釣り竿を初期位置にリセットします。
    /// </summary>
    private void ResetLodPosition()
    {
        pickup.Drop();
        transform.position = initialRodPosition;
        transform.rotation = initialRodRotation;
    }

    /// <summary>
    /// 与えられたVector3のXZ座標を少し変化させたVector3を返します。
    /// </summary>
    private Vector3 GetRandomizedXZ(Vector3 basePosition, float range)
    {
        // XZ座標をランダムに変化させる
        float randomX = Random.Range(1.5f, range) * (UnityEngine.Random.value > 0.5f ? 1 : -1);
        float randomZ = Random.Range(1.5f, range) * (UnityEngine.Random.value > 0.5f ? 1 : -1);
        return new Vector3(basePosition.x + randomX, basePosition.y, basePosition.z + randomZ);
    }
}
