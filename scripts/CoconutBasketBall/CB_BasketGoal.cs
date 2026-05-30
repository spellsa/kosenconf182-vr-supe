
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CB_BasketGoal : UdonSharpBehaviour
{
    [SerializeField] private ParticleSystem[] goalEffect;
    [SerializeField] private AudioSource goalSound;

    public void OnTriggerEnter(Collider other)
    {
        if (other.name.StartsWith("Coconut-2"))
        {
            Debug.Log("ココナッツがカゴに入りました！");
            PlayGoalEffectAndSound();
        }
    }

    private void PlayGoalEffectAndSound()
    {
        foreach (ParticleSystem effect in goalEffect)
        {
            effect.Play();
        }
        goalSound.Play();
    }
}
