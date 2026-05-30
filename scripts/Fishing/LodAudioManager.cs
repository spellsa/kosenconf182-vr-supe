using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LodAudioManager : UdonSharpBehaviour
{
    [SerializeField] private AudioSource audioSource_NoLoop;
    [SerializeField] private AudioSource audioSource_Loop;
    [SerializeField] private AudioClip[] lodClips;

    void Start()
    {
        // audioSource_Loop.Play();
    }

    public void PlaySplash()
    {
        if (audioSource_NoLoop != null && lodClips.Length > 0)
        {
            audioSource_NoLoop.clip = lodClips[0]; // 0番目のクリップを再生
            audioSource_NoLoop.Play();
        }
    }
    public void PlayRipple()
    {
        if (audioSource_Loop != null)
        {
            audioSource_Loop.Play();
        }
    }
    public void StopRipple()
    {
        if (audioSource_Loop != null)
        {
            audioSource_Loop.Stop();
        }
    }
    public void PlayCatch()
    {
        if (audioSource_NoLoop != null && lodClips.Length > 1)
        {
            audioSource_NoLoop.clip = lodClips[1]; // 2番目のクリップを再生
            audioSource_NoLoop.Play();
        }
    }
}
