//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2015 Tasharen Entertainment
//---------------------------------------------

#if UNITY_EDITOR || (!UNITY_FLASH && !NETFX_CORE && !UNITY_WP8 && !UNITY_WP_8_1)
#define REFLECTION_SUPPORT
#endif

//#define IGNORE_ERRORS

#if !STANDALONE
using UnityEngine;
#endif

using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System;

#if REFLECTION_SUPPORT
using System.Reflection;
using System.Globalization;
#endif

public struct Vector4i
{
	public int x;
	public int y;
	public int z;
	public int w;

	public Vector4i (int xx, int yy, int zz, int ww) { x = xx; y = yy; z = zz; w = ww; }
}

#if STANDALONE
public struct Vector2
{
	public float x;
	public float y;

	public Vector2 (float xx, float yy) { x = xx; y = yy; }
}

public struct Vector3
{
	public float x;
	public float y;
	public float z;

	public Vector3 (float xx, float yy, float zz) { x = xx; y = yy; z = zz; }
}

public struct Vector4
{
	public float x;
	public float y;
	public float z;
	public float w;

	public Vector4 (float xx, float yy, float zz, float ww) { x = xx; y = yy; z = zz; w = ww; }
}

public struct Quaternion
{
	public float x;
	public float y;
	public float z;
	public float w;

	public Quaternion (float xx, float yy, float zz, float ww) { x = xx; y = yy; z = zz; w = ww; }
}

public struct Rect
{
	public float x;
	public float y;
	public float width;
	public float height;

	public Rect (float xx, float yy, float ww, float hh) { x = xx; y = yy; width = ww; height = hh; }
}

public struct Color
{
	public float r;
	public float g;
	public float b;
	public float a;

	public Color (float rr, float gg, float bb, float aa = 0f) { r = rr; g = gg; b = bb; a = aa; }
}

public struct Color32
{
	public byte r;
	public byte g;
	public byte b;
	public byte a;

	public Color32 (byte rr, byte gg, byte bb, byte aa = 0) { r = rr; g = gg; b = bb; a = aa; }
}

public struct Matrix4x4
{
	public float m00;
	public float m10;
	public float m20;
	public float m30;

	public float m01;
	public float m11;
	public float m21;
	public float m31;

	public float m02;
	public float m12;
	public float m22;
	public float m32;

	public float m03;
	public float m13;
	public float m23;
	public float m33;
}

public struct BoneWeight
{
	public int boneIndex0;
	public int boneIndex1;
	public int boneIndex2;
	public int boneIndex3;

	public float weight0;
	public float weight1;
	public float weight2;
	public float weight3;
}

public struct Bounds
{
	public Vector3 center;
	public Vector3 size;

	public Bounds (Vector3 c, Vector3 s) { center = c; size = s; }
}
#endif

namespace TNet
{
/// <summary>
/// If custom or simply more efficient serialization is desired, derive your class from IBinarySerializable.
/// Ideal use case would be to reduce the amount of data sent over the network via RFCs.
/// </summary>

public interface IBinarySerializable
{
	/// <summary>
	/// Serialize the object's data into binary format.
	/// </summary>

	void Serialize (BinaryWriter writer);

	/// <summary>
	/// Deserialize the object's data from binary format.
	/// </summary>

	void Deserialize (BinaryReader reader);
}

/// <summary>
/// Obfuscated type integer. Usable just like any integer, but when it's in memory it's not recognizable.
/// Useful for avoiding CheatEngine lookups.
/// </summary>

public struct ObsInt
{
	public int obscured;
	public int revealed { get { return Restore(obscured); } set { obscured = Obfuscate(value); } }

	public ObsInt (int val) { obscured = Obfuscate(val); }

	static public implicit operator int (ObsInt o) { return Restore(o.obscured); }
	public override string ToString () { return Restore(obscured).ToString(); }

	const int mask1 = 0x00550055;
	const int d1 = 7;
	const int mask2 = 0x0000cccc;
	const int d2 = 14;

	static int Obfuscate (int x)
	{
		int t = (x ^ (x >> d1)) & mask1;
		int u = x ^ t ^ (t << d1);
		t = (u ^ (u >> d2)) & mask2;
		return u ^ t ^ (t << d2);
	}

	static int Restore (int y)
	{
		int t = (y ^ (y >> d2)) & mask2;
		int u = y ^ t ^ (t << d2);
		t = (u ^ (u >> d1)) & mask1;
		return u ^ t ^ (t << d1);
	}
}

/// <summary>
/// This class contains various serialization extension methods that make it easy to serialize
/// any object into binary form that's smaller in size than what you would get by simply using
/// the Binary Formatter. If you want more efficient serialization, implement IBinarySerializable.
/// </summary>

// Basic usage:
// binaryWriter.Write(data);
// binaryReader.Read<DataType>();

public static class Serialization
{
#if REFLECTION_SUPPORT
	/// <summary>
	/// Binary formatter, cached for convenience and performance (so it can be reused).
	/// </summary>
	static public BinaryFormatter formatter = new BinaryFormatter();
#endif

	static Dictionary<string, Type> mNameToType = new Dictionary<string, Type>();
	static Dictionary<Type, string> mTypeToName = new Dictionary<Type, string>();

	/// <summary>
	/// Given the type name in the string format, return its System.Type.
	/// </summary>

	static public Type NameToType (string name)
	{
		Type type = null;

		if (!mNameToType.TryGetValue(name, out type) || type == null)
		{
			if (name == "string[]") type = typeof(string[]);
			else if (name == "int[]") type = typeof(int[]);
			else if (name == "float[]") type = typeof(float[]);
			else if (name == "byte[]") type = typeof(byte[]);
			else if (name.EndsWith("[]"))
			{
				try
				{
#if STANDALONE
					type = Type.GetType(name.Substring(0, name.Length - 2));
#else
					type = UnityTools.FindType(name.Substring(0, name.Length - 2));
#endif
					if (type != null) type = type.MakeArrayType();
				}
				catch (Exception) { }
			}
			else if (name.StartsWith("IList"))
			{
				if (name.Length > 7 && name[5] == '<' && name[name.Length - 1] == '>')
				{
					Type elemType = NameToType(name.Substring(6, name.Length - 7));
					if (elemType != null) type = typeof(System.Collections.Generic.List<>).MakeGenericType(elemType);
				}
				else Tools.LogError("Malformed type: " + name);
			}
			else if (name.StartsWith("TList"))
			{
				if (name.Length > 7 && name[5] == '<' && name[name.Length - 1] == '>')
				{
					Type elemType = NameToType(name.Substring(6, name.Length - 7));
					if (elemType != null) type = typeof(TNet.List<>).MakeGenericType(elemType);
				}
				else Tools.LogError("Malformed type: " + name);
			}
			else
			{
				try
				{
#if STANDALONE
					type = Type.GetType(name);
#else
					type = UnityTools.FindType(name);
#endif
				}
				catch (Exception) { }
			}

#if UNITY_EDITOR
			if (type == null) UnityEngine.Debug.LogError("Unable to resolve type '" + name + "'");
#else
			if (type == null) Tools.LogError("Unable to resolve type '" + name + "'");
#endif
			mNameToType[name] = type;
		}
		return type;
	}

