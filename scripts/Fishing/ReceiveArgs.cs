
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/*
    SendCustomNetworkEventでFishing_lodから呼び出される関数
    FishingManagerのオーナーを変更することなく値を転送することを目的としています
    SendCustomNetworkEventが正式実装されたら消します
    まじでSendCustomNetworkEventしばく
*/

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ReceiveArgs : UdonSharpBehaviour
{
    [UdonSynced] public string argsString;

    [SerializeField] private GameObject fishingManager;
    private UdonBehaviour fishingManagerUdon;

    private void Start()
    {
        fishingManagerUdon = (UdonBehaviour)fishingManager.GetComponent(typeof(UdonBehaviour));
    }

    // 誰からでも呼び出される可能性のある関数
    public void SyncCatchInfo(string args)
    {
        Debug.Log("SyncCatchInfo: " + args);
        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

        argsString = args;
        RequestSerialization();
        fishingManagerUdon.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "Set_isValidRequest_receiveArgs_True");

        // プレイヤーが自分だけの場合は即座に呼び出す
        if (VRCPlayerApi.GetPlayerCount() == 1)
        {
            Debug.Log("インスタンスの総プレイヤー数が1のため、即座にFishCaughtを呼び出します");
            fishingManagerUdon.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "FishCaught");
        }
    }

    // ここはオーナー以外なら誰でも呼び出される
    public override void OnDeserialization()
    {
        fishingManagerUdon.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "FishCaught");
    }
}
