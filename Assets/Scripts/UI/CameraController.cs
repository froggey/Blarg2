using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour {
        public Transform cameraTransform;

        void Update() {
                transform.Translate(Input.GetAxis("Horizontal") * 2, 0, Input.GetAxis("Vertical") * 2);
                transform.localRotation *= Quaternion.AngleAxis(Input.GetAxis("Rotation"), Vector3.up);

                cameraTransform.Translate(0, 0, Input.GetAxis("Mouse ScrollWheel") * 50);
                if(cameraTransform.localPosition.y < 15) {
                        cameraTransform.localPosition = new Vector3(0, 15, -15);
                } else if(cameraTransform.localPosition.y > 200) {
                        cameraTransform.localPosition = new Vector3(0, 200, -200);
                }
        }
}