	/// <summary>
	/// Convert the specified type to its serialized string type.
	/// </summary>

	static public string TypeToName (Type type)
	{
		if (type == null)
		{
			Tools.LogError("Type cannot be null");
			return null;
		}
		string name;

		if (!mTypeToName.TryGetValue(type, out name) || name == null)
		{
			if (type == typeof(string[])) name = "string[]";
			else if (type == typeof(int[])) name = "int[]";
			else if (type == typeof(float[])) name = "float[]";
			else if (type == typeof(byte[])) name = "byte[]";
			else if (type.Implements(typeof(IList)))
			{
				Type arg = type.GetGenericArgument();

				if (arg != null)
				{
					string sub = arg.ToString().Replace("UnityEngine.", "");
					name = "IList<" + sub + ">";
				}
				else name = type.ToString().Replace("UnityEngine.", "");
			}
			else if (type.Implements(typeof(TList)))
			{
				Type arg = type.GetGenericArgument();

				if (arg != null)
				{
					string sub = arg.ToString().Replace("UnityEngine.", "");
					name = "TList<" + sub + ">";
				}
				else name = type.ToString().Replace("UnityEngine.", "");
			}
			else name = type.ToString().Replace("UnityEngine.", "");

			mTypeToName[type] = name;
		}
		return name;
	}

	static int Round (float val) { return (int)(val + 0.5f); } 

	/// <summary>
	/// Helper function to convert the specified value into the provided type.
	/// </summary>

#if STANDALONE
	static public object ConvertValue (object value, Type desiredType)
#else
	static public object ConvertValue (object value, Type desiredType, GameObject go = null)
#endif
	{
		if (value == null) return null;

		Type valueType = value.GetType();
		if (valueType == desiredType) return value;
		if (desiredType.IsAssignableFrom(valueType)) return value;

		if (desiredType == typeof(string))
		{
			return value.ToString();
		}
		else if (valueType == typeof(int))
		{
			// Integer conversion
			if (desiredType == typeof(byte)) return (byte)(int)value;
			if (desiredType == typeof(short)) return (short)(int)value;
			if (desiredType == typeof(ushort)) return (ushort)(int)value;
			if (desiredType == typeof(float)) return (float)(int)value;
			if (desiredType == typeof(long)) return (long)(int)value;
			if (desiredType == typeof(ulong)) return (ulong)(int)value;
			if (desiredType == typeof(UInt32)) return (UInt32)(int)value;
			if (desiredType == typeof(ObsInt)) return new ObsInt((int)value);
#if !STANDALONE
			if (desiredType == typeof(LayerMask)) return (LayerMask)(int)value;
#endif
		}
		else if (valueType == typeof(float))
		{
			// Float conversion
			if (desiredType == typeof(byte)) return (byte)Round((float)value);
			if (desiredType == typeof(short)) return (short)Round((float)value);
			if (desiredType == typeof(ushort)) return (ushort)Round((float)value);
			if (desiredType == typeof(int)) return Round((float)value);
		}
		else if (valueType == typeof(long))
		{
			if (desiredType == typeof(int)) return (int)(long)value;
			if (desiredType == typeof(ulong)) return (ulong)(long)value;
		}
		else if (valueType == typeof(ulong))
		{
			if (desiredType == typeof(int)) return (int)(ulong)value;
			if (desiredType == typeof(long)) return (long)(ulong)value;
		}
		else if (valueType == typeof(ObsInt))
		{
			if (desiredType == typeof(int)) return (int)(ObsInt)value;
		}
		else if (valueType == typeof(Color32))
		{
			if (desiredType == typeof(Color))
			{
				Color32 c = (Color32)value;
				return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
			}
		}
		else if (valueType == typeof(Vector3))
		{
			if (desiredType == typeof(Color))
			{
				Vector3 v = (Vector3)value;
				return new Color(v.x, v.y, v.z);
			}
#if !STANDALONE
			else if (desiredType == typeof(Quaternion))
			{
				return Quaternion.Euler((Vector3)value);
			}
#endif
		}
		else if (valueType == typeof(Color))
		{
			if (desiredType == typeof(Quaternion))
			{
				Color c = (Color)value;
				return new Quaternion(c.r, c.g, c.b, c.a);
			}
			else if (desiredType == typeof(Rect))
			{
				Color c = (Color)value;
				return new Rect(c.r, c.g, c.b, c.a);
			}
			else if (desiredType == typeof(Vector4))
			{
				Color c = (Color)value;
				return new Vector4(c.r, c.g, c.b, c.a);
			}
		}
#if !STANDALONE
		else if (valueType == typeof(Vector4[]) && desiredType == typeof(AnimationCurve))
		{
			Vector4[] vs = (Vector4[])value;
			AnimationCurve cv = new AnimationCurve();
			int keyCount = vs.Length;
			Keyframe[] kfs = new Keyframe[keyCount];

			for (int i = 0; i < keyCount; ++i)
			{
				Vector4 v = vs[i];
				kfs[i] = new Keyframe(v.x, v.y, v.z, v.w);
			}

			cv.keys = kfs;
			return cv;
		}
		else if (valueType == typeof(AnimationCurve) && desiredType == typeof(Vector4[]))
		{
			AnimationCurve av = (AnimationCurve)value;
			Keyframe[] kfs = av.keys;
			int keyCount = kfs.Length;
			Vector4[] vs = new Vector4[keyCount];

			for (int i = 0; i < keyCount; ++i)
			{
				Keyframe kf = kfs[i];
				vs[i] = new Vector4(kf.time, kf.value, kf.inTangent, kf.outTangent);
			}
			return vs;
		}
		else if (valueType == typeof(LayerMask) && desiredType == typeof(int)) return (int)(LayerMask)value;
		else if (valueType == typeof(string) && typeof(UnityEngine.Object).IsAssignableFrom(desiredType))
		{
			if (go != null) return go.StringToReference((string)value);
			else Debug.LogWarning("Game object reference is needed for a path-based reference");
		}
 #if REFLECTION_SUPPORT
		else if (valueType == typeof(string[]) && desiredType.IsArray)
		{
			System.Type elemType = desiredType.GetElementType();

			if (typeof(UnityEngine.Object).IsAssignableFrom(elemType))
			{
				string[] list = (string[])value;
				IList newList = desiredType.Create(list.Length) as IList;

				for (int i = 0, imax = list.Length; i < imax; ++i)
				{
					object obj = go.StringToReference(list[i]);
					if (go != null) newList[i] = Convert.ChangeType(obj, elemType);
					else Debug.LogWarning("Game object reference is needed for a path-based reference");
				}
				return newList;
			}
		}
		else if (valueType == typeof(string[]) && desiredType.IsGenericType)
		{
			System.Type elemType = desiredType.GetGenericArgument();

			if (typeof(UnityEngine.Object).IsAssignableFrom(elemType))
			{
				string[] list = (string[])value;
				Type arrType = typeof(System.Collections.Generic.List<>).MakeGenericType(elemType);
				IList newList = (IList)Activator.CreateInstance(arrType);

				for (int i = 0, imax = list.Length; i < imax; ++i)
				{
					if (go != null) newList.Add(go.StringToReference(list[i]));
					else Debug.LogWarning("Game object reference is needed for a path-based reference");
				}
				return newList;
			}
		}
 #endif
#endif

#if REFLECTION_SUPPORT
		if (desiredType.IsEnum)
		{
			if (valueType == typeof(Int32))
				return value;

			if (valueType == typeof(string))
			{
				string strVal = (string)value;

				if (!string.IsNullOrEmpty(strVal))
				{
					try
					{
						return System.Enum.Parse(desiredType, strVal);
					}
					catch (Exception) { }

					//string[] enumNames = Enum.GetNames(desiredType);
					//for (int i = 0; i < enumNames.Length; ++i)
					//    if (enumNames[i] == strVal)
					//        return Enum.GetValues(desiredType).GetValue(i);
				}
			}
		}
#endif
		Tools.LogError("Unable to convert " + valueType + " (" + value.ToString() + ") to " + desiredType);
		return null;
	}

#if !REFLECTION_SUPPORT
	/// <summary>
	/// Set the specified field's value using reflection.
	/// </summary>

