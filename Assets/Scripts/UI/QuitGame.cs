using UnityEngine;

public class QuitGame : MonoBehaviour {
        void Update() {
                if(Input.GetButtonUp("Quit")) {
                        Application.Quit();
                }
        }
}
