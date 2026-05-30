
using System.Security.Permissions;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3;
using VRC.SDK3.UdonNetworkCalling;
using System;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class test_WhiteCube : UdonSharpBehaviour
{
    [NetworkCallable]
    public void SayHello(String name)
    {
        Debug.Log("Hello, " + name + "! This is a test message from the test_WhiteCube script.");
    }
}
