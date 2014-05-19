using UnityEngine;
using System.Collections;

public class UnitSelection : MonoBehaviour {
        public bool isSelected;
        public Renderer highlight;

        private void OnSelected() {
                isSelected = true;
                highlight.material.color = Color.red;
        }

        private void OnUnselected() {
                isSelected = false;
                highlight.material.color = Color.white;
        }
}
