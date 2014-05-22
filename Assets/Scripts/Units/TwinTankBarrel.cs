using UnityEngine;
using System.Collections;

[RequireComponent (typeof(AudioSource))]
[RequireComponent (typeof(Animator))]
public class TwinTankBarrel : MonoBehaviour {
        private Animator animator;

        void Start() {
                animator = GetComponent<Animator>();
        }

        void Fire() {
                audio.PlayOneShot(audio.clip);
                animator.SetTrigger("Fire");
        }
}