	static public bool SetValue (this object obj, string name, object value)
	{
		Debug.LogError("Can't assign " + obj.GetType() + "." + name + " (reflection is not supported on this platform)");
		return false;
	}
#elif STANDALONE

	/// <summary>
	/// Sets the value of a field or property of specified name.
	/// </summary>

	static public bool SetValue (this object obj, string name, object value)
	{
		if (obj == null) return false;
		System.Type type = obj.GetType();

		PropertyInfo pro = type.GetSerializableProperty(name);

		if (pro != null)
		{
			try
			{
				object val = ConvertValue(value, pro.PropertyType);
				pro.SetValue(obj, val, null);
				return true;
			}
			catch (Exception ex)
			{
				Tools.LogError(ex.Message);
				return false;
			}
		}

		FieldInfo fi = type.GetSerializableField(name);

		if (fi != null)
		{
			try
			{
				object val = ConvertValue(value, fi.FieldType);
				fi.SetValue(obj, val);
			}
			catch (Exception ex)
			{
				Tools.LogError(ex.Message);
				return false;
			}
		}
		return false;
	}

#else // STANDALONE

	/// <summary>
	/// Sets the value of a field or property of specified name.
	/// </summary>

	static public bool SetValue (this object obj, string name, object value, GameObject go = null)
	{
		if (obj == null) return false;
		System.Type type = obj.GetType();

		FieldInfo fi = type.GetSerializableField(name);

		if (fi != null)
		{
 #if UNITY_EDITOR
			object val = ConvertValue(value, fi.FieldType, go);
			fi.SetValue(obj, val);
 #else
			try
			{
				object val = ConvertValue(value, fi.FieldType, go);
				fi.SetValue(obj, val);
			}
			catch (Exception ex)
			{
				Tools.LogError(ex.Message);
				return false;
			}
 #endif // UNITY_EDITOR
		}

		PropertyInfo pro = type.GetSerializableProperty(name);

		if (pro != null)
		{
 #if UNITY_EDITOR
			object val = ConvertValue(value, pro.PropertyType, go);
			pro.SetValue(obj, val, null);
			return true;
 #else
			try
			{
				object val = ConvertValue(value, pro.PropertyType, go);
				pro.SetValue(obj, val, null);
				return true;
			}
			catch (Exception ex)
			{
				Tools.LogError(ex.Message);
				return false;
			}
 #endif // UNITY_EDITOR
		}
		return false;
	}
#endif // STANDALONE

	/// <summary>
	/// Extension function that deserializes the specified object into the chosen file using binary format.
	/// Returns the size of the buffer written into the file.
	/// </summary>

	static public int Deserialize (this object obj, string path)
	{
		FileStream stream = File.Open(path, FileMode.Create);
		if (stream == null) return 0;
		BinaryWriter writer = new BinaryWriter(stream);
		writer.WriteObject(obj);
		int size = (int)stream.Position;
		writer.Close();
		return size;
	}

#region Write

	/// <summary>
	/// Write an integer value using the smallest number of bytes possible.
	/// </summary>

	static public void WriteInt (this BinaryWriter bw, int val)
	{
		if (val < 255 && val > -1)
		{
			bw.Write((byte)val);
		}
		else
		{
			bw.Write((byte)255);
			bw.Write(val);
		}
	}

	/// <summary>
	/// Write a value to the stream.
	/// </summary>

	static public void Write (this BinaryWriter writer, Vector2 v)
	{
		writer.Write(v.x);
		writer.Write(v.y);
	}

	/// <summary>
	/// Write a value to the stream.
	/// </summary>

	static public void Write (this BinaryWriter writer, Vector3 v)
	{
		writer.Write(v.x);
		writer.Write(v.y);
		writer.Write(v.z);
	}

	/// <summary>
	/// Write a value to the stream.
	/// </summary>

	static public void Write (this BinaryWriter writer, Vector4 v)
	{
		writer.Write(v.x);
		writer.Write(v.y);
		writer.Write(v.z);
		writer.Write(v.w);
	}

	/// <summary>
	/// Write a value to the stream.
	/// </summary>

	static public void Write (this BinaryWriter writer, Quaternion q)
	{
		writer.Write(q.x);
		writer.Write(q.y);
		writer.Write(q.z);
		writer.Write(q.w);
	}

