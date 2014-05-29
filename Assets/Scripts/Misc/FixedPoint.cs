// You are standing at the gate to Gehennom.  Unspeakable cruelty and harm lurk down there.

using UnityEngine;

// Fixed point numbers.

[System.Serializable]
public struct DReal {
        public static DReal PI = Create(205887); // 3.14... * 2**16. Change this if fixedShift changes.
        public static DReal HalfPI = Create(102943); // 1.57... * 2**16. Change this if fixedShift changes.
        public static DReal TwoPI = Create(411774); // 1.57... * 2**16. Change this if fixedShift changes.
        public static DReal MaxValue = Create(long.MaxValue);
        public static DReal MinValue = Create(long.MinValue);
        private static int fixedShift = 16;
        [SerializeField]
        private long value;

        //public DReal(float value) {
        //        double val_high_precision = value;
        //        val_high_precision *= 1 << fixedShift;
        //        this.value = (long)val_high_precision;
        //}

        private static DReal Create(long value) {
                DReal result = new DReal();
                result.value = value;
                return result;
        }

        public static explicit operator DReal(float value) {
                double val_high_precision = value;
                val_high_precision *= 1 << fixedShift;
                return Create((long)val_high_precision);
        }

        public static implicit operator DReal(int value) {
                return Create((long)value << fixedShift);
        }

        public static implicit operator DReal(uint value) {
                return Create((long)((ulong)value << fixedShift));
        }

        public static explicit operator float(DReal value) {
                return (float)((double)value.value / (double)(1 << fixedShift));
        }

        public override string ToString() {
                return ((float)this).ToString();
        }

        public static DReal operator +(DReal lhs, DReal rhs) {
                return Create(lhs.value + rhs.value);
        }

        public static DReal operator -(DReal lhs, DReal rhs) {
                return Create(lhs.value - rhs.value);
        }

        public static DReal operator -(DReal num) {
                return 0 - num;
        }

        public static DReal operator *(DReal lhs, DReal rhs) {
                return Create((lhs.value * rhs.value) >> fixedShift);
        }

        public static DReal operator /(DReal lhs, DReal rhs) {
                return Create((lhs.value << fixedShift) / rhs.value);
        }

        public static DReal operator %(DReal lhs, DReal rhs) {
                return Create(lhs.value % rhs.value);
        }

        public static bool operator ==(DReal lhs, DReal rhs) {
                return lhs.value == rhs.value;
        }

        public static bool operator !=(DReal lhs, DReal rhs) {
                return lhs.value != rhs.value;
        }

        public static bool operator <(DReal lhs, DReal rhs) {
                return lhs.value < rhs.value;
        }

        public static bool operator <=(DReal lhs, DReal rhs) {
                return lhs.value <= rhs.value;
        }

        public static bool operator >(DReal lhs, DReal rhs) {
                return lhs.value > rhs.value;
        }

        public static bool operator >=(DReal lhs, DReal rhs) {
                return lhs.value >= rhs.value;
        }

        public static DReal operator <<(DReal num, int Amount) {
                return Create(num.value << Amount);
        }

        public static DReal operator >>(DReal num, int Amount) {
                return Create(num.value >> Amount);
        }

        public override bool Equals(System.Object obj) {
                // If parameter is null return false.
                if (obj == null) {
                        return false;
                }

                // If parameter cannot be cast to Point return false.
                DReal p = (DReal)obj;

                // Return true if the fields match:
                return value == p.value;
        }

        public override int GetHashCode() {
                return (int)((value >> 32) ^ value);
        }

        // Floor remainder (% is truncate remainder).
        public static DReal Mod(DReal number, DReal divisor) {
                DReal rem = number % divisor;

                if(rem != 0 && (divisor < 0 ? number > 0 : number < 0)) {
                        return rem + divisor;
                } else {
                        return rem;
                }
        }

        public static DReal Sqrt(DReal f, int NumberOfIterations) {
                if(f.value < 0) {//NaN in Math.Sqrt
                        throw new System.ArithmeticException("Input Error");
                }
                if(f.value == 0) {
                        return 0;
                }

                DReal k = f + (DReal)1 >> 1;
                for(int i = 0; i < NumberOfIterations; i++) {
                        k = (k + (f / k)) >> 1;
                }

                if(k.value < 0) {
                        throw new System.ArithmeticException("Overflow");
                }

                return k;
        }

