
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class testCube : UdonSharpBehaviour
{
    [SerializeField]
    private UdonBehaviour targetBehaviour; // ここに呼び出したいUdonSharpBehaviourをアサイン

    public override void Interact()
    {
        targetBehaviour.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "SayHello", "superu");
    }
}