	/// <summary>
	/// Write a value to the stream.
	/// </summary>

	static public void Write (this BinaryWriter writer, Color32 c)
	{
		writer.Write(c.r);
		writer.Write(c.g);
		writer.Write(c.b);
		writer.Write(c.a);
	}

	/// <summary>
	/// Write a value to the stream.
	/// </summary>

	static public void Write (this BinaryWriter writer, Color c)
	{
		writer.Write(c.r);
		writer.Write(c.g);
		writer.Write(c.b);
		writer.Write(c.a);
	}

	/// <summary>
	/// Write the node hierarchy to the binary writer (binary format).
	/// </summary>

	static public void Write (this BinaryWriter writer, DataNode node)
	{
		if (node == null || string.IsNullOrEmpty(node.name))
		{
			writer.Write("");
		}
		else
		{
			writer.Write(node.name);
			writer.WriteObject(node.value);
			writer.WriteInt(node.children.size);

			for (int i = 0, imax = node.children.size; i < imax; ++i)
				writer.Write(node.children[i]);
		}
	}

	/// <summary>
	/// Write a matrix value to the stream.
	/// </summary>

	static public void Write (this BinaryWriter writer, Matrix4x4 mat)
	{
		writer.Write(mat.m00);
		writer.Write(mat.m10);
		writer.Write(mat.m20);
		writer.Write(mat.m30);

		writer.Write(mat.m01);
		writer.Write(mat.m11);
		writer.Write(mat.m21);
		writer.Write(mat.m31);

		writer.Write(mat.m02);
		writer.Write(mat.m12);
		writer.Write(mat.m22);
		writer.Write(mat.m32);

		writer.Write(mat.m03);
		writer.Write(mat.m13);
		writer.Write(mat.m23);
		writer.Write(mat.m33);
	}

	/// <summary>
	/// Write a bone weight value to the stream.
	/// </summary>

	static public void Write (this BinaryWriter writer, BoneWeight w)
	{
		writer.Write((byte)w.boneIndex0);
		writer.Write((byte)w.boneIndex1);
		writer.Write((byte)w.boneIndex2);
		writer.Write((byte)w.boneIndex3);
		writer.Write(w.weight0);
		writer.Write(w.weight1);
		writer.Write(w.weight2);
		writer.Write(w.weight3);
	}

	/// <summary>
	/// Write a bone weight value to the stream.
	/// </summary>

	static public void Write (this BinaryWriter writer, Bounds b)
	{
		writer.Write(b.center);
		writer.Write(b.size);
	}

	/// <summary>
	/// Get the identifier prefix for the specified type.
	/// If this is not one of the common types, the returned value will be 254 if reflection is supported and 255 otherwise.
	/// </summary>

	static int GetPrefix (Type type)
	{
		if (type == typeof(bool)) return 1;
		if (type == typeof(byte)) return 2;
		if (type == typeof(ushort)) return 3;
		if (type == typeof(int)) return 4;
		if (type == typeof(uint)) return 5;
		if (type == typeof(float)) return 6;
		if (type == typeof(string)) return 7;
		if (type == typeof(Vector2)) return 8;
		if (type == typeof(Vector3)) return 9;
		if (type == typeof(Vector4)) return 10;
		if (type == typeof(Quaternion)) return 11;
		if (type == typeof(Color32)) return 12;
		if (type == typeof(Color)) return 13;
		if (type == typeof(DataNode)) return 14;
		if (type == typeof(double)) return 15;
		if (type == typeof(short)) return 16;
#if !STANDALONE
		if (type == typeof(TNObject)) return 17;
#endif
		if (type == typeof(long)) return 18;
		if (type == typeof(ulong)) return 19;
		if (type == typeof(ObsInt)) return 20;
		if (type == typeof(Matrix4x4)) return 21;
		if (type == typeof(BoneWeight)) return 22;
		if (type == typeof(Bounds)) return 23;

		if (type == typeof(bool[])) return 101;
		if (type == typeof(byte[])) return 102;
		if (type == typeof(ushort[])) return 103;
		if (type == typeof(int[])) return 104;
		if (type == typeof(uint[])) return 105;
		if (type == typeof(float[])) return 106;
		if (type == typeof(string[])) return 107;
		if (type == typeof(Vector2[])) return 108;
		if (type == typeof(Vector3[])) return 109;
		if (type == typeof(Vector4[])) return 110;
		if (type == typeof(Quaternion[])) return 111;
		if (type == typeof(Color32[])) return 112;
		if (type == typeof(Color[])) return 113;
		if (type == typeof(double[])) return 115;
		if (type == typeof(short[])) return 116;
#if !STANDALONE
		if (type == typeof(TNObject[])) return 117;
#endif
		if (type == typeof(long[])) return 118;
		if (type == typeof(ulong[])) return 119;
		if (type == typeof(ObsInt[])) return 120;
		if (type == typeof(Matrix4x4[])) return 121;
		if (type == typeof(BoneWeight[])) return 122;
		if (type == typeof(Bounds[])) return 123;

#if REFLECTION_SUPPORT
		return 254;
#else
		return 255;
#endif
	}

	/// <summary>
	/// Given the prefix identifier, return the associated type.
	/// </summary>

	static Type GetType (int prefix)
	{
		switch (prefix)
		{
			case 1: return typeof(bool);
			case 2: return typeof(byte);
			case 3: return typeof(ushort);
			case 4: return typeof(int);
			case 5: return typeof(uint);
			case 6: return typeof(float);
			case 7: return typeof(string);
			case 8: return typeof(Vector2);
			case 9: return typeof(Vector3);
			case 10: return typeof(Vector4);
			case 11: return typeof(Quaternion);
			case 12: return typeof(Color32);
			case 13: return typeof(Color);
			case 14: return typeof(DataNode);
			case 15: return typeof(double);
			case 16: return typeof(short);
#if !STANDALONE
			case 17: return typeof(TNObject);
#endif
			case 18: return typeof(long);
			case 19: return typeof(ulong);
			case 20: return typeof(ObsInt);

			case 101: return typeof(bool[]);
			case 102: return typeof(byte[]);
			case 103: return typeof(ushort[]);
			case 104: return typeof(int[]);
			case 105: return typeof(uint[]);
			case 106: return typeof(float[]);
			case 107: return typeof(string[]);
			case 108: return typeof(Vector2[]);
			case 109: return typeof(Vector3[]);
			case 110: return typeof(Vector4[]);
			case 111: return typeof(Quaternion[]);
			case 112: return typeof(Color32[]);
			case 113: return typeof(Color[]);
			case 115: return typeof(double[]);
			case 116: return typeof(short[]);
#if !STANDALONE
			case 117: return typeof(TNObject[]);
#endif
			case 118: return typeof(long[]);
			case 119: return typeof(ulong[]);
			case 120: return typeof(ObsInt[]);
		}
		return null;
	}

