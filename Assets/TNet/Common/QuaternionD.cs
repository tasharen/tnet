using System;
using System.IO;
using Vector3D = TNet.Vector3D;
using TNet;

#if !STANDALONE
using UnityEngine;
#endif

/// <summary>
/// Partial copy of Unity's Quaternion class with double precision.
/// </summary>

[Serializable]
public struct QuaternionD : IBinarySerializable
{
	const double PI = 3.14159265359;
	const double HALFPI = 1.57079632679;
	const double Deg2Rad = PI / 180.0;
	const double Rad2Deg = 180.0 / PI;

	public double x;
	public double y;
	public double z;
	public double w;

	public QuaternionD (double x, double y, double z, double w)
	{
		this.x = x;
		this.y = y;
		this.z = z;
		this.w = w;
	}

	public QuaternionD (QuaternionD q)
	{
		this.x = q.x;
		this.y = q.y;
		this.z = q.z;
		this.w = q.w;
	}

#if !STANDALONE
	public QuaternionD (Quaternion q)
	{
		this.x = (double)q.x;
		this.y = (double)q.y;
		this.z = (double)q.z;
		this.w = (double)q.w;
	}

	public QuaternionD (Vector4 q)
	{
		this.x = (double)q.x;
		this.y = (double)q.y;
		this.z = (double)q.z;
		this.w = (double)q.w;
	}
#endif

	[System.Diagnostics.DebuggerHidden]
	[System.Diagnostics.DebuggerStepThrough]
	static double WrapAngle (double angle)
	{
		while (angle > 180d) angle -= 360d;
		while (angle < -180d) angle += 360d;
		return angle;
	}

	static public QuaternionD operator * (QuaternionD lhs, QuaternionD rhs)
	{
		return new QuaternionD(
			(lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y),
			(lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z),
			(lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x),
			(lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z));
	}

	static public QuaternionD operator + (QuaternionD lhs, QuaternionD rhs) { return new QuaternionD(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z, lhs.w + rhs.w); }
	static public QuaternionD operator - (QuaternionD lhs, QuaternionD rhs) { return new QuaternionD(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z, lhs.w - rhs.w); }
	static public QuaternionD operator * (QuaternionD lhs, double scalar) { return new QuaternionD(lhs.x * scalar, lhs.y * scalar, lhs.z * scalar, lhs.w * scalar); }

#if !STANDALONE
	static public implicit operator QuaternionD (Quaternion q)
	{
		return new QuaternionD((double)q.x, (double)q.y, (double)q.z, (double)q.w);
	}

	static public implicit operator QuaternionD (Vector4 q)
	{
		return new QuaternionD((double)q.x, (double)q.y, (double)q.z, (double)q.w);
	}

	static public implicit operator Quaternion (QuaternionD q)
	{
		return new Quaternion((float)q.x, (float)q.y, (float)q.z, (float)q.w);
	}

	static public implicit operator Vector4 (QuaternionD q)
	{
		return new Vector4((float)q.x, (float)q.y, (float)q.z, (float)q.w);
	}
#endif

	static public Vector3D operator * (QuaternionD rotation, Vector3D point)
	{
		var num1 = rotation.x * 2.0;
		var num2 = rotation.y * 2.0;
		var num3 = rotation.z * 2.0;
		var num4 = rotation.x * num1;
		var num5 = rotation.y * num2;
		var num6 = rotation.z * num3;
		var num7 = rotation.x * num2;
		var num8 = rotation.x * num3;
		var num9 = rotation.y * num3;
		var num10 = rotation.w * num1;
		var num11 = rotation.w * num2;
		var num12 = rotation.w * num3;

		Vector3D v;
		v.x = ((1.0 - (num5 + num6)) * point.x + (num7 - num12) * point.y + (num8 + num11) * point.z);
		v.y = ((num7 + num12) * point.x + (1.0 - (num4 + num6)) * point.y + (num9 - num10) * point.z);
		v.z = ((num8 - num11) * point.x + (num9 + num10) * point.y + (1.0 - (num4 + num5)) * point.z);
		return v;
	}

	static public Vector2D operator * (QuaternionD rotation, Vector2D point)
	{
		var num2 = rotation.y * 2.0;
		var num5 = rotation.y * num2;
		var num11 = rotation.w * num2;

		Vector2D v;
		v.x = ((1.0 - num5) * point.x + num11 * point.y);
		v.y = (-num11 * point.x + (1.0 - num5) * point.y);
		return v;
	}

