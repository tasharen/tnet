using System;
using System.IO;

#if !STANDALONE
using UnityEngine;
#endif

namespace TNet
{
	/// <summary>
	/// Partial copy of the Unity's Matrix4x4 struct using double precision.
	/// </summary>

	[Serializable]
	public struct Matrix4x4D : IBinarySerializable
	{
		public double m00;
		public double m10;
		public double m20;
		public double m30;

		public double m01;
		public double m11;
		public double m21;
		public double m31;

		public double m02;
		public double m12;
		public double m22;
		public double m32;

		public double m03;
		public double m13;
		public double m23;
		public double m33;

		public Matrix4x4D (
			double p00, double p01, double p02, double p03,
			double p10, double p11, double p12, double p13,
			double p20, double p21, double p22, double p23,
			double p30, double p31, double p32, double p33)
		{
			m00 = p00;
			m01 = p01;
			m02 = p02;
			m03 = p03;
			m10 = p10;
			m11 = p11;
			m12 = p12;
			m13 = p13;
			m20 = p20;
			m21 = p21;
			m22 = p22;
			m23 = p23;
			m30 = p30;
			m31 = p31;
			m32 = p32;
			m33 = p33;
		}

		public Matrix4x4D (Matrix4x4 mat)
		{
			m00 = mat.m00;
			m01 = mat.m01;
			m02 = mat.m02;
			m03 = mat.m03;
			m10 = mat.m10;
			m11 = mat.m11;
			m12 = mat.m12;
			m13 = mat.m13;
			m20 = mat.m20;
			m21 = mat.m21;
			m22 = mat.m22;
			m23 = mat.m23;
			m30 = mat.m30;
			m31 = mat.m31;
			m32 = mat.m32;
			m33 = mat.m33;
		}

		public Matrix4x4D (Matrix4x4D mat)
		{
			m00 = mat.m00;
			m01 = mat.m01;
			m02 = mat.m02;
			m03 = mat.m03;
			m10 = mat.m10;
			m11 = mat.m11;
			m12 = mat.m12;
			m13 = mat.m13;
			m20 = mat.m20;
			m21 = mat.m21;
			m22 = mat.m22;
			m23 = mat.m23;
			m30 = mat.m30;
			m31 = mat.m31;
			m32 = mat.m32;
			m33 = mat.m33;
		}

		static public Matrix4x4D zero
		{
			get
			{
				return new Matrix4x4D()
				{
					m00 = 0d,
					m01 = 0d,
					m02 = 0d,
					m03 = 0d,
					m10 = 0d,
					m11 = 0d,
					m12 = 0d,
					m13 = 0d,
					m20 = 0d,
					m21 = 0d,
					m22 = 0d,
					m23 = 0d,
					m30 = 0d,
					m31 = 0d,
					m32 = 0d,
					m33 = 0d
				};
			}
		}

		static public readonly Matrix4x4D identity = new Matrix4x4D()
		{
			m00 = 1d,
			m01 = 0d,
			m02 = 0d,
			m03 = 0d,
			m10 = 0d,
			m11 = 1d,
			m12 = 0d,
			m13 = 0d,
			m20 = 0d,
			m21 = 0d,
			m22 = 1d,
			m23 = 0d,
			m30 = 0d,
			m31 = 0d,
			m32 = 0d,
			m33 = 1d
		};

		/// <summary>
		/// Column-major access.
		/// </summary>

