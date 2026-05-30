
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class TeleportToFishingZone : UdonSharpBehaviour
{
    public override void Interact()
    {
        // プレイヤーAPIを取得
        VRCPlayerApi player = Networking.LocalPlayer;

        if (player == null)
        {
            Debug.LogError("プレイヤーAPIを取得できませんでした。");
            return;
        }

        // テレポート先の座標と向きを設定
        Vector3 teleportPosition = new Vector3(38.87f, 42.593f, 22.35f); // ここにテレポート先の座標を設定
        Quaternion teleportRotation = Quaternion.Euler(0, 90, 0); // ここにテレポート先の向きを設定

        // テレポートする
        player.TeleportTo(teleportPosition, teleportRotation);
    }
}
