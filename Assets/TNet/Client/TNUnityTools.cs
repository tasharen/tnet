//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2013 Tasharen Entertainment
//---------------------------------------------

using UnityEngine;
using System.IO;
using System;
using System.Reflection;
using System.Net;

namespace TNet
{
/// <summary>
/// Common Tasharen Network-related functionality and helper functions to be used with Unity.
/// </summary>

static public class UnityTools
{
	/// <summary>
	/// Call the specified function on all the scripts. It's an expensive function, so use sparingly.
	/// </summary>

	static public void Broadcast (string methodName, params object[] parameters)
	{
		MonoBehaviour[] mbs = UnityEngine.Object.FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];

		for (int i = 0, imax = mbs.Length; i < imax; ++i)
		{
			MonoBehaviour mb = mbs[i];
			MethodInfo method = mb.GetType().GetMethod(methodName,
				BindingFlags.Instance |
				BindingFlags.NonPublic |
				BindingFlags.Public);

			if (method != null)
			{
#if UNITY_EDITOR
				try
				{
					method.Invoke(mb, parameters);
				}
				catch (System.Exception ex)
				{
					Debug.LogError(ex.Message + " (" + mb.GetType() + "." + methodName + ")", mb);
				}
#else
				method.Invoke(mb, parameters);
#endif
			}
		}
	}

	/// <summary>
	/// Returns whether the specified type can be serialized.
	/// </summary>

	static public bool CanBeSerialized (Type type)
	{
		if (type == typeof(bool)) return true;
		if (type == typeof(byte)) return true;
		if (type == typeof(ushort)) return true;
		if (type == typeof(int)) return true;
		if (type == typeof(uint)) return true;
		if (type == typeof(float)) return true;
		if (type == typeof(string)) return true;
		if (type == typeof(Vector2)) return true;
		if (type == typeof(Vector3)) return true;
		if (type == typeof(Vector4)) return true;
		if (type == typeof(Quaternion)) return true;
		if (type == typeof(Color32)) return true;
		if (type == typeof(Color)) return true;
		if (type == typeof(DateTime)) return true;
		if (type == typeof(IPEndPoint)) return true;
		if (type == typeof(bool[])) return true;
		if (type == typeof(byte[])) return true;
		if (type == typeof(ushort[])) return true;
		if (type == typeof(int[])) return true;
		if (type == typeof(uint[])) return true;
		if (type == typeof(float[])) return true;
		if (type == typeof(string[])) return true;
		return false;
	}

	/// <summary>
	/// Write the array of objects into the specified writer.
	/// </summary>

	static public void Write (BinaryWriter bw, params object[] objs)
	{
		WriteInt(bw, objs.Length);
		if (objs.Length == 0) return;

		for (int b = 0, bmax = objs.Length; b < bmax; ++b)
		{
			object obj = objs[b];

			if (obj != null && !WriteObject(bw, obj))
			{
				Debug.LogError("Unable to write type " + obj.GetType());
			}
		}
	}

	/// <summary>
	/// Write an integer value using the smallest number of bytes possible.
	/// </summary>