	/// <summary>
	/// Write the specified type to the binary writer.
	/// </summary>

	static public void Write (this BinaryWriter bw, Type type)
	{
		int prefix = GetPrefix(type);
		bw.Write((byte)prefix);
		if (prefix > 250) bw.Write(TypeToName(type));
	}

	/// <summary>
	/// Write the specified type to the binary writer.
	/// </summary>

	static public void Write (this BinaryWriter bw, int prefix, Type type)
	{
		bw.Write((byte)prefix);
		if (prefix > 250) bw.Write(TypeToName(type));
	}

	/// <summary>
	/// Write a float using invariant culture, trimming values close to 0 down to 0 for easier readability.
	/// </summary>

	[System.Diagnostics.DebuggerHidden]
	[System.Diagnostics.DebuggerStepThrough]
	static public void WriteFloat (this StreamWriter writer, float f)
	{
		writer.Write(((f > -0.0001f && f < 0.0001f) ? 0f : f).ToString(CultureInfo.InvariantCulture));
	}

	/// <summary>
	/// Write a single object to the binary writer.
	/// </summary>

	static public void WriteObject (this BinaryWriter bw, object obj) { bw.WriteObject(obj, 255, false, true); }

	/// <summary>
	/// Write a single object to the binary writer.
	/// </summary>

	static public void WriteObject (this BinaryWriter bw, object obj, bool useReflection) { bw.WriteObject(obj, 255, false, useReflection); }

	/// <summary>
	/// Write a single object to the binary writer.
	/// </summary>

