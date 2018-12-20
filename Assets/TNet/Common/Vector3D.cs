using System;

#if !STANDALONE
using UnityEngine;
#endif

namespace TNet
{
	/// <summary>
	/// Near-identical copy of the Unity's Vector3 struct, except using double precision.
	/// This class was created by duplicating Unity's functionality and replacing floats with doubles.
	/// </summary>

	[Serializable]
	public struct Vector3D
	{
		public double x;
		public double y;
		public double z;

		public Vector3D (double x, double y, double z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public Vector3D (float x, float y, float z)
		{
			this.x = (double)x;
			this.y = (double)y;
			this.z = (double)z;
		}

		public Vector3D (Vector3D v)
		{
			this.x = v.x;
			this.y = v.y;
			this.z = v.z;
		}

		public Vector3D (Vector3 v)
		{
			this.x = (double)v.x;
			this.y = (double)v.y;
			this.z = (double)v.z;
		}

		public Vector3D (double x, double y)
		{
			this.x = x;
			this.y = y;
			this.z = 0d;
		}

		static public bool operator == (Vector3D lhs, Vector3D rhs)
		{
			double temp = lhs.x - rhs.x;
			if (temp < -0.00001 || temp > 0.00001) return false;

			temp = lhs.y - rhs.y;
			if (temp < -0.00001 || temp > 0.00001) return false;

			temp = lhs.z - rhs.z;
			if (temp < -0.00001 || temp > 0.00001) return false;
			return true;
		}

		static public bool operator != (Vector3D lhs, Vector3D rhs)
		{
			return !(lhs == rhs);
		}

		static public implicit operator Vector3D (Vector3 v)
		{
			return new Vector3D((double)v.x, (double)v.y, (double)v.z);
		}

		static public implicit operator Vector3 (Vector3D v)
		{
			return new Vector3((float)v.x, (float)v.y, (float)v.z);
		}

		public override int GetHashCode ()
		{
			return x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2;
		}

		public override bool Equals (object other)
		{
			if (!(other is Vector3D)) return false;
			return this == (Vector3D)other;
		}

#if !STANDALONE
		public Vector3D normalized { get { return Vector3D.Normalize(this); } }
		public double magnitude { get { return Math.Sqrt(x * x + y * y + z * z); } }
		public double sqrMagnitude { get { return x * x + y * y + z * z; } }
		static public Vector3D zero { get { return new Vector3D(0d, 0d, 0d); } }
		static public Vector3D one { get { return new Vector3D(1d, 1d, 1d); } }
		static public Vector3D forward { get { return new Vector3D(0d, 0d, 1d); } }
		static public Vector3D back { get { return new Vector3D(0d, 0d, -1d); } }
		static public Vector3D up { get { return new Vector3D(0d, 1d, 0d); } }
		static public Vector3D down { get { return new Vector3D(0d, -1d, 0d); } }
		static public Vector3D left { get { return new Vector3D(-1d, 0d, 0d); } }
		static public Vector3D right { get { return new Vector3D(1d, 0d, 0d); } }

		static public Vector3D operator + (Vector3D a, Vector3D b)
		{
			return new Vector3D(a.x + b.x, a.y + b.y, a.z + b.z);
		}

		static public Vector3D operator - (Vector3D a, Vector3D b)
		{
			return new Vector3D(a.x - b.x, a.y - b.y, a.z - b.z);
		}

		static public Vector3D operator + (Vector3D a, Vector3 b)
		{
			return new Vector3D(a.x + b.x, a.y + b.y, a.z + b.z);
		}

		static public Vector3D operator - (Vector3D a, Vector3 b)
		{
			return new Vector3D(a.x - b.x, a.y - b.y, a.z - b.z);
		}

		static public Vector3D operator - (Vector3D a)
		{
			return new Vector3D(-a.x, -a.y, -a.z);
		}

		static public Vector3D operator * (Vector3D a, double d)
		{
			return new Vector3D(a.x * d, a.y * d, a.z * d);
		}

		static public Vector3D operator * (double d, Vector3D a)
		{
			return new Vector3D(a.x * d, a.y * d, a.z * d);
		}

		static public Vector3D operator / (Vector3D a, double d)
		{
			return new Vector3D(a.x / d, a.y / d, a.z / d);
		}

		public bool isNanOrInfinity
		{
			get
			{
				if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z)) return true;
				if (double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z)) return true;
				return false;
			}
		}

		public bool AlmostEquals (Vector3D rhs, double threshold = 0.00001)
		{
			double temp = x - rhs.x;
			if (temp < -threshold || temp > threshold) return false;

			temp = y - rhs.y;
			if (temp < -threshold || temp > threshold) return false;

			temp = z - rhs.z;
			if (temp < -threshold || temp > threshold) return false;
			return true;
		}

		public bool AlmostEquals (ref Vector3D rhs, double threshold = 0.00001)
		{
			double temp = x - rhs.x;
			if (temp < -threshold || temp > threshold) return false;

			temp = y - rhs.y;
			if (temp < -threshold || temp > threshold) return false;

			temp = z - rhs.z;
			if (temp < -threshold || temp > threshold) return false;
			return true;
		}

		static public Vector3D Lerp (Vector3D from, Vector3D to, double factor)
		{
			if (factor < 0d) factor = 0d;
			if (factor > 1d) factor = 1d;
			return new Vector3D(from.x + (to.x - from.x) * factor, from.y + (to.y - from.y) * factor, from.z + (to.z - from.z) * factor);
		}

		static public Vector3D Slerp (Vector3D from, Vector3D to, double factor)
		{
			double magFrom = from.magnitude;
			if (magFrom == 0.0) return Lerp(from, to, factor);

			double magTo = to.magnitude;
			if (magTo == 0.0) return Lerp(from, to, factor);

			double mag = (magFrom + magTo) * factor;
			from *= 1.0 / magFrom;
			to *= 1.0 / magTo;
			return Lerp(from, to, factor) * mag;
		}

		static public Vector3D MoveTowards (Vector3D current, Vector3D target, double maxDistanceDelta)
		{
			Vector3D vector3 = target - current;
			double magnitude = vector3.magnitude;
			return (magnitude <= maxDistanceDelta || magnitude == 0d) ? target :
				current + vector3 / magnitude * maxDistanceDelta;
		}

		static public Vector3D SmoothDamp (Vector3D current, Vector3D target, ref Vector3D currentVelocity, double smoothTime, double maxSpeed)
		{
			double deltaTime = (double)Time.deltaTime;
			return Vector3D.SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
		}

		static public Vector3D SmoothDamp (Vector3D current, Vector3D target, ref Vector3D currentVelocity, double smoothTime)
		{
			double deltaTime = (double)Time.deltaTime;
			double maxSpeed = double.PositiveInfinity;
			return Vector3D.SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
		}

		static public Vector3D SmoothDamp (Vector3D current, Vector3D target, ref Vector3D currentVelocity, double smoothTime, double maxSpeed, double deltaTime)
		{
			smoothTime = Math.Max(0.0001d, smoothTime);
			double num1 = 2d / smoothTime;
			double num2 = num1 * deltaTime;
			double num3 = (1.0d / (1.0d + num2 + 0.479999989271164d * num2 * num2 + 0.234999999403954d * num2 * num2 * num2));
			Vector3D vector = current - target;
			Vector3D vector3_1 = target;
			double maxLength = maxSpeed * smoothTime;
			Vector3D vector3_2 = Vector3D.ClampMagnitude(vector, maxLength);
			target = current - vector3_2;
			Vector3D vector3_3 = (currentVelocity + num1 * vector3_2) * deltaTime;
			currentVelocity = (currentVelocity - num1 * vector3_3) * num3;
			Vector3D vector3_4 = target + (vector3_2 + vector3_3) * num3;

			if (Vector3D.Dot(vector3_1 - current, vector3_4 - vector3_1) > 0.0)
			{
				vector3_4 = vector3_1;
				currentVelocity = (vector3_4 - vector3_1) / deltaTime;
			}
			return vector3_4;
		}

		public void Set (double x, double y, double z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		static public Vector3D Scale (Vector3D a, Vector3D b)
		{
			return new Vector3D(a.x * b.x, a.y * b.y, a.z * b.z);
		}

		public void Scale (Vector3D scale)
		{
			x *= scale.x;
			y *= scale.y;
			z *= scale.z;
		}

		static public Vector3D Cross (Vector3D lhs, Vector3D rhs)
		{
			return new Vector3D(lhs.y * rhs.z - lhs.z * rhs.y, lhs.z * rhs.x - lhs.x * rhs.z, lhs.x * rhs.y - lhs.y * rhs.x);
		}

		static public Vector3D Reflect (Vector3D inDirection, Vector3D inNormal)
		{
			return -2d * Vector3D.Dot(inNormal, inDirection) * inNormal + inDirection;
		}

		static public Vector3D Normalize (Vector3D value)
		{
			double num = Vector3D.Magnitude(value);
			return (num > 0.00000000001) ? value / num : Vector3D.zero;
		}

		public void Normalize ()
		{
			double num = Vector3D.Magnitude(this);
			this = (num > 0.00000000001) ? this / num : Vector3D.zero;
		}

		public override string ToString ()
		{
			return "(" + x + ", " + y + ", " + z + ")";
		}

		static public double Dot (Vector3D lhs, Vector3D rhs)
		{
			return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
		}

		static public Vector3D Project (Vector3D vector, Vector3D onNormal)
		{
			double num = Vector3D.Dot(onNormal, onNormal);
			return (num < 1.40129846432482E-45d) ? Vector3D.zero : onNormal * Vector3D.Dot(vector, onNormal) / num;
		}

		static public Vector3D Exclude (Vector3D excludeThis, Vector3D fromThat)
		{
			return fromThat - Vector3D.Project(fromThat, excludeThis);
		}

		static public double Angle (Vector3D from, Vector3D to)
		{
			return Math.Acos(Clamp(Dot(from.normalized, to.normalized), -1d, 1d)) * 57.29578d;
		}

		static public double Clamp (double value, double min, double max)
		{
			if (value < min) value = min;
			else if (value > max) value = max;
			return value;
		}

		static public double Distance (Vector3D a, Vector3D b)
		{
			var x = a.x - b.x;
			var y = a.y - b.y;
			var z = a.z - b.z;
			return Math.Sqrt(x * x + y * y + z * z);
		}

		static public double FlatDistance (Vector3D a, Vector3D b)
		{
			var x = a.x - b.x;
			var z = a.z - b.z;
			return Math.Sqrt(x * x + z * z);
		}

		static public float SqrDistance (Vector3 a, Vector3 b)
		{
			var x = a.x - b.x;
			var y = a.y - b.y;
			var z = a.z - b.z;
			return x * x + y * y + z * z;
		}

		static public double SqrDistance (Vector3D a, Vector3D b)
		{
			var x = a.x - b.x;
			var y = a.y - b.y;
			var z = a.z - b.z;
			return x * x + y * y + z * z;
		}

		static public double FlatSqrDistance (Vector3D a, Vector3D b)
		{
			var x = a.x - b.x;
			var z = a.z - b.z;
			return (x * x + z * z);
		}

		static public float FlatSqrDistance (Vector3 a, Vector3 b)
		{
			var x = a.x - b.x;
			var z = a.z - b.z;
			return (x * x + z * z);
		}

		static public Vector3D ClampMagnitude (Vector3D vector, double maxLength)
		{
			if (vector.sqrMagnitude > maxLength * maxLength) return vector.normalized * maxLength;
			return vector;
		}

		static public double Magnitude (Vector3D a)
		{
			return Math.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z);
		}

		static public float Magnitude (Vector3 a)
		{
			return Mathf.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z);
		}

		static public double SqrMagnitude (Vector3D a)
		{
			return a.x * a.x + a.y * a.y + a.z * a.z;
		}

		static public float SqrMagnitude (Vector3 a)
		{
			return a.x * a.x + a.y * a.y + a.z * a.z;
		}

		static public Vector3D Min (Vector3D lhs, Vector3D rhs)
		{
			return new Vector3D(Math.Min(lhs.x, rhs.x), Math.Min(lhs.y, rhs.y), Math.Min(lhs.z, rhs.z));
		}

		static public Vector3D Max (Vector3D lhs, Vector3D rhs)
		{
			return new Vector3D(Math.Max(lhs.x, rhs.x), Math.Max(lhs.y, rhs.y), Math.Max(lhs.z, rhs.z));
		}

		public Vector3 ToVector3 ()
		{
			return new Vector3((float)x, (float)y, (float)z);
		}

		public Quaternion ToLookRotation ()
		{
			return Quaternion.LookRotation(ToVector3());
		}

		/// <summary>
		/// Round the 3 values.
		/// </summary>

		public Vector3D Round ()
		{
			return new Vector3D(Math.Round(x), Math.Round(y), Math.Round(z));
		}
#endif // STANDALONE
	}
}