        public static DReal Abs(DReal n) {
                return (n < 0) ? -n : n;
        }

        public static DReal Sqrt(DReal f) {
                int numberOfIterations = 8;
                if(f > 100) {
                        numberOfIterations = 12;
                }
                if(f > 1000) {
                        numberOfIterations = 16;
                }
                return Sqrt(f, numberOfIterations);
        }

        // http://devmaster.net/forums/topic/4648-fast-and-accurate-sinecosine/
        public static DReal Sin(DReal x) {
                x = Mod((x + PI), TwoPI) - PI;
                DReal b = 4 / PI;
                DReal c = -4 / (PI * PI);
                DReal y = b * x + c * x * Abs(x);
                DReal p = Create(14746); // 0.225;
                return p * (y * Abs(y) - y) + y;
        }

        public static DReal Cos(DReal i) {
                return Sin(i + HalfPI);
        }

        public static DReal Tan(DReal i) {
                return Sin(i) / Cos(i);
        }

        public static DReal Atan(DReal F) {
                // http://forums.devshed.com/c-programming-42/implementing-an-atan-function-200106.html
                if(F == 0) {
                        return 0;
                }
                if(F < 0) {
                        return -Atan(-F);
                }
                // Caution: Magic.
                DReal x = (F - 1) / (F + 1);
                DReal y = x * x;
                DReal result = (Create(51471) + (((((((((((((((((Create(187) * y) - Create(1059)) * y) + Create(2812)) * y) - Create(4934)) * y) + Create(6983)) * y)
                                                - Create(9311))
                                               * y)
                                              + Create(13102))
                                             * y)
                                            - Create(21845))
                                           * y)
                                          + Create(65536))
                                         * x));
                //Debug.Log("Atan(" + F + ") = " + result + "  float: " + Mathf.Atan((float)F) + "  diff: " + Mathf.Abs(Mathf.Atan((float)F) - (float)result));
                return result;
        }

        public static DReal Atan2(DReal y, DReal x)
        {
                if(x > 0) return Atan(y/x);
                if(y >= 0 && x < 0) return Atan(y/x) + PI;
                if(y < 0 && x < 0) return Atan(y/x) - PI;
                if(y > 0 && x == 0) return HalfPI;
                if(y < 0 && x == 0) return -HalfPI;
                return 0;
        }

        public static DReal Radians(DReal degrees) {
                return degrees * (PI / 180);
        }

        public static DReal Degrees(DReal radians) {
                return radians * (180 / PI);
        }

        public static DReal Min(DReal a, DReal b) {
                return (a < b) ? a : b;
        }

        public static DReal Max(DReal a, DReal b) {
                return (a > b) ? a : b;
        }

        public static string Serialize(DReal n) {
                return n.value.ToString();
        }

        public static DReal Deserialize(string n) {
                return Create(System.Convert.ToInt64(n));
        }
}

[System.Serializable]
public struct DVector2 {
        public DReal x,y;

        public DVector2(DReal x, DReal y) {
                this.x = x;
                this.y = y;
        }

        public override string ToString() {
                return "(" + x + ", " + y + ")";
        }

        public DReal magnitude {
                get { return DReal.Sqrt(x * x + y * y); }
        }
        public DReal sqrMagnitude {
                get { return x * x + y * y; }
        }
        public DVector2 normalized {
                get {
                        DReal length = this.magnitude;
                        return new DVector2(x/length, y/length);
                }
        }

        public static DVector2 operator +(DVector2 lhs, DVector2 rhs) {
                return new DVector2(lhs.x + rhs.x, lhs.y + rhs.y);
        }

        public static DVector2 operator -(DVector2 lhs, DVector2 rhs) {
                return new DVector2(lhs.x - rhs.x, lhs.y - rhs.y);
        }

        public static DVector2 operator *(DVector2 lhs, DReal rhs) {
                return new DVector2(lhs.x * rhs, lhs.y * rhs);
        }

        public static DVector2 FromAngle(DReal radians) {
                return new DVector2(DReal.Cos(radians), DReal.Sin(radians));
        }

        public static DReal ToAngle(DVector2 vector) {
                return DReal.Atan2(vector.y, vector.x);
        }

        public static DReal Dot(DVector2 a, DVector2 b) {
                return a.x * b.x + a.y * b.y;
        }
}