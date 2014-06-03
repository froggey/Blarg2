using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class ZMachineBarrel : MonoBehaviour {
        void Fire() {
                audio.PlayOneShot(audio.clip);
        }
}
