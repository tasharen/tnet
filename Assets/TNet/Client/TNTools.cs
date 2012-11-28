//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using System.IO;
using System;

namespace TNet
{
/// <summary>
/// Common Tasharen Network-related functionality and helper functions.
/// </summary>

static public class Tools
{
	/// <summary>
	/// Write the array of objects into the specified writer.
	/// </summary>

	static public void Write (BinaryWriter bw, params object[] objs)
	{
		if (objs.Length < 255)
		{
			bw.Write((byte)objs.Length);
		}
		else
		{
			bw.Write((byte)255);
			bw.Write(objs.Length);
		}
		if (objs.Length == 0) return;

		for (int b = 0, bmax = objs.Length; b < bmax; ++b)
		{
			object obj = objs[b];
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
			else if (type == typeof(int))
			{
				bw.Write('c');
				bw.Write((int)obj);
			}
			else if (type == typeof(float))
			{
				bw.Write('d');
				bw.Write((float)obj);
			}
			else if (type == typeof(string))
			{
				bw.Write('e');
				bw.Write((string)obj);
			}
			else if (type == typeof(Vector2))
			{
				Vector2 v = (Vector2)obj;
				bw.Write('f');
				bw.Write(v.x);
				bw.Write(v.y);
			}
			else if (type == typeof(Vector3))
			{
				Vector3 v = (Vector3)obj;
				bw.Write('g');
				bw.Write(v.x);
				bw.Write(v.y);
				bw.Write(v.z);
			}
			else if (type == typeof(Vector4))
			{
				Vector4 v = (Vector4)obj;
				bw.Write('h');
				bw.Write(v.x);
				bw.Write(v.y);
				bw.Write(v.z);
				bw.Write(v.w);
			}
			else if (type == typeof(Quaternion))
			{
				Quaternion q = (Quaternion)obj;
				bw.Write('i');
				bw.Write(q.x);
				bw.Write(q.y);
				bw.Write(q.z);
				bw.Write(q.w);
			}
			else if (type == typeof(Color32))
			{
				Color32 c = (Color32)obj;
				bw.Write('j');
				bw.Write(c.r);
				bw.Write(c.g);
				bw.Write(c.b);
				bw.Write(c.a);
			}
			else if (type == typeof(Color))
			{
				Color c = (Color)obj;
				bw.Write('k');
				bw.Write(c.r);
				bw.Write(c.g);
				bw.Write(c.b);
				bw.Write(c.a);
			}
			else if (type == typeof(DateTime))
			{
				DateTime time = (DateTime)obj;
				bw.Write('l');
				bw.Write((Int64)time.Ticks);
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
			else if (type == typeof(int[]))
			{
				int[] arr = (int[])obj;
				bw.Write('C');
				bw.Write(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
			}
			else if (type == typeof(float[]))
			{
				float[] arr = (float[])obj;
				bw.Write('D');
				bw.Write(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
			}
			else if (type == typeof(string[]))
			{
				string[] arr = (string[])obj;
				bw.Write('E');
				bw.Write(arr.Length);
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
			}
			else
			{
				Debug.LogError("Unable to save type " + type);
			}
		}
	}

	/// <summary>
	/// Read the object array from the specified reader.
	/// </summary>

	static public object[] Read (BinaryReader reader)
	{
		int count = reader.ReadByte();
		if (count == 255) count = reader.ReadInt32();
		if (count == 0) return null;

		object[] data = new object[count];

		for (int i = 0; i < count; ++i)
		{
			char type = reader.ReadChar();

			switch (type)
			{
				case 'a': data[i] = reader.ReadBoolean(); break;
				case 'b': data[i] = reader.ReadByte(); break;
				case 'c': data[i] = reader.ReadInt32(); break;
				case 'd': data[i] = reader.ReadSingle(); break;
				case 'e': data[i] = reader.ReadString(); break;
				case 'f': data[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle()); break;
				case 'g': data[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); break;
				case 'h': data[i] = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); break;
				case 'i': data[i] = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); break;
				case 'j': data[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()); break;
				case 'k': data[i] = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); break;
				case 'l': data[i] = new DateTime(reader.ReadInt64()); break;
				case 'A':
				{
					int elements = reader.ReadInt32();
					bool[] arr = new bool[elements];
					for (int b = 0; b < elements; ++b) arr[b] = reader.ReadBoolean();
					data[i] = arr;
					break;
				}
				case 'B':
				{
					int elements = reader.ReadInt32();
					data[i] = reader.ReadBytes(elements);
					break;
				}
				case 'C':
				{
					int elements = reader.ReadInt32();
					int[] arr = new int[elements];
					for (int b = 0; b < elements; ++b) arr[b] = reader.ReadInt32();
					data[i] = arr;
					break;
				}
				case 'D':
				{
					int elements = reader.ReadInt32();
					float[] arr = new float[elements];
					for (int b = 0; b < elements; ++b) arr[b] = reader.ReadSingle();
					data[i] = arr;
					break;
				}
				case 'E':
				{
					int elements = reader.ReadInt32();
					string[] arr = new string[elements];
					for (int b = 0; b < elements; ++b) arr[b] = reader.ReadString();
					data[i] = arr;
					break;
				}
				default:
				{
					Debug.LogError("Reading of type '" + type + "' has not been implemented");
					break;
				}
			}
		}
		return data;
	}
}
}