	static public Vector3 operator * (QuaternionD rotation, Vector3 point)
	{
		var num1 = rotation.x * 2.0;
		var num2 = rotation.y * 2.0;
		var num3 = rotation.z * 2.0;
		var num4 = rotation.x * num1;
		var num5 = rotation.y * num2;
		var num6 = rotation.z * num3;
		var num7 = rotation.x * num2;
		var num8 = rotation.x * num3;
		var num9 = rotation.y * num3;
		var num10 = rotation.w * num1;
		var num11 = rotation.w * num2;
		var num12 = rotation.w * num3;

		Vector3 v;
		v.x = (float)((1.0 - (num5 + num6)) * point.x + (num7 - num12) * point.y + (num8 + num11) * point.z);
		v.y = (float)((num7 + num12) * point.x + (1.0 - (num4 + num6)) * point.y + (num9 - num10) * point.z);
		v.z = (float)((num8 - num11) * point.x + (num9 + num10) * point.y + (1.0 - (num4 + num5)) * point.z);
		return v;
	}

	static public bool operator == (QuaternionD lhs, QuaternionD rhs)
	{
		return QuaternionD.Dot(lhs, rhs) > 0.999999999;
	}

	static public bool operator != (QuaternionD lhs, QuaternionD rhs)
	{
		return QuaternionD.Dot(lhs, rhs) <= 0.999999999;
	}

	// Constants used for quaternion-euler conversion
	const double r2d2 = Rad2Deg * 2.0;
	const double r2dh = Rad2Deg * HALFPI;
	const double d2rh = Deg2Rad * 0.5;

	/// <summary>
	/// Quaternion - euler conversion.
	/// </summary>

	public Vector3D eulerAngles
	{
		get
		{
			double xx = x * x;
			double yy = y * y;
			double zz = z * z;
			double ww = w * w;
			double unit = xx + yy + zz + ww;
			double test = x * w - y * z;
			Vector3D v;

			if (test > 0.4995f * unit)
			{
				v.x = r2dh;
				v.y = WrapAngle(r2d2 * Math.Atan2(y, x));
				v.z = 0d;
				return v;
			}

			if (test < -0.4995f * unit)
			{
				v.x = -r2dh;
				v.y = WrapAngle(-r2d2 * Math.Atan2(y, x));
				v.z = 0d;
				return v;
			}

			v.x = WrapAngle(Rad2Deg * Math.Asin(2d * (w * x - y * z)));
			v.y = WrapAngle(Rad2Deg * Math.Atan2(2d * w * y + 2d * z * x, 1d - 2d * (xx + yy)));
			v.z = WrapAngle(Rad2Deg * Math.Atan2(2d * w * z + 2d * x * y, 1d - 2d * (zz + xx)));
			return v;
		}
		set
		{
			double halfX = value.x * d2rh;
			double halfY = value.y * d2rh;
			double halfZ = value.z * d2rh;

			double sinx = Math.Sin(halfX);
			double siny = Math.Sin(halfY);
			double sinz = Math.Sin(halfZ);

			double cosx = Math.Cos(halfX);
			double cosy = Math.Cos(halfY);
			double cosz = Math.Cos(halfZ);

			double sysz = siny * sinz;
			double cysz = cosy * sinz;
			double sycz = siny * cosz;
			double cycz = cosy * cosz;

			x = sinx * cycz + cosx * sysz;
			y = cosx * sycz - sinx * cysz;
			z = cosx * cysz - sinx * sycz;
			w = cosx * cycz + sinx * sysz;

			Normalize();
		}
	}

	static public QuaternionD identity { get { return new QuaternionD(0d, 0d, 0d, 1d); } }

	public void Set (double x, double y, double z, double w)
	{
		this.x = x;
		this.y = y;
		this.z = z;
		this.w = w;
	}

	public void Normalize ()
	{
		double mag = Math.Sqrt(x * x + y * y + z * z + w * w);

		if (mag > 0.000001)
		{
			mag = 1f / mag;
			x *= mag;
			y *= mag;
			z *= mag;
			w *= mag;
		}
		else
		{
			x = 0f;
			y = 0f;
			z = 0f;
			w = 1f;
		}
	}

	public QuaternionD inverse { get { return QuaternionD.Inverse(this); } }
	public QuaternionD fastInverse { get { return new QuaternionD(-x, -y, -z, w); } }

	static public QuaternionD Inverse (QuaternionD q)
	{
		q.Normalize();
		q.x = -q.x;
		q.y = -q.y;
		q.z = -q.z;
		return q;
	}

	static public double Dot (QuaternionD a, QuaternionD b)
	{
		return (a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w);
	}

	static public double Angle (QuaternionD a, QuaternionD b)
	{
		return Math.Acos(Math.Min(Math.Abs(QuaternionD.Dot(a, b)), 1d)) * 114.5915603637696;
	}

	public override string ToString ()
	{
		return "(" + x + ", " + y + ", " + z + ", " + w + ")";
	}

	public override int GetHashCode ()
	{
		return x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2 ^ w.GetHashCode() >> 1;
	}

