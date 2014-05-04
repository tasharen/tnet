//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2014 Tasharen Entertainment
//---------------------------------------------

#if UNITY_EDITOR || (!UNITY_FLASH && !NETFX_CORE && !UNITY_WP8)
#define REFLECTION_SUPPORT
#endif

using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System;

#if REFLECTION_SUPPORT
using System.Reflection;
#endif

namespace TNet
{
/// <summary>
/// If custom or simply more efficient serialization is desired, derive your class from IBinarySerializable.
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
/// This class contains various serialization extension methods that make it easy to serialize
/// any object into binary form that's smaller in size than what you would get by simply using
/// the Binary Formatter.
/// 
/// Basic usage:
/// binaryWriter.Write(data);
/// binaryReader.Read<DataType>();
/// </summary>

public static class Serialization
{
	/// <summary>
	/// Binary formatter, cached for convenience and performance (so it can be reused).
	/// </summary>

	static public BinaryFormatter formatter = new BinaryFormatter();

	static Dictionary<string, Type> mNameToType = new Dictionary<string, Type>();
	static Dictionary<Type, string> mTypeToName = new Dictionary<Type, string>();

	/// <summary>
	/// Given the type name in the string format, return its System.Type.
	/// </summary>

	static public Type NameToType (string name)
	{
		Type type;

		if (!mNameToType.TryGetValue(name, out type))
		{
			if (name == "Vector2") type = typeof(Vector2);
			else if (name == "Vector3") type = typeof(Vector3);
			else if (name == "Vector4") type = typeof(Vector4);
			else if (name == "Quaternion") type = typeof(Quaternion);
			else if (name == "Rect") type = typeof(Rect);
			else if (name == "Color") type = typeof(Color);
			else if (name == "Color32") type = typeof(Color32);
			else type = Type.GetType(name);
			mNameToType[name] = type;
		}
		return type;
	}

	/// <summary>
	/// Convert the specified type to its serialized string type.
	/// </summary>

	static public string TypeToName (Type type)
	{
		string name;

		if (!mTypeToName.TryGetValue(type, out name))
		{
			if (type == typeof(Vector2)) name = "Vector2";
			else if (type == typeof(Vector3)) name = "Vector3";
			else if (type == typeof(Vector4)) name = "Vector4";
			else if (type == typeof(Quaternion)) name = "Quaternion";
			else if (type == typeof(Rect)) name = "Rect";
			else if (type == typeof(Color)) name = "Color";
			else if (type == typeof(Color32)) name = "Color32";
			else name = type.ToString();
			mTypeToName[type] = name;
		}
		return name;
	}

#if REFLECTION_SUPPORT
	static Dictionary<Type, List<FieldInfo>> mFieldDict = new Dictionary<Type, List<FieldInfo>>();

	/// <summary>
	/// Collect all serializable fields on the class of specified type.
	/// </summary>

	static public List<FieldInfo> GetSerializableFields (this Type type)
	{
		List<FieldInfo> list;

		if (!mFieldDict.TryGetValue(type, out list))
		{
			list = new List<FieldInfo>();
			FieldInfo[] fields = type.GetFields();

			bool serializable = type.IsDefined(typeof(System.SerializableAttribute), true);

			for (int i = 0, imax = fields.Length; i < imax; ++i)
			{
				FieldInfo field = fields[i];

				// Don't do anything with static fields
				if ((field.Attributes & FieldAttributes.Static) != 0) continue;

				// Ignore fields that were not marked as serializable
				if (!field.IsDefined(typeof(SerializeField), true))
				{
					// Class is not serializable
					if (!serializable) continue;

					// It's not a public field
					if ((field.Attributes & FieldAttributes.Public) == 0) continue;
				}

				// Ignore fields that were marked as non-serializable
				if (field.IsDefined(typeof(System.NonSerializedAttribute), true)) continue;

				// It's a valid serialiable field
				list.Add(field);
			}
			mFieldDict[type] = list;
		}
		return list;
	}

