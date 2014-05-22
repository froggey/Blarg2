using UnityEngine;

class NumericTest : MonoBehaviour {
        void Start() {
                for(DReal n = -DReal.TwoPI; n <= DReal.TwoPI; n += (DReal)1 / 10) {
                        print(n + "  D: " + DReal.Tan(n) + "  F: " + Mathf.Tan((float)n) + "  D: " + Mathf.Abs(Mathf.Tan((float)n) - (float)DReal.Tan(n)));
                }
        }
}
