
using System.Runtime.CompilerServices;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CB_JumpBoard : UdonSharpBehaviour
{
    private float previousImpulse = 0;
    [SerializeField] private float jumpImpulse;
    [SerializeField] private float BoardRadius;

    // プレイヤーがコライダーに入ったとき
    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player != Networking.LocalPlayer) return;

        previousImpulse = player.GetJumpImpulse();
        player.SetJumpImpulse(jumpImpulse);
        SendCustomEventDelayedSeconds(nameof(UpdateColliderCheck), 0.2f);
    }

    // プレイヤーがコライダーから出たとき
    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player != Networking.LocalPlayer) return;
        player.SetJumpImpulse(previousImpulse);
    }

    // OnPlayerTriggerExitが呼ばれない場合の保険
    // 一定時間ごとにプレイヤーがコライダー内にいるかチェックし、コライダー外に出ていたらジャンプ力を元に戻す
    public void UpdateColliderCheck()
    {
        // プレイヤーがコライダー内にいる場合
        if (Vector3.Distance(Networking.LocalPlayer.GetPosition(), transform.position) < BoardRadius)
        {
            SendCustomEventDelayedSeconds(nameof(UpdateColliderCheck), 0.2f);
        }
        // プレイヤーがコライダー外に出た場合
        else
        {
            Networking.LocalPlayer.SetJumpImpulse(previousImpulse);// ジャンプ力を元に戻す
        }
    }
}