		public double this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: return m00;
					case 1: return m10;
					case 2: return m20;
					case 3: return m30;
					case 4: return m01;
					case 5: return m11;
					case 6: return m21;
					case 7: return m31;
					case 8: return m02;
					case 9: return m12;
					case 10: return m22;
					case 11: return m32;
					case 12: return m03;
					case 13: return m13;
					case 14: return m23;
					case 15: return m33;
				}
				return 0d;
			}
			set
			{
				switch (index)
				{
					case 0: m00 = value; break;
					case 1: m10 = value; break;
					case 2: m20 = value; break;
					case 3: m30 = value; break;
					case 4: m01 = value; break;
					case 5: m11 = value; break;
					case 6: m21 = value; break;
					case 7: m31 = value; break;
					case 8: m02 = value; break;
					case 9: m12 = value; break;
					case 10: m22 = value; break;
					case 11: m32 = value; break;
					case 12: m03 = value; break;
					case 13: m13 = value; break;
					case 14: m23 = value; break;
					case 15: m33 = value; break;
				}
			}
		}

		static public explicit operator Matrix4x4D (Matrix4x4 m)
		{
			return new Matrix4x4D(
				(double)m.m00, (double)m.m01, (double)m.m02, (double)m.m03,
				(double)m.m10, (double)m.m11, (double)m.m12, (double)m.m13,
				(double)m.m20, (double)m.m21, (double)m.m22, (double)m.m23,
				(double)m.m30, (double)m.m31, (double)m.m32, (double)m.m33);
		}

		static public Matrix4x4D operator * (Matrix4x4D lhs, Matrix4x4D rhs)
		{
			return new Matrix4x4D()
			{
				m00 = (lhs.m00 * rhs.m00 + lhs.m01 * rhs.m10 + lhs.m02 * rhs.m20 + lhs.m03 * rhs.m30),
				m01 = (lhs.m00 * rhs.m01 + lhs.m01 * rhs.m11 + lhs.m02 * rhs.m21 + lhs.m03 * rhs.m31),
				m02 = (lhs.m00 * rhs.m02 + lhs.m01 * rhs.m12 + lhs.m02 * rhs.m22 + lhs.m03 * rhs.m32),
				m03 = (lhs.m00 * rhs.m03 + lhs.m01 * rhs.m13 + lhs.m02 * rhs.m23 + lhs.m03 * rhs.m33),
			
				m10 = (lhs.m10 * rhs.m00 + lhs.m11 * rhs.m10 + lhs.m12 * rhs.m20 + lhs.m13 * rhs.m30),
				m11 = (lhs.m10 * rhs.m01 + lhs.m11 * rhs.m11 + lhs.m12 * rhs.m21 + lhs.m13 * rhs.m31),
				m12 = (lhs.m10 * rhs.m02 + lhs.m11 * rhs.m12 + lhs.m12 * rhs.m22 + lhs.m13 * rhs.m32),
				m13 = (lhs.m10 * rhs.m03 + lhs.m11 * rhs.m13 + lhs.m12 * rhs.m23 + lhs.m13 * rhs.m33),
			
				m20 = (lhs.m20 * rhs.m00 + lhs.m21 * rhs.m10 + lhs.m22 * rhs.m20 + lhs.m23 * rhs.m30),
				m21 = (lhs.m20 * rhs.m01 + lhs.m21 * rhs.m11 + lhs.m22 * rhs.m21 + lhs.m23 * rhs.m31),
				m22 = (lhs.m20 * rhs.m02 + lhs.m21 * rhs.m12 + lhs.m22 * rhs.m22 + lhs.m23 * rhs.m32),
				m23 = (lhs.m20 * rhs.m03 + lhs.m21 * rhs.m13 + lhs.m22 * rhs.m23 + lhs.m23 * rhs.m33),
			
				m30 = (lhs.m30 * rhs.m00 + lhs.m31 * rhs.m10 + lhs.m32 * rhs.m20 + lhs.m33 * rhs.m30),
				m31 = (lhs.m30 * rhs.m01 + lhs.m31 * rhs.m11 + lhs.m32 * rhs.m21 + lhs.m33 * rhs.m31),
				m32 = (lhs.m30 * rhs.m02 + lhs.m31 * rhs.m12 + lhs.m32 * rhs.m22 + lhs.m33 * rhs.m32),
				m33 = (lhs.m30 * rhs.m03 + lhs.m31 * rhs.m13 + lhs.m32 * rhs.m23 + lhs.m33 * rhs.m33)
			};
		}

		static public Vector3D operator * (Matrix4x4D lhs, Vector3D v)
		{
			return new Vector3D()
			{
				x = lhs.m00 * v.x + lhs.m01 * v.y + lhs.m02 * v.z,
				y = lhs.m10 * v.x + lhs.m11 * v.y + lhs.m12 * v.z,
				z = lhs.m20 * v.x + lhs.m21 * v.y + lhs.m22 * v.z
			};
		}