	/// <summary>
	/// Retrieve the specified serializable field from the type. Returns 'null' if the field was not found or if it's not serializable.
	/// </summary>

	static public FieldInfo GetSerializableField (this Type type, string name)
	{
		List<FieldInfo> list = type.GetSerializableFields();

		for (int i = 0, imax = list.size; i < imax; ++i)
		{
			FieldInfo field = list[i];
			if (field.Name == name) return field;
		}
		return null;
	}
#endif
#region Write
	/// <summary>
	/// Write an integer value using the smallest number of bytes possible.
	/// </summary>

	static public void WriteInt (this BinaryWriter bw, int val)
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
		writer.Write(node.name);
		writer.WriteObject(node.value);
		writer.WriteInt(node.children.size);

		for (int i = 0, imax = node.children.size; i < imax; ++i)
			writer.Write(node.children[i]);
	}

	/// <summary>
	/// Get the identifier prefix for the specified type.
	/// If this is not one of the common types, the returned value will be 253 if this type derives from IBinarySerializable,
	/// 254 if reflection is supported and 255 otherwise.
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

			case 101: return typeof(bool);
			case 102: return typeof(byte);
			case 103: return typeof(ushort);
			case 104: return typeof(int);
			case 105: return typeof(uint);
			case 106: return typeof(float);
			case 107: return typeof(string);
			case 108: return typeof(Vector2);
			case 109: return typeof(Vector3);
			case 110: return typeof(Vector4);
			case 111: return typeof(Quaternion);
			case 112: return typeof(Color32);
			case 113: return typeof(Color);
		}
		return null;
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

		// The object implements IBinarySerializable
		if (obj is IBinarySerializable)
		{
			bw.Write((byte)253);
			if (!typeIsKnown) bw.Write(TypeToName(obj.GetType()));
			(obj as IBinarySerializable).Serialize(bw);
			return;
		}

		// If it's a TNet list, serialize all of its elements
		if (obj is TList)
		{
#if REFLECTION_SUPPORT
			if (useReflection)
			{
				Type type = obj.GetType();
				TList list = obj as TList;
				if (!typeIsKnown) bw.Write((byte)99);

				// Determine the prefix for this type
				Type elemType = type.GetGenericArguments()[0];
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

				bw.Write((byte)elemPrefix);
				bw.Write(TypeToName(elemType));
				bw.Write((byte)(sameType ? 1 : 0));
				bw.WriteInt(list.Count);

				for (int i = 0, imax = list.Count; i < imax; ++i)
					bw.WriteObject(list.Get(i), elemPrefix, sameType, useReflection);
				return;
			}
#endif
			if (!typeIsKnown) bw.Write((byte)255);
			formatter.Serialize(bw.BaseStream, obj);
			return;
		}

		// If it's a generic list, serialize all of its elements
		if (obj is IList)
		{
#if REFLECTION_SUPPORT
			if (useReflection)
			{
				Type type = obj.GetType();
				Type[] types = type.GetGenericArguments();

				if (types.Length == 1)
				{
					IList list = obj as IList;
					if (!typeIsKnown) bw.Write((byte)100);

					// It's a simple list with just one argument
					Type elemType = types[0];

					// Determine the prefix for this type
					int elemPrefix = GetPrefix(elemType);
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

					bw.Write((byte)elemPrefix);
					bw.Write(TypeToName(elemType));
					bw.Write((byte)(sameType ? 1 : 0));
					bw.WriteInt(list.Count);

					foreach (object o in list) bw.WriteObject(o, elemPrefix, sameType, useReflection);
					return;
				}
			}
#endif
			if (!typeIsKnown) bw.Write((byte)255);
			formatter.Serialize(bw.BaseStream, obj);
			return;
		}

		// Prefix is what identifies what type is going to follow
		if (!typeIsKnown)
		{
			Type type = obj.GetType();
			prefix = GetPrefix(type);
			bw.Write((byte)prefix);

			// If this is not a common type then we need to write down what type it is
			if (prefix > 250) bw.Write(TypeToName(type));
		}

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
				for (int i = 0, imax = arr.Length; i < imax; ++i) bw.Write(arr[i]);
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
#if REFLECTION_SUPPORT
			case 254:
			{
				FilterFields(obj);
				bw.WriteInt(mFieldNames.size);

				for (int i = 0, imax = mFieldNames.size; i < imax; ++i)
				{
					bw.Write(mFieldNames[i]);
					bw.WriteObject(mFieldValues[i]);
				}
				break;
			}
