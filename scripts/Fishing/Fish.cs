
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class Fish : UdonSharpBehaviour
{
    // 定数
    private const float HEAD_BONE_OFFSET = 1f;
    private const float MOVE_DURATION_TO_ROD = 0.8f; // オブジェクトの移動にかかる時間

    // privateフィールド
    private bool isFollowingPlayer = false;
    private VRCPlayerApi player;

    // 釣り竿の位置にオブジェクトを徐々に近づけるためのパラメーター
    private float moveElapsedToRod = 0f;
    private bool isMovingToRod = false;
    private Vector3 startPointToRod, midPointToRod, endPointToRod;

    [HideInInspector] public int fishID; // 0から始まる一意なID（fishingManagerで初期化するためpublic）

    void Start()
    {
        SendCustomEventDelayedSeconds(nameof(CheckAndMoveRoutine), 0.1f);
    }

    void FixedUpdate()
    {
        // オブジェクトが移動中の場合、ベジェ曲線に沿ってオブジェクトを移動させる
        if (isMovingToRod)
        {
            moveElapsedToRod += Time.deltaTime;
            float t = Mathf.Clamp01(moveElapsedToRod / MOVE_DURATION_TO_ROD); // Math.fを使用して0.0fから1.0fの範囲に制限

            // ベジェ曲線に沿ってオブジェクトを移動させる
            Vector3 newPosition = CalculateQuadraticBezier(startPointToRod, midPointToRod, endPointToRod, t);
            transform.position = newPosition;

            // 移動が完了したらフラグを下ろす
            if (t >= 1.0f)
            {
                isMovingToRod = false;
                moveElapsedToRod = 0f;
                SendCustomEventDelayedSeconds(nameof(StartFollowingPlayer), 5f);
                Debug.Log("Fish: オブジェクトの移動が完了しました。");
            }
        }
    }

    void OnDisable()
    {
        ResetState();
        Debug.Log("Fish: オブジェクトが非アクティブになりました。");
    }

    // プレイヤーの位置を定期的にチェックして、必要に応じて移動するルーチン（SendCustomEventを使用するためにpublic）
    public void CheckAndMoveRoutine()
    {
        // ここではオーナー以外を弾く処理を入れているが、本来はこれによって非オーナー以外がこのループをこれ以上スケジュールすることもできなくなり、
        // オーナーが退出した場合に、処理が止まってしまうように見えるが、VRCObkectPoolの仕様により、
        // 新しくオブジェクトが釣られた場合（再アクティブ化）に、再度Startが呼ばれ、このルーチンが再開されるため問題はない
        if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) return;
        if (!isFollowingPlayer || player == null) return;

        Vector3 headbonePos = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        Vector3 targetPosition = headbonePos + new Vector3(0, HEAD_BONE_OFFSET, 0);

        transform.position = targetPosition;

        SendCustomEventDelayedSeconds(nameof(CheckAndMoveRoutine), 0.1f);
    }

    /// <summary>
    /// 釣り竿の位置にオブジェクトを徐々に近づけるのを開始する
    /// </summary>
    [NetworkCallable]
    public void StartMovingToPos(Vector3 startPoint, Vector3 endPoint, int callerID)
    {
        // 釣ったプレイヤーのIDが自分と一致しない場合は終了
        // オーナー譲渡が間に合わないことによる判定のミスを防ぐためにプレイヤーIDでチェックする
        if (callerID != Networking.LocalPlayer.playerId) return;

        player = Networking.LocalPlayer;

        // 移動開始時にパラメーターをセット
        startPointToRod = startPoint; // 現在オブジェクトの位置を開始点に設定
        endPointToRod = endPoint; // 釣り竿の位置を終了点に設定
        midPointToRod = (startPointToRod + endPointToRod) / 2 + new Vector3(0, 1.5f, 0); // 中間点を設定（高さを上げる）
        moveElapsedToRod = 0f; // 経過時間をリセット
        isMovingToRod = true; // 移動中フラグを立てる
    }

    // プレイヤーへの追従を開始する
    //（SendCustomEventを使用するためにpublic、外部から呼び出されない）
    public void StartFollowingPlayer()
    {
        isFollowingPlayer = true;
        SendCustomEventDelayedSeconds(nameof(CheckAndMoveRoutine), 0f);
        Debug.Log("Fish: プレイヤーへの追従を開始しました。");
    }

    // 位置や変数などを初期化する
    private void ResetState()
    {
        player = null;
        isFollowingPlayer = false;
        isMovingToRod = false;
        moveElapsedToRod = 0f;
        startPointToRod = Vector3.zero;
        midPointToRod = Vector3.zero;
        endPointToRod = Vector3.zero;
    }

    /// <summary>
    /// 二次ベジエ曲線を計算します。
    /// </summary>
    private Vector3 CalculateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }
}
