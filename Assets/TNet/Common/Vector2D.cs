using System;

#if !STANDALONE
using UnityEngine;
#endif

namespace TNet
{
	/// <summary>
	/// Near-identical copy of the Unity's Vector2 struct, except using double precision.
	/// This class was created by duplicating Unity's functionality and replacing floats with doubles.
	/// </summary>

	[Serializable]
	public struct Vector2D
	{
		public double x;
		public double y;

		public Vector2D (double x, double y)
		{
			this.x = x;
			this.y = y;
		}

		public Vector2D (float x, float y)
		{
			this.x = (double)x;
			this.y = (double)y;
		}

		public Vector2D (Vector2D v)
		{
			this.x = v.x;
			this.y = v.y;
		}

		public Vector2D (Vector2 v)
		{
			this.x = (double)v.x;
			this.y = (double)v.y;
		}

#if !STANDALONE
		public Vector2D normalized { get { return Vector2D.Normalize(this); } }
		public double magnitude { get { return Math.Sqrt(x * x + y * y); } }
		public double sqrMagnitude { get { return x * x + y * y; } }
		static public Vector2D zero { get { return new Vector2D(0d, 0d); } }
		static public Vector2D one { get { return new Vector2D(1d, 1d); } }
		static public Vector2D up { get { return new Vector2D(0d, 1d); } }
		static public Vector2D down { get { return new Vector2D(0d, -1d); } }
		static public Vector2D left { get { return new Vector2D(-1d, 0d); } }
		static public Vector2D right { get { return new Vector2D(1d, 0d); } }

		static public Vector2D operator + (Vector2D a, Vector2D b)
		{
			return new Vector2D(a.x + b.x, a.y + b.y);
		}

		static public Vector2D operator - (Vector2D a, Vector2D b)
		{
			return new Vector2D(a.x - b.x, a.y - b.y);
		}

		static public Vector2D operator - (Vector2D a)
		{
			return new Vector2D(-a.x, -a.y);
		}

		static public Vector2D operator * (Vector2D a, double d)
		{
			return new Vector2D(a.x * d, a.y * d);
		}

		static public Vector2D operator * (double d, Vector2D a)
		{
			return new Vector2D(a.x * d, a.y * d);
		}

		static public Vector2D operator / (Vector2D a, double d)
		{
			return new Vector2D(a.x / d, a.y / d);
		}

		static public bool operator == (Vector2D lhs, Vector2D rhs)
		{
			double temp = lhs.x - rhs.x;
			if (temp < -0.00001 || temp > 0.00001) return false;
			temp = lhs.y - rhs.y;
			if (temp < -0.00001 || temp > 0.00001) return false;
			return true;
		}

		public bool AlmostEquals (Vector2D rhs, double threshold = 0.00001)
		{
			double temp = x - rhs.x;
			if (temp < -threshold || temp > threshold) return false;
			temp = y - rhs.y;
			if (temp < -threshold || temp > threshold) return false;
			return true;
		}

		public bool AlmostEquals (ref Vector2D rhs, double threshold = 0.00001)
		{
			double temp = x - rhs.x;
			if (temp < -threshold || temp > threshold) return false;
			temp = y - rhs.y;
			if (temp < -threshold || temp > threshold) return false;
			return true;
		}

		static public bool operator != (Vector2D lhs, Vector2D rhs)
		{
			return !(lhs == rhs);
		}

		static public implicit operator Vector2D (Vector2 v)
		{
			return new Vector2D((double)v.x, (double)v.y);
		}

		static public implicit operator Vector2 (Vector2D v)
		{
			return new Vector2((float)v.x, (float)v.y);
		}

		static public Vector2D Lerp (Vector2D from, Vector2D to, double factor)
		{
			if (factor < 0d) factor = 0d;
			if (factor > 1d) factor = 1d;
			return new Vector2D(from.x + (to.x - from.x) * factor, from.y + (to.y - from.y) * factor);
		}

		static public Vector2D Slerp (Vector2D from, Vector2D to, double factor)
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

		static public Vector2D MoveTowards (Vector2D current, Vector2D target, double maxDistanceDelta)
		{
			Vector2D vector3 = target - current;
			double magnitude = vector3.magnitude;
			return (magnitude <= maxDistanceDelta || magnitude == 0d) ? target :
				current + vector3 / magnitude * maxDistanceDelta;
		}

		static public Vector2D SmoothDamp (Vector2D current, Vector2D target, ref Vector2D currentVelocity, double smoothTime, double maxSpeed)
		{
			double deltaTime = (double)Time.deltaTime;
			return Vector2D.SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
		}

		static public Vector2D SmoothDamp (Vector2D current, Vector2D target, ref Vector2D currentVelocity, double smoothTime)
		{
			double deltaTime = (double)Time.deltaTime;
			double maxSpeed = double.PositiveInfinity;
			return Vector2D.SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
		}

