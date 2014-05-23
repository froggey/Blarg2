using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour {
        public Transform cameraTransform;
        public AnimationCurve angleCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
        public int maxZoom = 15;
        public int minZoom = 200;

        void Update() {
                transform.Translate(Input.GetAxis("Horizontal") * 2, 0, Input.GetAxis("Vertical") * 2);
                transform.localRotation *= Quaternion.AngleAxis(Input.GetAxis("Rotation"), Vector3.up);

                cameraTransform.localRotation = Quaternion.AngleAxis(45, new Vector3(1,0,0));
                cameraTransform.Translate(0, 0, Input.GetAxis("Mouse ScrollWheel") * 50);
                if(cameraTransform.localPosition.y < maxZoom) {
                        cameraTransform.localPosition = new Vector3(0, maxZoom, -maxZoom);
                } else if(cameraTransform.localPosition.y > minZoom) {
                        cameraTransform.localPosition = new Vector3(0, minZoom, -minZoom);
                }

                cameraTransform.localRotation = Quaternion.AngleAxis(angleCurve.Evaluate((cameraTransform.localPosition.y - maxZoom) / minZoom) * 90, new Vector3(1,0,0));
        }
}
