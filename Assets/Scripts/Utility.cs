using UnityEngine;

static public class Utility {
        public static bool IntersectLineCircle(DVector2 circleOrigin, DReal radius, DVector2 lineStart, DVector2 lineEnd, out DVector2 outIp) {
                var d = lineEnd - lineStart; // ray direction.
                var f = lineStart - circleOrigin; // Vector from circle centre to ray start.

                var a = DVector2.Dot(d, d);
                var b = 2 * DVector2.Dot(f, d);
                var c = DVector2.Dot(f, f) - radius * radius;

                if(a == 0) {
                        // start & end are close enough that distance = 0. Degrade to point/circle test.
                        outIp = lineStart;
                        return (lineStart - circleOrigin).sqrMagnitude < radius*radius;
                }

                var discriminant = b * b - 4 * a * c;

                if(discriminant < 0) {
                        outIp = new DVector2();
                        return false;
                }

                // ray didn't totally miss sphere,
                // so there is a solution to
                // the equation.
                discriminant = DReal.Sqrt(discriminant);

                // either solution may be on or off the ray so need to test both
                // t1 is always the smaller value, because BOTH discriminant and
                // a are nonnegative.
                var t1 = (-b - discriminant)/(2*a);
                var t2 = (-b + discriminant)/(2*a);

                // 3x HIT cases:
                //          -o->             --|-->  |            |  --|->
                // Impale(t1 hit,t2 hit), Poke(t1 hit,t2>1), ExitWound(t1<0, t2 hit),

                // 3x MISS cases:
                //       ->  o                     o ->              | -> |
                // FallShort (t1>1,t2>1), Past (t1<0,t2<0), CompletelyInside(t1<0, t2>1)
                if(t1 >= 0 && t1 <= 1) {
                        // t1 is the intersection, and it's closer than t2
                        // (since t1 uses -b - discriminant)
                        // Impale, Poke
                        outIp = circleOrigin + d.normalized * -radius;
                        return true;
                }

                // here t1 didn't intersect so we are either started
                // inside the sphere or completely past it
                if(t2 >= 0 && t2 <= 1) {
                        // ExitWound
                        outIp = circleOrigin + d.normalized * radius;
                        return true ;
                }

                // no intn: FallShort, Past, CompletelyInside
                outIp = new DVector2();
                return false;
	}
}
/*


		float l1 = (b - b4ac) * diva;
		float l2 = (b + b4ac) * diva;

		// we need the closest intersection point.. so find smaller l.
		// To get the other end , just change the comparison operator.
		float l = l2;
		if (l2 < l1)
		{
			l = l1;
		}
                */