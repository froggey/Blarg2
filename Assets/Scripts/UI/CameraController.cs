using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour {
        public Transform cameraTransform;
        public AnimationCurve angleCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
        public int maxZoom = 15;
        public int minZoom = 200;

        bool isScrolling;
        Vector3 scrollOrigin;

        void Update() {
                int scrollMode = PlayerPrefs.GetInt("Scroll Mode");

                if(Input.GetButtonDown("Scroll")) {
                        isScrolling = true;
                        scrollOrigin = Input.mousePosition;
                } else if(!Input.GetButton("Scroll")) {
                        isScrolling = false;
                }

                transform.Translate(Input.GetAxis("Horizontal") * 2, 0, Input.GetAxis("Vertical") * 2);
                transform.localRotation *= Quaternion.AngleAxis(Input.GetAxis("Rotation"), Vector3.up);
                if(scrollMode == 0) {
                        if(isScrolling) {
                                Vector3 pos = Camera.main.ScreenToViewportPoint(Input.mousePosition - scrollOrigin);
                                transform.Translate(pos.x * 8, 0, pos.y * 8);
                        }
                } else {
                        if(Input.GetButton("Scroll")) {
                                transform.Translate(Input.GetAxis("Mouse X") * 2, 0, Input.GetAxis("Mouse Y") * 2);
                        }
                }

                cameraTransform.localRotation = Quaternion.AngleAxis(45, new Vector3(1,0,0));
                cameraTransform.Translate(0, 0, Input.GetAxis("Zoom") * 50);
                if(cameraTransform.localPosition.y < maxZoom) {
                        cameraTransform.localPosition = new Vector3(0, maxZoom, -maxZoom);
                } else if(cameraTransform.localPosition.y > minZoom) {
                        cameraTransform.localPosition = new Vector3(0, minZoom, -minZoom);
                }

                cameraTransform.localRotation = Quaternion.AngleAxis(angleCurve.Evaluate((cameraTransform.localPosition.y - maxZoom) / minZoom) * 90, new Vector3(1,0,0));
        }

        public void LookAt(DVector2 point) {
                transform.position = new Vector3((float)point.x, 0, (float)point.y);
        }
}
