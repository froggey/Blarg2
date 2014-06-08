using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Linq;

[CustomPropertyDrawer(typeof(EditableDReal))]
public class FixedPointEditor : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
                var s = property.FindPropertyRelative("stringValue");
                s.stringValue = EditorGUI.TextField(position, label, s.stringValue);
        }
}