#if !STANDALONE
		static public Quaternion operator * (Matrix4x4D lhs, Quaternion rot)
		{
			var fw = rot * Vector3.forward;
			var up = rot * Vector3.up;
			fw = lhs.MultiplyVector(fw);
			up = lhs.MultiplyVector(up);
			return Quaternion.LookRotation(fw, up);
		}
#endif

		public Matrix4x4D transpose
		{
			get
			{
				Matrix4x4D m;
				m.m00 = m00; m.m01 = m10; m.m02 = m20; m.m03 = m30;
				m.m10 = m01; m.m11 = m11; m.m12 = m21; m.m13 = m31;
				m.m20 = m02; m.m21 = m12; m.m22 = m22; m.m23 = m32;
				m.m30 = m03; m.m31 = m13; m.m32 = m23; m.m33 = m33;
				return m;
			}
		}

		public Matrix4x4D inverse3x4
		{
			get
			{
				double det;

				det = m00 * m11 * m22;
				det += m01 * m12 * m20;
				det += m02 * m10 * m21;
				det -= m02 * m11 * m20;
				det -= m01 * m10 * m22;
				det -= m00 * m12 * m21;
				det = 1.0 / det;

				Matrix4x4D m;

				m.m00 =  (m11 * m22 - m12 * m21) * det;
				m.m10 = -(m10 * m22 - m12 * m20) * det;
				m.m20 =  (m10 * m21 - m11 * m20) * det;
				m.m30 = 0.0;

				m.m01 = -(m01 * m22 - m02 * m21) * det;
				m.m11 =  (m00 * m22 - m02 * m20) * det;
				m.m21 = -(m00 * m21 - m01 * m20) * det;
				m.m31 = 0.0;

				m.m02 =  (m01 * m12 - m02 * m11) * det;
				m.m12 = -(m00 * m12 - m02 * m10) * det;
				m.m22 =  (m00 * m11 - m01 * m10) * det;
				m.m32 = 0.0;

				m.m03 = -(m03 * m.m00 + m13 * m.m01 + m23 * m.m02);
				m.m13 = -(m03 * m.m10 + m13 * m.m11 + m23 * m.m12);
				m.m23 = -(m03 * m.m20 + m13 * m.m21 + m23 * m.m22);
				m.m33 = 1.0;

				return m;
			}
		}

		public Matrix4x4D inverse
		{
			get
			{
				double det = GetDeterminant();
				if (det == 0.0) return this;
				det = 1.0 / det;
				Matrix4x4D mat;

				mat.m00 = (-m13 * m22 * m31 + m12 * m23 * m31 + m13 * m21 * m32 -
							m11 * m23 * m32 - m12 * m21 * m33 + m11 * m22 * m33) * det;
				mat.m10 = ( m13 * m22 * m30 - m12 * m23 * m30 - m13 * m20 * m32 +
							m10 * m23 * m32 + m12 * m20 * m33 - m10 * m22 * m33) * det;
				mat.m20 = (-m13 * m21 * m30 + m11 * m23 * m30 + m13 * m20 * m31 -
							m10 * m23 * m31 - m11 * m20 * m33 + m10 * m21 * m33) * det;
				mat.m30 = ( m12 * m21 * m30 - m11 * m22 * m30 - m12 * m20 * m31 +
							m10 * m22 * m31 + m11 * m20 * m32 - m10 * m21 * m32) * det;

				mat.m01 = ( m03 * m22 * m31 - m02 * m23 * m31 - m03 * m21 * m32 +
							m01 * m23 * m32 + m02 * m21 * m33 - m01 * m22 * m33) * det;
				mat.m11 = (-m03 * m22 * m30 + m02 * m23 * m30 + m03 * m20 * m32 -
							m00 * m23 * m32 - m02 * m20 * m33 + m00 * m22 * m33) * det;
				mat.m21 = ( m03 * m21 * m30 - m01 * m23 * m30 - m03 * m20 * m31 +
							m00 * m23 * m31 + m01 * m20 * m33 - m00 * m21 * m33) * det;
				mat.m31 = (-m02 * m21 * m30 + m01 * m22 * m30 + m02 * m20 * m31 -
							m00 * m22 * m31 - m01 * m20 * m32 + m00 * m21 * m32) * det;

				mat.m02 = (-m03 * m12 * m31 + m02 * m13 * m31 + m03 * m11 * m32 -
							m01 * m13 * m32 - m02 * m11 * m33 + m01 * m12 * m33) * det;
				mat.m12 = ( m03 * m12 * m30 - m02 * m13 * m30 - m03 * m10 * m32 +
							m00 * m13 * m32 + m02 * m10 * m33 - m00 * m12 * m33) * det;
				mat.m22 = (-m03 * m11 * m30 + m01 * m13 * m30 + m03 * m10 * m31 -
							m00 * m13 * m31 - m01 * m10 * m33 + m00 * m11 * m33) * det;
				mat.m32 = ( m02 * m11 * m30 - m01 * m12 * m30 - m02 * m10 * m31 +
							m00 * m12 * m31 + m01 * m10 * m32 - m00 * m11 * m32) * det;

				mat.m03 = ( m03 * m12 * m21 - m02 * m13 * m21 - m03 * m11 * m22 +
							m01 * m13 * m22 + m02 * m11 * m23 - m01 * m12 * m23) * det;
				mat.m13 = (-m03 * m12 * m20 + m02 * m13 * m20 + m03 * m10 * m22 -
							m00 * m13 * m22 - m02 * m10 * m23 + m00 * m12 * m23) * det;
				mat.m23 = ( m03 * m11 * m20 - m01 * m13 * m20 - m03 * m10 * m21 +
							m00 * m13 * m21 + m01 * m10 * m23 - m00 * m11 * m23) * det;
				mat.m33 = (-m02 * m11 * m20 + m01 * m12 * m20 + m02 * m10 * m21 -
							m00 * m12 * m21 - m01 * m10 * m22 + m00 * m11 * m22) * det;
				return mat;
			}
		}

		public double GetDeterminant ()
		{
			return m03 * m12 * m21 * m30 -
				  m02 * m13 * m21 * m30 -
				  m03 * m11 * m22 * m30 +
				  m01 * m13 * m22 * m30 +
				  m02 * m11 * m23 * m30 -
				  m01 * m12 * m23 * m30 -
				  m03 * m12 * m20 * m31 +
				  m02 * m13 * m20 * m31 +
				  m03 * m10 * m22 * m31 -
				  m00 * m13 * m22 * m31 -
				  m02 * m10 * m23 * m31 +
				  m00 * m12 * m23 * m31 +
				  m03 * m11 * m20 * m32 -
				  m01 * m13 * m20 * m32 -
				  m03 * m10 * m21 * m32 +
				  m00 * m13 * m21 * m32 +
				  m01 * m10 * m23 * m32 -
				  m00 * m11 * m23 * m32 -
				  m02 * m11 * m20 * m33 +
				  m01 * m12 * m20 * m33 +
				  m02 * m10 * m21 * m33 -
				  m00 * m12 * m21 * m33 -
				  m01 * m10 * m22 * m33 +
				  m00 * m11 * m22 * m33;
		}

		public Vector3D MultiplyVector (Vector3D v)
		{
			return new Vector3D(
				v.x * m00 + v.y * m01 + v.z * m02,
				v.x * m10 + v.y * m11 + v.z * m12,
				v.x * m20 + v.y * m21 + v.z * m22);
		}

		public Vector3 MultiplyVector (Vector3 v)
		{
			return new Vector3(
				(float)(v.x * m00 + v.y * m01 + v.z * m02),
				(float)(v.x * m10 + v.y * m11 + v.z * m12),
				(float)(v.x * m20 + v.y * m21 + v.z * m22));
		}

		public Vector3D MultiplyPoint3x4 (Vector3D v)
		{
			Vector3D vector3;
			vector3.x = m00 * v.x + m01 * v.y + m02 * v.z + m03;
			vector3.y = m10 * v.x + m11 * v.y + m12 * v.z + m13;
			vector3.z = m20 * v.x + m21 * v.y + m22 * v.z + m23;
			return vector3;
		}

		public Vector3 MultiplyPoint3x4 (Vector3 v)
		{
			Vector3 vector3;
			vector3.x = (float)(m00 * v.x + m01 * v.y + m02 * v.z + m03);
			vector3.y = (float)(m10 * v.x + m11 * v.y + m12 * v.z + m13);
			vector3.z = (float)(m20 * v.x + m21 * v.y + m22 * v.z + m23);
			return vector3;
		}

		public Vector3D MultiplyPoint (Vector3D v)
		{
			Vector3D vector3;
			vector3.x = (m00 * v.x + m01 * v.y + m02 * v.z) + m03;
			vector3.y = (m10 * v.x + m11 * v.y + m12 * v.z) + m13;
			vector3.z = (m20 * v.x + m21 * v.y + m22 * v.z) + m23;
			double num = ((m30 * v.x + m31 * v.y + m32 * v.z) + m33);

			if (num != 0d)
			{
				num = 1d / num;
				vector3.x *= num;
				vector3.y *= num;
				vector3.z *= num;
			}
			return vector3;
		}

		public Vector3 MultiplyPoint (Vector3 v)
		{
			Vector3D vector3;
			vector3.x = (m00 * v.x + m01 * v.y + m02 * v.z) + m03;
			vector3.y = (m10 * v.x + m11 * v.y + m12 * v.z) + m13;
			vector3.z = (m20 * v.x + m21 * v.y + m22 * v.z) + m23;
			double num = ((m30 * v.x + m31 * v.y + m32 * v.z) + m33);

			if (num != 0d)
			{
				num = 1d / num;
				vector3.x *= num;
				vector3.y *= num;
				vector3.z *= num;
			}
			return new Vector3((float)vector3.x, (float)vector3.y, (float)vector3.z);
		}

		static public Matrix4x4D TRS (Vector3D pos, QuaternionD rot, Vector3D scale)
		{
			double xx, xy, xz, xw, yy, yz, yw, zz, zw;

			xx = rot.x * rot.x;
			xy = rot.x * rot.y;
			xz = rot.x * rot.z;
			xw = rot.x * rot.w;

			yy = rot.y * rot.y;
			yz = rot.y * rot.z;
			yw = rot.y * rot.w;

			zz = rot.z * rot.z;
			zw = rot.z * rot.w;

			Matrix4x4D m;
			m.m00 = 1.0 - 2.0 * (yy + zz);
			m.m10 = 2.0 * (xy + zw);
			m.m20 = 2.0 * (xz - yw);
			m.m30 = 0.0;

			m.m01 = 2.0 * (xy - zw);
			m.m11 = 1.0 - 2.0 * (xx + zz);
			m.m21 = 2.0 * (yz + xw);
			m.m31 = 0.0;

			m.m02 = 2.0 * (xz + yw);
			m.m12 = 2.0 * (yz - xw);
			m.m22 = 1.0 - 2.0 * (xx + yy);
			m.m32 = 0.0;

			// Scale applies to columns (rotation * scale), same as Unity's Matrix4x4.TRS
			m.m00 *= scale.x;
			m.m10 *= scale.x;
			m.m20 *= scale.x;

			m.m01 *= scale.y;
			m.m11 *= scale.y;
			m.m21 *= scale.y;

			m.m02 *= scale.z;
			m.m12 *= scale.z;
			m.m22 *= scale.z;

			m.m03 = pos.x;
			m.m13 = pos.y;
			m.m23 = pos.z;
			m.m33 = 1.0;
			return m;
		}

		static public Matrix4x4D TRS (Vector3D pos, QuaternionD rot, double scale)
		{
			double xx, xy, xz, xw, yy, yz, yw, zz, zw;

			xx = rot.x * rot.x;
			xy = rot.x * rot.y;
			xz = rot.x * rot.z;
			xw = rot.x * rot.w;

			yy = rot.y * rot.y;
			yz = rot.y * rot.z;
			yw = rot.y * rot.w;

			zz = rot.z * rot.z;
			zw = rot.z * rot.w;

			Matrix4x4D m;
			m.m00 = 1.0 - 2.0 * (yy + zz);
			m.m10 = 2.0 * (xy + zw);
			m.m20 = 2.0 * (xz - yw);
			m.m30 = 0.0;

			m.m01 = 2.0 * (xy - zw);
			m.m11 = 1.0 - 2.0 * (xx + zz);
			m.m21 = 2.0 * (yz + xw);
			m.m31 = 0.0;

			m.m02 = 2.0 * (xz + yw);
			m.m12 = 2.0 * (yz - xw);
			m.m22 = 1.0 - 2.0 * (xx + yy);
			m.m32 = 0.0;

			m.m00 *= scale;
			m.m10 *= scale;
			m.m20 *= scale;

			m.m01 *= scale;
			m.m11 *= scale;
			m.m21 *= scale;

			m.m02 *= scale;
			m.m12 *= scale;
			m.m22 *= scale;

			m.m03 = pos.x;
			m.m13 = pos.y;
			m.m23 = pos.z;
			m.m33 = 1.0;
			return m;
		}

		static public Matrix4x4D TRS (Vector3D pos, double degreesY, double scale)
		{
			var halfRad = degreesY * 0.0087266462599722;
			var sinR = Math.Sin(halfRad);
			var cosR = Math.Cos(halfRad);

			var yy = cosR * cosR;
			var yw = cosR * sinR;

			Matrix4x4D m;
			m.m00 = -(1d - 2d * yy) * scale;
			m.m10 = 0d;
			m.m20 = -2d * yw * scale;
			m.m30 = 0d;

			m.m01 = 0d;
			m.m11 = scale;
			m.m21 = 0d;
			m.m31 = 0d;

			m.m02 = 2d * yw * scale;
			m.m12 = 0d;
			m.m22 = -(1d - 2d * yy) * scale;
			m.m32 = 0d;

			m.m03 = pos.x;
			m.m13 = pos.y;
			m.m23 = pos.z;
			m.m33 = 1d;
			return m;
		}

		static public Matrix4x4D TRS (Vector3D pos, QuaternionD rot)
		{
			double xx, xy, xz, xw, yy, yz, yw, zz, zw;

			xx = rot.x * rot.x;
			xy = rot.x * rot.y;
			xz = rot.x * rot.z;
			xw = rot.x * rot.w;

			yy = rot.y * rot.y;
			yz = rot.y * rot.z;
			yw = rot.y * rot.w;

			zz = rot.z * rot.z;
			zw = rot.z * rot.w;

			Matrix4x4D m;
			m.m00 = 1.0 - 2.0 * (yy + zz);
			m.m10 = 2.0 * (xy + zw);
			m.m20 = 2.0 * (xz - yw);
			m.m30 = 0.0;

			m.m01 = 2.0 * (xy - zw);
			m.m11 = 1.0 - 2.0 * (xx + zz);
			m.m21 = 2.0 * (yz + xw);
			m.m31 = 0.0;

			m.m02 = 2.0 * (xz + yw);
			m.m12 = 2.0 * (yz - xw);
			m.m22 = 1.0 - 2.0 * (xx + yy);
			m.m32 = 0.0;

			m.m03 = pos.x;
			m.m13 = pos.y;
			m.m23 = pos.z;
			m.m33 = 1.0;
			return m;
		}

		static public Matrix4x4D TRS (Vector3D pos)
		{
			Matrix4x4D m;
			m.m00 = 1.0;
			m.m10 = 0.0;
			m.m20 = 0.0;
			m.m30 = 0.0;

			m.m01 = 0.0;
			m.m11 = 1.0;
			m.m21 = 0.0;
			m.m31 = 0.0;

			m.m02 = 0.0;
			m.m12 = 0.0;
			m.m22 = 1.0;
			m.m32 = 0.0;

			m.m03 = pos.x;
			m.m13 = pos.y;
			m.m23 = pos.z;
			m.m33 = 1.0;
			return m;
		}

		static public Matrix4x4D TRS (Vector3 pos, Quaternion rot, Vector3 scale)
		{
			double xx, xy, xz, xw, yy, yz, yw, zz, zw;

			xx = rot.x * rot.x;
			xy = rot.x * rot.y;
			xz = rot.x * rot.z;
			xw = rot.x * rot.w;

			yy = rot.y * rot.y;
			yz = rot.y * rot.z;
			yw = rot.y * rot.w;

			zz = rot.z * rot.z;
			zw = rot.z * rot.w;

			Matrix4x4D m;
			m.m00 = 1f - 2.0f * (yy + zz);
			m.m10 = 2.0f * (xy + zw);
			m.m20 = 2.0f * (xz - yw);
			m.m30 = 0f;

			m.m01 = 2.0f * (xy - zw);
			m.m11 = 1f - 2.0f * (xx + zz);
			m.m21 = 2.0f * (yz + xw);
			m.m31 = 0f;

			m.m02 = 2.0f * (xz + yw);
			m.m12 = 2.0f * (yz - xw);
			m.m22 = 1f - 2.0f * (xx + yy);
			m.m32 = 0f;

			// Scale applies to columns (rotation * scale), same as Unity's Matrix4x4.TRS
			m.m00 *= scale.x;
			m.m10 *= scale.x;
			m.m20 *= scale.x;

			m.m01 *= scale.y;
			m.m11 *= scale.y;
			m.m21 *= scale.y;

			m.m02 *= scale.z;
			m.m12 *= scale.z;
			m.m22 *= scale.z;

			m.m03 = pos.x;
			m.m13 = pos.y;
			m.m23 = pos.z;
			m.m33 = 1f;
			return m;
		}

		static public Matrix4x4D TRS (Vector3D pos, Quaternion rot) { return TRS(pos, (QuaternionD)rot); }

		static public Matrix4x4D TRS (Vector3 pos, Quaternion rot)
		{
			double xx, xy, xz, xw, yy, yz, yw, zz, zw;

			xx = rot.x * rot.x;
			xy = rot.x * rot.y;
			xz = rot.x * rot.z;
			xw = rot.x * rot.w;

			yy = rot.y * rot.y;
			yz = rot.y * rot.z;
			yw = rot.y * rot.w;

			zz = rot.z * rot.z;
			zw = rot.z * rot.w;

			Matrix4x4D m;
			m.m00 = 1f - 2.0f * (yy + zz);
			m.m10 = 2.0f * (xy + zw);
			m.m20 = 2.0f * (xz - yw);
			m.m30 = 0f;

			m.m01 = 2.0f * (xy - zw);
			m.m11 = 1f - 2.0f * (xx + zz);
			m.m21 = 2.0f * (yz + xw);
			m.m31 = 0f;

			m.m02 = 2.0f * (xz + yw);
			m.m12 = 2.0f * (yz - xw);
			m.m22 = 1f - 2.0f * (xx + yy);
			m.m32 = 0f;

			m.m03 = pos.x;
			m.m13 = pos.y;
			m.m23 = pos.z;
			m.m33 = 1f;
			return m;
		}

		/// <summary>
		/// Serialize the object's data into binary format.
		/// </summary>

		public void Serialize (BinaryWriter writer)
		{
			writer.Write(m00);
			writer.Write(m01);
			writer.Write(m02);
			writer.Write(m03);
			writer.Write(m10);
			writer.Write(m11);
			writer.Write(m12);
			writer.Write(m13);
			writer.Write(m20);
			writer.Write(m21);
			writer.Write(m22);
			writer.Write(m23);
			writer.Write(m30);
			writer.Write(m31);
			writer.Write(m32);
			writer.Write(m33);
		}

		/// <summary>
		/// Deserialize the object's data from binary format.
		/// </summary>

		public void Deserialize (BinaryReader reader)
		{
			m00 = reader.ReadDouble();
			m01 = reader.ReadDouble();
			m02 = reader.ReadDouble();
			m03 = reader.ReadDouble();
			m10 = reader.ReadDouble();
			m11 = reader.ReadDouble();
			m12 = reader.ReadDouble();
			m13 = reader.ReadDouble();
			m20 = reader.ReadDouble();
			m21 = reader.ReadDouble();
			m22 = reader.ReadDouble();
			m23 = reader.ReadDouble();
			m30 = reader.ReadDouble();
			m31 = reader.ReadDouble();
			m32 = reader.ReadDouble();
			m33 = reader.ReadDouble();
		}
	}
}