		static public Vector2D SmoothDamp (Vector2D current, Vector2D target, ref Vector2D currentVelocity, double smoothTime, double maxSpeed, double deltaTime)
		{
			smoothTime = Math.Max(0.0001d, smoothTime);
			double num1 = 2d / smoothTime;
			double num2 = num1 * deltaTime;
			double num3 = (1.0d / (1.0d + num2 + 0.479999989271164d * num2 * num2 + 0.234999999403954d * num2 * num2 * num2));
			var vector = current - target;
			var vector3_1 = target;
			double maxLength = maxSpeed * smoothTime;
			var vector3_2 = Vector2D.ClampMagnitude(vector, maxLength);
			target = current - vector3_2;
			var vector3_3 = (currentVelocity + num1 * vector3_2) * deltaTime;
			currentVelocity = (currentVelocity - num1 * vector3_3) * num3;
			var vector3_4 = target + (vector3_2 + vector3_3) * num3;

			if (Dot(vector3_1 - current, vector3_4 - vector3_1) > 0.0)
			{
				vector3_4 = vector3_1;
				currentVelocity = (vector3_4 - vector3_1) / deltaTime;
			}
			return vector3_4;
		}

		public void Set (double x, double y)
		{
			this.x = x;
			this.y = y;
		}

		static public Vector2D Scale (Vector2D a, Vector2D b)
		{
			return new Vector2D(a.x * b.x, a.y * b.y);
		}

		public void Scale (Vector2D scale)
		{
			x *= scale.x;
			y *= scale.y;
		}

		public override int GetHashCode ()
		{
			return x.GetHashCode() ^ y.GetHashCode() << 2;
		}

		public override bool Equals (object other)
		{
			if (!(other is Vector2D)) return false;
			return this == (Vector2D)other;
		}

		static public Vector2D Normalize (Vector2D value)
		{
			double num = Vector2D.Magnitude(value);
			return (num > 0.00000000001) ? value / num : Vector2D.zero;
		}

		public void Normalize ()
		{
			double num = Vector2D.Magnitude(this);
			this = (num > 0.00000000001) ? this / num : Vector2D.zero;
		}

		public override string ToString ()
		{
			return "(" + x + ", " + y + ")";
		}

		static public double Dot (Vector2D lhs, Vector2D rhs)
		{
			return lhs.x * rhs.x + lhs.y * rhs.y;
		}

		static public Vector2D Project (Vector2D vector, Vector2D onNormal)
		{
			double num = Vector2D.Dot(onNormal, onNormal);
			return (num < 1.40129846432482E-45d) ? Vector2D.zero : onNormal * Vector2D.Dot(vector, onNormal) / num;
		}

		static public Vector2D Exclude (Vector2D excludeThis, Vector2D fromThat)
		{
			return fromThat - Vector2D.Project(fromThat, excludeThis);
		}

		static public double Angle (Vector2D from, Vector2D to)
		{
			return Math.Acos(Vector3D.Clamp(Dot(from.normalized, to.normalized), -1d, 1d)) * 57.29578d;
		}

		static public double Distance (Vector2D a, Vector2D b)
		{
			double x = a.x - b.x;
			double y = a.y - b.y;
			return Math.Sqrt(x * x + y * y);
		}

		static public double SqrDistance (Vector2D a, Vector2D b)
		{
			double x = a.x - b.x;
			double y = a.y - b.y;
			return x * x + y * y;
		}

		static public Vector2D ClampMagnitude (Vector2D vector, double maxLength)
		{
			if (vector.sqrMagnitude > maxLength * maxLength)
				return vector.normalized * maxLength;
			else
				return vector;
		}

		static public double Magnitude (Vector2D a)
		{
			return Math.Sqrt(a.x * a.x + a.y * a.y);
		}

		static public double SqrMagnitude (Vector2D a)
		{
			return a.x * a.x + a.y * a.y;
		}

		static public Vector2D Min (Vector2D lhs, Vector2D rhs)
		{
			return new Vector2D(Math.Min(lhs.x, rhs.x), Math.Min(lhs.y, rhs.y));
		}

		static public Vector2D Max (Vector2D lhs, Vector2D rhs)
		{
			return new Vector2D(Math.Max(lhs.x, rhs.x), Math.Max(lhs.y, rhs.y));
		}

		public Vector2 ToVector2 ()
		{
			return new Vector2((float)x, (float)y);
		}

		static Vector2D Rotate (Vector2D pos, float rot)
		{
			var halfZ = rot * Mathf.Deg2Rad * 0.5;
			var sinz = Math.Sin(halfZ);
			var cosz = Math.Cos(halfZ);
			var num3 = sinz * 2d;
			var num6 = sinz * num3;
			var num12 = cosz * num3;
			return new Vector2D((1d - num6) * pos.x - num12 * pos.y, num12 * pos.x + (1d - num6) * pos.y);
		}
#endif
	}
}