	static public void WriteInt (BinaryWriter bw, int val)
	{
		if (val < 255)
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
	/// Write a single object to the binary writer.
	/// </summary>

	static public bool WriteObject (BinaryWriter bw, object obj)
	{
		System.Type type = obj.GetType();

		if (type == typeof(bool))
		{
			bw.Write('a');
			bw.Write((bool)obj);
		}
		else if (type == typeof(byte))
		{
			bw.Write('b');
			bw.Write((byte)obj);
		}
		else if (type == typeof(ushort))
		{
			bw.Write('c');
			bw.Write((ushort)obj);
		}
		else if (type == typeof(int))
		{
			bw.Write('d');
			bw.Write((int)obj);
		}
		else if (type == typeof(uint))
		{
			bw.Write('e');
			bw.Write((uint)obj);
		}
		else if (type == typeof(float))
		{
			bw.Write('f');
			bw.Write((float)obj);
		}
		else if (type == typeof(string))
		{
			bw.Write('g');
			bw.Write((string)obj);
		}
		else if (type == typeof(Vector2))
		{
			Vector2 v = (Vector2)obj;
			bw.Write('h');
			bw.Write(v.x);
			bw.Write(v.y);
		}
		else if (type == typeof(Vector3))
		{
			Vector3 v = (Vector3)obj;
			bw.Write('i');
			bw.Write(v.x);
			bw.Write(v.y);
			bw.Write(v.z);
		}
		else if (type == typeof(Vector4))
		{
			Vector4 v = (Vector4)obj;
			bw.Write('j');
			bw.Write(v.x);
			bw.Write(v.y);
			bw.Write(v.z);
			bw.Write(v.w);
		}
		else if (type == typeof(Quaternion))
		{
			Quaternion q = (Quaternion)obj;
			bw.Write('k');
			bw.Write(q.x);
			bw.Write(q.y);
			bw.Write(q.z);
			bw.Write(q.w);
		}
		else if (type == typeof(Color32))
		{
			Color32 c = (Color32)obj;
			bw.Write('l');
			bw.Write(c.r);
			bw.Write(c.g);
			bw.Write(c.b);
			bw.Write(c.a);
		}
		else if (type == typeof(Color))
		{
			Color c = (Color)obj;
			bw.Write('m');
			bw.Write(c.r);
			bw.Write(c.g);
			bw.Write(c.b);
			bw.Write(c.a);
		}
		else if (type == typeof(DateTime))
		{
			DateTime time = (DateTime)obj;
			bw.Write('n');
			bw.Write((Int64)time.Ticks);
		}
		else if (type == typeof(IPEndPoint))
		{
			IPEndPoint ip = (IPEndPoint)obj;
			byte[] bytes = ip.Address.GetAddressBytes();
			bw.Write('o');
			bw.Write((byte)bytes.Length);
			bw.Write(bytes);
			bw.Write((ushort)ip.Port);
		}
		else if (type == typeof(bool[]))
		{
			bool[] arr = (bool[])obj;
			bw.Write('A');
			bw.Write(arr.Length);
			for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
		}
		else if (type == typeof(byte[]))
		{
			byte[] arr = (byte[])obj;
			bw.Write('B');
			bw.Write(arr.Length);
			bw.Write(arr);
		}
		else if (type == typeof(ushort[]))
		{
			ushort[] arr = (ushort[])obj;
			bw.Write('C');
			bw.Write(arr.Length);
			for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
		}
		else if (type == typeof(int[]))
		{
			int[] arr = (int[])obj;
			bw.Write('D');
			bw.Write(arr.Length);
			for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
		}
		else if (type == typeof(uint[]))
		{
			uint[] arr = (uint[])obj;
			bw.Write('E');
			bw.Write(arr.Length);
			for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
		}
		else if (type == typeof(float[]))
		{
			float[] arr = (float[])obj;
			bw.Write('F');
			bw.Write(arr.Length);
			for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
		}
		else if (type == typeof(string[]))
		{
			string[] arr = (string[])obj;
			bw.Write('G');
			bw.Write(arr.Length);
			for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
		}
		else
		{
			bw.Write('0');
			return false;
		}
		return true;
	}

	/// <summary>
	/// Read the object array from the specified reader.
	/// </summary>

	static public object[] Read (BinaryReader reader)
	{
		int count = ReadInt(reader);
		if (count == 0) return null;

		object[] data = new object[count];

		for (int i = 0; i < count; ++i)
		{
			data[i] = ReadObject(reader);
		}
		return data;
	}

	/// <summary>
	/// Read the previously saved integer value.
	/// </summary>

	static public int ReadInt (BinaryReader reader)
	{
		int count = reader.ReadByte();
		if (count == 255) count = reader.ReadInt32();
		return count;
	}

	/// <summary>
	/// Read a single object from the binary reader.
	/// </summary>

	static public object ReadObject (BinaryReader reader)
	{
		object obj = null;
		char type = reader.ReadChar();

		switch (type)
		{
			case 'a': obj = reader.ReadBoolean(); break;
			case 'b': obj = reader.ReadByte(); break;
			case 'c': obj = reader.ReadUInt16(); break;
			case 'd': obj = reader.ReadInt32(); break;
			case 'e': obj = reader.ReadUInt32(); break;
			case 'f': obj = reader.ReadSingle(); break;
			case 'g': obj = reader.ReadString(); break;
			case 'h': obj = new Vector2(reader.ReadSingle(), reader.ReadSingle()); break;
			case 'i': obj = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); break;
			case 'j': obj = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); break;
			case 'k': obj = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); break;
			case 'l': obj = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()); break;
			case 'm': obj = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); break;
			case 'n': obj = new DateTime(reader.ReadInt64()); break;
			case 'o':
			{
				byte[] bytes = reader.ReadBytes(reader.ReadByte());
				IPEndPoint ip = new IPEndPoint(new IPAddress(bytes), reader.ReadUInt16());
				obj = ip;
				break;
			}
			case 'A':
			{
				int elements = reader.ReadInt32();
				bool[] arr = new bool[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadBoolean();
				obj = arr;
				break;
			}
			case 'B':
			{
				int elements = reader.ReadInt32();
				obj = reader.ReadBytes(elements);
				break;
			}
			case 'C':
			{
				int elements = reader.ReadInt32();
				ushort[] arr = new ushort[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadUInt16();
				obj = arr;
				break;
			}
			case 'D':
			{
				int elements = reader.ReadInt32();
				int[] arr = new int[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadInt32();
				obj = arr;
				break;
			}
			case 'E':
			{
				int elements = reader.ReadInt32();
				uint[] arr = new uint[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadUInt32();
				obj = arr;
				break;
			}
			case 'F':
			{
				int elements = reader.ReadInt32();
				float[] arr = new float[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadSingle();
				obj = arr;
				break;
			}
			case 'G':
			{
				int elements = reader.ReadInt32();
				string[] arr = new string[elements];
				for (int b = 0; b < elements; ++b) arr[b] = reader.ReadString();
				obj = arr;
				break;
			}
			//default:
			//{
			//    Debug.LogError("Reading of type '" + type + "' has not been implemented");
			//    break;
			//}
		}
		return obj;
	}

	/// <summary>
	/// Mathf.Lerp(from, to, Time.deltaTime * strength) is not framerate-independent. This function is.
	/// </summary>

	static public float SpringLerp (float from, float to, float strength, float deltaTime)
	{
		if (deltaTime > 1f) deltaTime = 1f;
		int ms = Mathf.RoundToInt(deltaTime * 1000f);
		deltaTime = 0.001f * strength;
		for (int i = 0; i < ms; ++i) from = Mathf.Lerp(from, to, deltaTime);
		return from;
	}

	/// <summary>
	/// Pad the specified rectangle, returning an enlarged rectangle.
	/// </summary>

	static public Rect PadRect (Rect rect, float padding)
	{
		Rect r = rect;
		r.xMin -= padding;
		r.xMax += padding;
		r.yMin -= padding;
		r.yMax += padding;
		return r;
	}

	/// <summary>
	/// Whether the specified game object is a child of the specified parent.
	/// </summary>

	static public bool IsParentChild (GameObject parent, GameObject child)
	{
		if (parent == null || child == null) return false;
		return IsParentChild(parent.transform, child.transform);
	}

	/// <summary>
	/// Whether the specified transform is a child of the specified parent.
	/// </summary>

	static public bool IsParentChild (Transform parent, Transform child)
	{
		if (parent == null || child == null) return false;

		while (child != null)
		{
			if (parent == child) return true;
			child = child.parent;
		}
		return false;
	}
}
}