	public override bool Equals (object other)
	{
		if (other is QuaternionD)
		{
			QuaternionD quaternion = (QuaternionD)other;
			return x.Equals(quaternion.x) && y.Equals(quaternion.y) && z.Equals(quaternion.z) && w.Equals(quaternion.w);
		}
#if !STANDALONE
		else if (other is Quaternion)
		{
			Quaternion quaternion = (Quaternion)other;
			return x.Equals((double)quaternion.x) && y.Equals((double)quaternion.y) && z.Equals((double)quaternion.z) && w.Equals((double)quaternion.w);
		}
#endif
		return false;
	}

	public static QuaternionD AngleAxis (double degrees, Vector3D axis)
	{
		var mag = axis.x * axis.y * axis.z;
		if (mag == 0d) return identity;
		var radians = degrees * Deg2Rad * 0.5;
		mag = Math.Sqrt(mag);
		axis.x /= mag;
		axis.y /= mag;
		axis.z /= mag;
		var sin = Math.Sin(radians);
		axis.x *= sin;
		axis.y *= sin;
		axis.z *= sin;
		return new QuaternionD(axis.x, axis.y, axis.z, Math.Cos(radians));
	}

	/// <summary>
	/// Spherical interpolation.
	/// </summary>

	static public QuaternionD Slerp (QuaternionD from, QuaternionD to, double factor)
	{
		double dot = Dot(from, to), theta, sinInv, first, second = 1d;

		// Choose the shortest path
		if (dot < 0.0)
		{
			dot = -dot;
			second = -1.0;
		}

		// If the quaternions are too close together, LERP
		if (dot > 0.999998986721039) return from + (to - from) * factor;

		// Otherwise SLERP
		theta = Math.Acos(dot);
		sinInv = 1f / Math.Sin(theta);
		first = Math.Sin((1f - factor) * theta) * sinInv;
		second *= Math.Sin(factor * theta) * sinInv;

		// Final result is pretty straightforward
		return new QuaternionD(
			first * from.x + second * to.x,
			first * from.y + second * to.y,
			first * from.z + second * to.z,
			first * from.w + second * to.w);
	}

	/// <summary>
	/// Calculate the quaternion given the 3 euler angle values.
	/// </summary>

	static public QuaternionD Euler (double x, double y, double z)
	{
		double dx = x * Deg2Rad;
		double dy = y * Deg2Rad;
		double dz = z * Deg2Rad;

		double halfX = dx * 0.5;
		double halfY = dy * 0.5;
		double halfZ = dz * 0.5;

		double sinx = Math.Sin(halfX);
		double siny = Math.Sin(halfY);
		double sinz = Math.Sin(halfZ);

		double cosx = Math.Cos(halfX);
		double cosy = Math.Cos(halfY);
		double cosz = Math.Cos(halfZ);

		double sysz = siny * sinz;
		double cysz = cosy * sinz;
		double sycz = siny * cosz;
		double cycz = cosy * cosz;

		return new QuaternionD(
			sinx * cycz + cosx * sysz,
			cosx * sycz - sinx * cysz,
			cosx * cysz - sinx * sycz,
			cosx * cycz + sinx * sysz);
	}

	/// <summary>
	/// Calculate the quaternion given the Y rotation angle (in degrees).
	/// </summary>

	static public QuaternionD Euler (double y)
	{
		var r = y * Deg2Rad * 0.5;
		return new QuaternionD(0d, Math.Cos(r), 0d, Math.Sin(r));
	}

#if !STANDALONE
	/// <summary>
	/// Normalize the quaternion.
	/// </summary>

	static public Quaternion Normalize (Quaternion q)
	{
		var mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);

		if (mag > 0.000001f)
		{
			mag = 1f / mag;
			q.x *= mag;
			q.y *= mag;
			q.z *= mag;
			q.w *= mag;
		}
		else
		{
			q.x = 0f;
			q.y = 0f;
			q.z = 0f;
			q.w = 1f;
		}
		return q;
	}
#endif

	/// <summary>
	/// Normalize the quaternion.
	/// </summary>

	static public QuaternionD Normalize (QuaternionD q)
	{
		var mag = Math.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);

		if (mag > 0.000001)
		{
			mag = 1.0 / mag;
			q.x *= mag;
			q.y *= mag;
			q.z *= mag;
			q.w *= mag;
		}
		else
		{
			q.x = 0f;
			q.y = 0f;
			q.z = 0f;
			q.w = 1f;
		}
		return q;
	}

	/// <summary>
	/// Calculate the quaternion given the 3 euler angle values.
	/// </summary>

	static public QuaternionD Euler (Vector3D v) { return Euler(v.x, v.y, v.z); }

	/// <summary>
	/// Serialize the object's data into binary format.
	/// </summary>

	public void Serialize (BinaryWriter writer)
	{
		writer.Write(x);
		writer.Write(y);
		writer.Write(z);
		writer.Write(w);
	}

	/// <summary>
	/// Deserialize the object's data from binary format.
	/// </summary>

	public void Deserialize (BinaryReader reader)
	{
		x = reader.ReadDouble();
		y = reader.ReadDouble();
		z = reader.ReadDouble();
		w = reader.ReadDouble();
	}
}
