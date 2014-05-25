using UnityEngine;
using System.Collections;

public class UnitSelection : MonoBehaviour {
        public bool isSelected;
        public Renderer highlight;

        public Color highlightColor = Color.red;
        private Color originalColor;

        void Start() {
                originalColor = highlight.material.color;
        }

        private void OnSelected() {
                isSelected = true;
                highlight.material.color = highlightColor;
        }

        private void OnUnselected() {
                isSelected = false;
                highlight.material.color = originalColor;
        }
}