#endif
			case 255:
			{
				formatter.Serialize(bw.BaseStream, obj);
				break;
			}
			default:
			{
				Debug.LogError("Prefix " + prefix + " is not supported");
				break;
			}
		}
	}

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
		return new Vector2(reader.ReadSingle(), reader.ReadSingle());
	}

	/// <summary>
	/// Read a value from the stream.
	/// </summary>

	static public Vector3 ReadVector3 (this BinaryReader reader)
	{
		return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
	}

	/// <summary>
	/// Read a value from the stream.
	/// </summary>

	static public Vector4 ReadVector4 (this BinaryReader reader)
	{
		return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
	}

	/// <summary>
	/// Read a value from the stream.
	/// </summary>

	static public Quaternion ReadQuaternion (this BinaryReader reader)
	{
		return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
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
		return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
	}

	/// <summary>
	/// Read the node hierarchy from the binary reader (binary format).
	/// </summary>

	static public DataNode ReadDataNode (this BinaryReader reader)
	{
		DataNode node = new DataNode();
		node.name = reader.ReadString();
		node.value = reader.ReadObject();
		int count = reader.ReadInt();
		for (int i = 0; i < count; ++i)
			node.children.Add(reader.ReadDataNode());
		return node;
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

	static public object ReadObject (this BinaryReader reader) { return reader.ReadObject(0, null, false); }

	/// <summary>
	/// Read a single object from the binary reader.
	/// </summary>

	static object ReadObject (this BinaryReader reader, int prefix, Type type, bool typeIsKnown)
	{
		if (!typeIsKnown)
		{
			prefix = reader.ReadByte();
			type = (prefix > 250) ? Type.GetType(reader.ReadString()) : GetType(prefix);
		}

		switch (prefix)
		{
			case 0: return null;
			case 1: return reader.ReadBoolean();
			case 2: return reader.ReadByte();
			case 3: return reader.ReadUInt16();
			case 4: return reader.ReadInt32();
			case 5: return reader.ReadUInt32();
			case 6: return reader.ReadSingle();
			case 7: return reader.ReadString();
			case 8: return reader.ReadVector2();
			case 9: return reader.ReadVector3();
			case 10: return reader.ReadVector4();
			case 11: return reader.ReadQuaternion();
			case 12: return reader.ReadColor32();
			case 13: return reader.ReadColor();
			case 14: return reader.ReadDataNode();
			case 99: // TNet.List
			{
#if REFLECTION_SUPPORT
				prefix = reader.ReadByte();
				type = (prefix > 250) ? Type.GetType(reader.ReadString()) : GetType(prefix);

				if (type == null)
				{
					Debug.LogError("Unknown type " + prefix);
					return null;
				}

				bool sameType = (reader.ReadByte() == 1);
				Type arrType = typeof(TNet.List<>).MakeGenericType(type);
				TList arr = (TList)Activator.CreateInstance(arrType);
				int elements = reader.ReadInt();

				for (int i = 0; i < elements; ++i)
					arr.Add(reader.ReadObject(prefix, type, sameType));
				return arr;
#else
				Debug.LogError("Reflection is not supported on this platform");
				return null;
#endif
			}
			case 100: // System.Collections.Generic.List
			{
#if REFLECTION_SUPPORT
				prefix = reader.ReadByte();
				type = (prefix > 250) ? Type.GetType(reader.ReadString()) : GetType(prefix);
				
				if (type == null)
				{
					Debug.LogError("Unknown type " + prefix);
					return null;
				}

				bool sameType = (reader.ReadByte() == 1);
				Type arrType = typeof(System.Collections.Generic.List<>).MakeGenericType(type);
				IList arr = (IList)Activator.CreateInstance(arrType);
				int elements = reader.ReadInt();

				for (int i = 0; i < elements; ++i)
					arr.Add(reader.ReadObject(prefix, type, sameType));
				return arr;
#else
				Debug.LogError("Reflection is not supported on this platform");
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
			case 253:
			{
				IBinarySerializable ser = (IBinarySerializable)type.GetConstructor(mVoid).Invoke(null);
				ser.Deserialize(reader);
				return ser;
			}
			case 254:
			{
#if REFLECTION_SUPPORT
				// Create the object
				object obj = type.GetConstructor(mVoid).Invoke(null);

				// How many fields have been serialized?
				int count = ReadInt(reader);

				for (int i = 0; i < count; ++i)
				{
					// Read the name of the field
					string fieldName = reader.ReadString();

					// Try to find this field
					FieldInfo fi = type.GetField(fieldName);

					// Read the value
					object val = reader.ReadObject();

					// Assign the value
					if (fi != null) fi.SetValue(obj, val);
				}
				return obj;
#else
				Debug.LogError("Reflection is not supported on this platform");
				return null;
#endif
			}
			case 255:
			{
				return formatter.Deserialize(reader.BaseStream);
			}
		}
		return null;
	}

#if REFLECTION_SUPPORT
	static Type[] mVoid = new Type[] { };
#endif

#endregion
#region Arrays
	static System.Collections.Generic.Dictionary<byte, object[]> mTemp =
		new System.Collections.Generic.Dictionary<byte, object[]>();

	/// <summary>
	/// Get a temporary array of specified size.
	/// </summary>

	static object[] GetTempBuffer (int count)
	{
		object[] temp;

		if (!mTemp.TryGetValue((byte)count, out temp))
		{
			temp = new object[count];
			mTemp[(byte)count] = temp;
		}
		return temp;
	}

	/// <summary>
	/// Write the array of objects into the specified writer.
	/// </summary>

	static public void WriteArray (this BinaryWriter bw, params object[] objs)
	{
		bw.WriteInt(objs.Length);
		if (objs.Length == 0) return;

		for (int b = 0, bmax = objs.Length; b < bmax; ++b)
			bw.WriteObject(objs[b]);
	}

	/// <summary>
	/// Read the object array from the specified reader.
	/// </summary>

	static public object[] ReadArray (this BinaryReader reader)
	{
		int count = reader.ReadInt();
		if (count == 0) return null;

		object[] temp = GetTempBuffer(count);

		for (int i = 0; i < count; ++i)
			temp[i] = reader.ReadObject();

		return temp;
	}

	/// <summary>
	/// Read the object array from the specified reader. The first value will be set to the specified object.
	/// </summary>

	static public object[] ReadArray (this BinaryReader reader, object obj)
	{
		int count = reader.ReadInt() + 1;

		object[] temp = GetTempBuffer(count);

		temp[0] = obj;
		for (int i = 1; i < count; ++i)
			temp[i] = reader.ReadObject();

		return temp;
	}

	/// <summary>
	/// Combine the specified object and array into one array in an efficient manner.
	/// </summary>

	static public object[] CombineArrays (object obj, params object[] objs)
	{
		int count = objs.Length;
		object[] temp = GetTempBuffer(count + 1);

		temp[0] = obj;
		for (int i = 0; i < count; ++i)
			temp[i + 1] = objs[i];

		return temp;
	}
#endregion
}
}