	static void WriteObject (this BinaryWriter bw, object obj, int prefix, bool typeIsKnown, bool useReflection)
	{
		if (obj == null)
		{
			bw.Write((byte)0);
			return;
		}

#if !STANDALONE
		// AnimationCurve should be sent as an array of Vector4 values since it's not serializable on its own
		if (obj is AnimationCurve) obj = ConvertValue(obj, typeof(Vector4[]));
#endif
		// The object implements IBinarySerializable
		if (obj is IBinarySerializable)
		{
			if (!typeIsKnown) bw.Write(253, obj.GetType());
			(obj as IBinarySerializable).Serialize(bw);
			return;
		}

		Type type;

		if (!typeIsKnown)
		{
			type = obj.GetType();
			prefix = GetPrefix(type);
		}
		else type = GetType(prefix);

		// If this is a custom type, there is more work to be done
		if (prefix > 250)
 		{
#if !STANDALONE
			if (obj is GameObject)
			{
				Debug.LogError("It's not possible to send entire game objects as parameters because Unity has no consistent way to identify them.\n" +
					"Consider sending a path to the game object or its TNObject's ID instead.");
				bw.Write((byte)0);
				return;
			}

			if (obj is Component)
			{
				Debug.LogError("It's not possible to send components as parameters because Unity has no consistent way to identify them.");
				bw.Write((byte)0);
				return;
			}
#endif
			// If it's a TNet list, serialize all of its elements
			if (obj is TList)
			{
#if REFLECTION_SUPPORT
				if (useReflection)
				{
					Type elemType = type.GetGenericArgument();

					if (elemType != null)
					{
						TList list = obj as TList;

						// Determine the prefix for this type
						int elemPrefix = GetPrefix(elemType);
						bool sameType = true;

						// Make sure that all elements are of the same type
						for (int i = 0, imax = list.Count; i < imax; ++i)
						{
							object o = list.Get(i);

							if (o != null && elemType != o.GetType())
							{
								sameType = false;
								elemPrefix = 255;
								break;
							}
						}

						if (!typeIsKnown) bw.Write((byte)98);
						bw.Write(elemType);
						bw.Write((byte)(sameType ? 1 : 0));
						bw.WriteInt(list.Count);

						for (int i = 0, imax = list.Count; i < imax; ++i)
							bw.WriteObject(list.Get(i), elemPrefix, sameType, useReflection);
						return;
					}
				}
				if (!typeIsKnown) bw.Write((byte)255);
				formatter.Serialize(bw.BaseStream, obj);
#endif
				return;
			}

			// If it's a generic list, serialize all of its elements
			if (obj is IList)
			{
#if REFLECTION_SUPPORT
				if (useReflection)
				{
					Type elemType = type.GetGenericArgument();
					bool fixedSize = false;

					if (elemType == null)
					{
						elemType = type.GetElementType();
						fixedSize = (type != null);
					}

					if (fixedSize || elemType != null)
					{
						// Determine the prefix for this type
						int elemPrefix = GetPrefix(elemType);
						IList list = obj as IList;
						bool sameType = true;

						// Make sure that all elements are of the same type
						foreach (object o in list)
						{
							if (o != null && elemType != o.GetType())
							{
								sameType = false;
								elemPrefix = 255;
								break;
							}
						}

						if (!typeIsKnown) bw.Write(fixedSize ? (byte)100 : (byte)99);
						bw.Write(fixedSize ? type : elemType);
						bw.Write((byte)(sameType ? 1 : 0));
						bw.WriteInt(list.Count);

						foreach (object o in list) bw.WriteObject(o, elemPrefix, sameType, useReflection);
						return;
					}
				}
				if (!typeIsKnown) bw.Write((byte)255);
				formatter.Serialize(bw.BaseStream, obj);
#endif
				return;
			}
		}

		// Prefix is what identifies what type is going to follow
		if (!typeIsKnown) bw.Write(prefix, type);

		switch (prefix)
		{
			case 1: bw.Write((bool)obj); break;
			case 2: bw.Write((byte)obj); break;
			case 3: bw.Write((ushort)obj); break;
			case 4: bw.Write((int)obj); break;
			case 5: bw.Write((uint)obj); break;
			case 6: bw.Write((float)obj); break;
			case 7: bw.Write((string)obj); break;
			case 8: bw.Write((Vector2)obj); break;
			case 9: bw.Write((Vector3)obj); break;
			case 10: bw.Write((Vector4)obj); break;
			case 11: bw.Write((Quaternion)obj); break;
			case 12: bw.Write((Color32)obj); break;
			case 13: bw.Write((Color)obj); break;
			case 14: bw.Write((DataNode)obj); break;
			case 15: bw.Write((double)obj); break;
			case 16: bw.Write((short)obj); break;
#if !STANDALONE
			case 17: bw.Write((uint)(obj as TNObject).uid); break;
#endif
			case 18: bw.Write((long)obj); break;
			case 19: bw.Write((ulong)obj); break;
			case 20: bw.Write(((ObsInt)obj).obscured); break;
			case 21: bw.Write((Matrix4x4)obj); break;
			case 22: bw.Write((BoneWeight)obj); break;
			case 23: bw.Write((Bounds)obj); break;
			case 101:
			{
				bool[] arr = (bool[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 102:
			{
				byte[] arr = (byte[])obj;
				bw.WriteInt(arr.Length);
				bw.Write(arr);
				break;
			}
			case 103:
			{
				ushort[] arr = (ushort[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 104:
			{
				int[] arr = (int[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 105:
			{
				uint[] arr = (uint[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 106:
			{
				float[] arr = (float[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 107:
			{
				string[] arr = (string[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i] ?? "");
				break;
			}
			case 108:
			{
				Vector2[] arr = (Vector2[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 109:
			{
				Vector3[] arr = (Vector3[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 110:
			{
				Vector4[] arr = (Vector4[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 111:
			{
				Quaternion[] arr = (Quaternion[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 112:
			{
				Color32[] arr = (Color32[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 113:
			{
				Color[] arr = (Color[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 115:
			{
				double[] arr = (double[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 116:
			{
				short[] arr = (short[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
#if !STANDALONE
			case 117:
			{
				TNObject[] arr = (TNObject[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i)
					bw.Write((uint)arr[i].uid);
				break;
			}
#endif
			case 118:
			{
				long[] arr = (long[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 119:
			{
				ulong[] arr = (ulong[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 120:
			{
				ObsInt[] arr = (ObsInt[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i].obscured);
				break;
			}
			case 121:
			{
				Matrix4x4[] arr = (Matrix4x4[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 122:
			{
				BoneWeight[] arr = (BoneWeight[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 123:
			{
				Bounds[] arr = (Bounds[])obj;
				bw.WriteInt(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
				break;
			}
			case 254: // Serialization using Reflection
			{
#if REFLECTION_SUPPORT
				FilterFields(obj);
				bw.WriteInt(mFieldNames.size);

				for (int i = 0, imax = mFieldNames.size; i < imax; ++i)
				{
					bw.Write(mFieldNames[i]);
					bw.WriteObject(mFieldValues[i]);
				}
#else
				Debug.LogError("Reflection-based serialization is not supported on this platform.");
#endif
				break;
			}
			case 255: // Serialization using a Binary Formatter
			{
#if REFLECTION_SUPPORT
				formatter.Serialize(bw.BaseStream, obj);
#else
				Debug.LogError("Reflection-based serialization is not supported on this platform.");
#endif
				break;
			}
			default:
			{
#if !STANDALONE
				Debug.LogError("Prefix " + prefix + " is not supported");
#else
				Tools.LogError("Prefix " + prefix + " is not supported");
#endif
				break;
			}
		}
	}

#if REFLECTION_SUPPORT
	static List<string> mFieldNames = new List<string>();
	static List<object> mFieldValues = new List<object>();

	/// <summary>
	/// Helper function that retrieves all serializable fields on the specified object and filters them, removing those with null values.
	/// </summary>

	static void FilterFields (object obj)
	{
		Type type = obj.GetType();
		List<FieldInfo> fields = type.GetSerializableFields();

		mFieldNames.Clear();
		mFieldValues.Clear();

		for (int i = 0; i < fields.size; ++i)
		{
			FieldInfo f = fields[i];
			object val = f.GetValue(obj);

			if (val != null)
			{
				mFieldNames.Add(f.Name);
				mFieldValues.Add(val);
			}
		}
	}
#endif

#endregion
#region Read

	/// <summary>
	/// Read the previously saved integer value.
	/// </summary>

	static public int ReadInt (this BinaryReader reader)
	{
		int count = reader.ReadByte();
		if (count == 255) count = reader.ReadInt32();
		return count;
	}

	/// <summary>
	/// Read a value from the stream.
	/// </summary>

	static public Vector2 ReadVector2 (this BinaryReader reader)
	{
		float x = reader.ReadSingle();
		float y = reader.ReadSingle();
		if (float.IsNaN(x)) x = 0f;
		if (float.IsNaN(y)) y = 0f;
		return new Vector2(x, y);
	}

	/// <summary>
	/// Read a value from the stream.
	/// </summary>

	static public Vector3 ReadVector3 (this BinaryReader reader)
	{
		float x = reader.ReadSingle();
		float y = reader.ReadSingle();
		float z = reader.ReadSingle();
		if (float.IsNaN(x)) x = 0f;
		if (float.IsNaN(y)) y = 0f;
		if (float.IsNaN(z)) z = 0f;
		return new Vector3(x, y, z);
	}

	/// <summary>
	/// Read a value from the stream.
	/// </summary>

	static public Vector4 ReadVector4 (this BinaryReader reader)
	{
		float x = reader.ReadSingle();
		float y = reader.ReadSingle();
		float z = reader.ReadSingle();
		float w = reader.ReadSingle();
		if (float.IsNaN(x)) x = 0f;
		if (float.IsNaN(y)) y = 0f;
		if (float.IsNaN(z)) z = 0f;
		if (float.IsNaN(w)) w = 0f;
		return new Vector4(x, y, z, w);
	}

	/// <summary>
	/// Read a value from the stream.
	/// </summary>

	static public Quaternion ReadQuaternion (this BinaryReader reader)
	{
		float x = reader.ReadSingle();
		float y = reader.ReadSingle();
		float z = reader.ReadSingle();
		float w = reader.ReadSingle();
		if (float.IsNaN(x)) x = 0f;
		if (float.IsNaN(y)) y = 0f;
		if (float.IsNaN(z)) z = 0f;
		if (float.IsNaN(w)) w = 0f;
		return new Quaternion(x, y, z, w);
	}

	/// <summary>
	/// Read a value from the stream.
	/// </summary>

	static public Color32 ReadColor32 (this BinaryReader reader)
	{
		return new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
	}

	/// <summary>
	/// Read a value from the stream.
	/// </summary>

	static public Color ReadColor (this BinaryReader reader)
	{
		float x = reader.ReadSingle();
		float y = reader.ReadSingle();
		float z = reader.ReadSingle();
		float w = reader.ReadSingle();
		if (float.IsNaN(x)) x = 0f;
		if (float.IsNaN(y)) y = 0f;
		if (float.IsNaN(z)) z = 0f;
		if (float.IsNaN(w)) w = 0f;
		return new Color(x, y, z, w);
	}

	/// <summary>
	/// Read the node hierarchy from the binary reader (binary format).
	/// </summary>

	static public DataNode ReadDataNode (this BinaryReader reader)
	{
		string str = reader.ReadString();
		if (string.IsNullOrEmpty(str)) return null;
		DataNode node = new DataNode(str);

#if IGNORE_ERRORS
		try
		{
#endif
		node.value = reader.ReadObject();
		int count = reader.ReadInt();

		for (int i = 0; i < count; ++i)
		{
			DataNode dn = reader.ReadDataNode();
			if (dn != null) node.children.Add(dn);
		}
#if IGNORE_ERRORS
		}
		catch (Exception) {}
#endif
		return node;
	}

	/// <summary>
	/// Read a previously serialized matrix from the stream.
	/// </summary>

	static public Matrix4x4 ReadMatrix (this BinaryReader reader)
	{
		Matrix4x4 m = new Matrix4x4();
		m.m00 = reader.ReadSingle();
		m.m10 = reader.ReadSingle();
		m.m20 = reader.ReadSingle();
		m.m30 = reader.ReadSingle();

		m.m01 = reader.ReadSingle();
		m.m11 = reader.ReadSingle();
		m.m21 = reader.ReadSingle();
		m.m31 = reader.ReadSingle();

		m.m02 = reader.ReadSingle();
		m.m12 = reader.ReadSingle();
		m.m22 = reader.ReadSingle();
		m.m32 = reader.ReadSingle();

		m.m03 = reader.ReadSingle();
		m.m13 = reader.ReadSingle();
		m.m23 = reader.ReadSingle();
		m.m33 = reader.ReadSingle();
		return m;
	}

	/// <summary>
	/// Read a previously serialized bounds struct.
	/// </summary>

	static public Bounds ReadBounds (this BinaryReader reader)
	{
		Vector3 center = reader.ReadVector3();
		Vector3 size = reader.ReadVector3();
		return new Bounds(center, size);
	}

	/// <summary>
	/// Read a previously serialized bone weight from the stream.
	/// </summary>

	static public BoneWeight ReadBoneWeight (this BinaryReader reader)
	{
		BoneWeight w = new BoneWeight();
		w.boneIndex0 = reader.ReadByte();
		w.boneIndex1 = reader.ReadByte();
		w.boneIndex2 = reader.ReadByte();
		w.boneIndex3 = reader.ReadByte();
		w.weight0 = reader.ReadSingle();
		w.weight1 = reader.ReadSingle();
		w.weight2 = reader.ReadSingle();
		w.weight3 = reader.ReadSingle();
		return w;
	}

	/// <summary>
	/// Read a previously encoded type from the reader.
	/// </summary>

	static public Type ReadType (this BinaryReader reader)
	{
		int prefix = reader.ReadByte();
		return (prefix > 250) ? NameToType(reader.ReadString()) : GetType(prefix);
	}

	/// <summary>
	/// Read a previously encoded type from the reader.
	/// </summary>

	static public Type ReadType (this BinaryReader reader, out int prefix)
	{
		prefix = reader.ReadByte();
		return (prefix > 250) ? NameToType(reader.ReadString()) : GetType(prefix);
	}

	/// <summary>
	/// Read a single object from the binary reader and cast it to the chosen type.
	/// </summary>

	static public T ReadObject<T> (this BinaryReader reader)
	{
		object obj = ReadObject(reader);
		if (obj == null) return default(T);
		return (T)obj;
	}

	/// <summary>
	/// Read a single object from the binary reader.
	/// </summary>

	static public object ReadObject (this BinaryReader reader) { return reader.ReadObject(null, 0, null, false); }

	/// <summary>
	/// Read a single object from the binary reader.
	/// </summary>

	static public object ReadObject (this BinaryReader reader, object obj) { return reader.ReadObject(obj, 0, null, false); }

	/// <summary>
	/// Read a single object from the binary reader.
	/// </summary>

	static object ReadObject (this BinaryReader reader, object obj, int prefix, Type type, bool typeIsKnown)
	{
		if (!typeIsKnown) type = reader.ReadType(out prefix);
		if (type.Implements(typeof(IBinarySerializable))) prefix = 253;

#if IGNORE_ERRORS
		try
		{
#endif
		switch (prefix)
		{
			case 0: return null;
			case 1: return reader.ReadBoolean();
			case 2: return reader.ReadByte();
			case 3: return reader.ReadUInt16();
			case 4: return reader.ReadInt32();
			case 5: return reader.ReadUInt32();
			case 6:
			{
				float f = reader.ReadSingle();
				if (float.IsNaN(f)) f = 0f;
				return f;
			}
			case 7: return reader.ReadString();
			case 8: return reader.ReadVector2();
			case 9: return reader.ReadVector3();
			case 10: return reader.ReadVector4();
			case 11: return reader.ReadQuaternion();
			case 12: return reader.ReadColor32();
			case 13: return reader.ReadColor();
			case 14: return reader.ReadDataNode();
			case 15: return reader.ReadDouble();
			case 16: return reader.ReadInt16();
#if !STANDALONE
			case 17: return TNObject.Find(reader.ReadUInt32());
#endif
			case 18: return reader.ReadInt64();
			case 19: return reader.ReadUInt64();
			case 20:
			{
				ObsInt obs;
				obs.obscured = reader.ReadInt32();
				return obs;
			}
			case 21: return reader.ReadMatrix();
			case 22: return reader.ReadBoneWeight();
			case 23: return reader.ReadBounds();
			case 98: // TNet.List
			{
				type = reader.ReadType(out prefix);
				bool sameType = (reader.ReadByte() == 1);
				int elements = reader.ReadInt();
				TList arr = null;

				if (obj != null)
				{
					arr = (TList)obj;
				}
				else
				{
#if REFLECTION_SUPPORT
					Type arrType = typeof(TNet.List<>).MakeGenericType(type);
					arr = (TList)Activator.CreateInstance(arrType);
#else
					Debug.LogError("Reflection-based serialization is not supported on this platform");
#endif
				}

				for (int i = 0; i < elements; ++i)
				{
					object val = reader.ReadObject(null, prefix, type, sameType);
					if (arr != null) arr.Add(val);
				}
				return arr;
			}
			case 99: // System.Collections.Generic.List
			{
				type = reader.ReadType(out prefix);
				bool sameType = (reader.ReadByte() == 1);
				int elements = reader.ReadInt();
				IList arr = null;

				if (obj != null)
				{
					arr = (IList)obj;
				}
				else
				{
#if REFLECTION_SUPPORT
					Type arrType = typeof(System.Collections.Generic.List<>).MakeGenericType(type);
					arr = (IList)Activator.CreateInstance(arrType);
#else
					Debug.LogError("Reflection-based serialization is not supported on this platform");
#endif
				}

				for (int i = 0; i < elements; ++i)
				{
					object val = reader.ReadObject(null, prefix, type, sameType);
					if (arr != null) arr.Add(val);
				}
				return arr;
			}
			case 100: // Array
			{
#if REFLECTION_SUPPORT
				type = reader.ReadType(out prefix);
				bool sameType = (reader.ReadByte() == 1);
				int elements = reader.ReadInt();

				IList arr = null;
				object created = null;

				try
				{
					created = type.Create(elements);
					arr = (IList)created;
				}
				catch (Exception ex)
				{
					Tools.LogError(ex.Message + "\n" + "Expected: " + type + "[" + elements + "]\n" +
						"Created: " + (created != null ? created.GetType().ToString() : "<null>"));
				}

				if (arr != null)
				{
					type = type.GetElementType();
					prefix = GetPrefix(type);
					for (int i = 0; i < elements; ++i)
						arr[i] = reader.ReadObject(null, prefix, type, sameType);
				}
				else Tools.LogError("Failed to create a " + type);
				return arr;
#else
				Tools.LogError("Reflection is not supported on this platform");
				return null;
#endif
			}
			case 101:
			{
				int elements = reader.ReadInt();
				bool[] arr = new bool[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadBoolean();
				return arr;
			}
			case 102:
			{
				int elements = reader.ReadInt();
				return reader.ReadBytes(elements);
			}
			case 103:
			{
				int elements = reader.ReadInt();
				ushort[] arr = new ushort[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadUInt16();
				return arr;
			}
			case 104:
			{
				int elements = reader.ReadInt();
				int[] arr = new int[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadInt32();
				return arr;
			}
			case 105:
			{
				int elements = reader.ReadInt();
				uint[] arr = new uint[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadUInt32();
				return arr;
			}
			case 106:
			{
				int elements = reader.ReadInt();
				float[] arr = new float[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadSingle();
				return arr;
			}
			case 107:
			{
				int elements = reader.ReadInt();
				string[] arr = new string[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadString();
				return arr;
			}
			case 108:
			{
				int elements = reader.ReadInt();
				Vector2[] arr = new Vector2[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadVector2();
				return arr;
			}
			case 109:
			{
				int elements = reader.ReadInt();
				Vector3[] arr = new Vector3[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadVector3();
				return arr;
			}
			case 110:
			{
				int elements = reader.ReadInt();
				Vector4[] arr = new Vector4[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadVector4();
				return arr;
			}
			case 111:
			{
				int elements = reader.ReadInt();
				Quaternion[] arr = new Quaternion[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadQuaternion();
				return arr;
			}
			case 112:
			{
				int elements = reader.ReadInt();
				Color32[] arr = new Color32[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadColor32();
				return arr;
			}
			case 113:
			{
				int elements = reader.ReadInt();
				Color[] arr = new Color[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadColor();
				return arr;
			}
			case 115:
			{
				int elements = reader.ReadInt();
				double[] arr = new double[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadDouble();
				return arr;
			}
			case 116:
			{
				int elements = reader.ReadInt();
				short[] arr = new short[elements];
				for (int b = 0; b < elements; ++b)
					arr[b] = reader.ReadInt16();
				return arr;
			}
#if !STANDALONE
			case 117:
			{
				int elements = reader.ReadInt();
				TNObject[] arr = new TNObject[elements];
				for (int b = 0; b < elements; ++b) arr[b] = TNObject.Find(reader.ReadUInt32());
				return arr;
			}
#endif
			case 118:
			{
				int elements = reader.ReadInt();
				long[] arr = new long[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadInt64();
				return arr;
			}
			case 119:
			{
				int elements = reader.ReadInt();
				ulong[] arr = new ulong[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadUInt64();
				return arr;
			}
			case 120:
			{
				int elements = reader.ReadInt();
				ObsInt[] arr = new ObsInt[elements];
				for (int b = 0; b < elements; ++b) arr[b].obscured = reader.ReadInt32();
				return arr;
			}
			case 121:
			{
				int elements = reader.ReadInt();
				Matrix4x4[] arr = new Matrix4x4[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadMatrix();
				return arr;
			}
			case 122:
			{
				int elements = reader.ReadInt();
				BoneWeight[] arr = new BoneWeight[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadBoneWeight();
				return arr;
			}
			case 123:
			{
				int elements = reader.ReadInt();
				Bounds[] arr = new Bounds[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadBounds();
				return arr;
			}
			case 253:
			{
				IBinarySerializable ser = (obj != null) ? (IBinarySerializable)obj : (IBinarySerializable)type.Create();
				if (ser != null) ser.Deserialize(reader);
				return ser;
			}
			case 254: // Serialization using Reflection
			{
#if REFLECTION_SUPPORT
				// Create the object
				if (obj == null)
				{
					obj = type.Create();
					if (obj == null) Tools.LogError("Unable to create an instance of " + type);
				}

				if (obj != null)
				{
					// How many fields have been serialized?
					int count = ReadInt(reader);

					for (int i = 0; i < count; ++i)
					{
						// Read the name of the field
						string fieldName = reader.ReadString();

						if (string.IsNullOrEmpty(fieldName))
						{
							Tools.LogError("Null field specified when serializing " + type);
							continue;
						}

						// Read the value
						obj.SetValue(fieldName, reader.ReadObject());
					}
				}
				return obj;
#else
				Debug.LogError("Reflection-based serialization is not supported on this platform");
				return null;
#endif
			}
			case 255: // Serialization using a Binary Formatter
			{
#if REFLECTION_SUPPORT
				return formatter.Deserialize(reader.BaseStream);
#else
				Debug.LogError("Reflection-based serialization is not supported on this platform.");
				return null;
#endif
			}
			default:
			{
				Tools.LogError("Unknown prefix: " + prefix + " at position " + reader.BaseStream.Position);
				return null;
			}
		}
#if IGNORE_ERRORS
		}
		catch (Exception ex)
		{
			Tools.LogError(ex.Message + " at position " + reader.BaseStream.Position);
		}
		return null;
#endif
	}
#endregion
}
}
