using UnityEngine;
using System.Collections;

[RequireComponent (typeof(AudioSource))]
public class RailGunTurretBarrel : MonoBehaviour {
        void Fire() {
                audio.PlayOneShot(audio.clip);
        }
}