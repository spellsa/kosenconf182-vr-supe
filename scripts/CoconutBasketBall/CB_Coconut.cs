
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class CB_Coconut : UdonSharpBehaviour
{
    private const float RESET_AFTER_SECONDS = 5.0f; // 最後にリリースされてから、この秒数が経過したあとに位置をリセットする
    private const float RESET_DISTANCE = 10.0f;
    private const float BASKET_RADIUS = 1.0f; // カゴの半径

    private bool isInteracted = false;

    private float basketExitTime = 0;
    private bool isCountingOutOfBasketDuration = false;

    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform coconutRespawnPoint;
    [SerializeField] private Transform resetDistanceReferencePoint;// リセット距離の計算の基準点
    [SerializeField] private VRCPickup pickup;

    void FixedUpdate()
    {
        if (!Networking.IsOwner(gameObject)) return;

        CheckCoconutPosition();
    }

    // インタラクトしたとき
    public override void Interact()
    {
        // オーナー権限を取得
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        isInteracted = true;
        isCountingOutOfBasketDuration = false;
        basketExitTime = 0;
    }

    // 離したとき
    public override void OnDrop()
    {
        isInteracted = false;
    }

    // ココナッツの位置をチェックする（オーナーが実行する）
    public void CheckCoconutPosition()
    {
        CheckOutOfBasket();// インタラクトなしでカゴの外に出たかチェック
        CheckResetDistance();// リセット距離を超えたかチェック
        HandleTimeoutReset();// タイムアウトによるリセットを処理
    }

    private void CheckOutOfBasket()
    {
        if (isCountingOutOfBasketDuration || isInteracted) return;// すでにカウンターのフラグが立っている、インタラクトされている場合は何もしない

        float distance = CalculateDistanceFromVector3(transform.position, coconutRespawnPoint.position);
        if (distance > BASKET_RADIUS)
        {
            basketExitTime = Time.time;
            isCountingOutOfBasketDuration = true;
        }
    }

    private void CheckResetDistance()
    {
        if (isInteracted) return;

        float distance = CalculateDistanceFromVector3(transform.position, resetDistanceReferencePoint.position);
        if (distance > RESET_DISTANCE)
        {
            ResetCoconut();
        }
    }

    private void HandleTimeoutReset()
    {
        if (!isCountingOutOfBasketDuration) return;

        if (Time.time - basketExitTime > RESET_AFTER_SECONDS)
        {
            ResetCoconut();
        }
    }

    // ココナッツをリセットする
    private void ResetCoconut()
    {
        // オーナー権限を取得
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        pickup.Drop();// 持っていたら離す
        basketExitTime = 0;
        isCountingOutOfBasketDuration = false;

        // 位置、回転、速度をリセット
        transform.position = coconutRespawnPoint.position;
        transform.rotation = coconutRespawnPoint.rotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // 2点間の距離を計算する（y軸を無視）
    private float CalculateDistanceFromVector3(Vector3 a, Vector3 b)
    {
        Vector2 a2D = new Vector2(a.x, a.z);
        Vector2 b2D = new Vector2(b.x, b.z);
        return Vector2.Distance(a2D, b2D);
    }
}
