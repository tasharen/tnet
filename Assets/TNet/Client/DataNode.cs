//----------------------------------------------
//            NGUI: Next-Gen UI kit
// Copyright © 2011-2014 Tasharen Entertainment
//----------------------------------------------

#if UNITY_EDITOR || (!UNITY_FLASH && !NETFX_CORE && !UNITY_WP8)
#define REFLECTION_SUPPORT
#endif

using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

#if REFLECTION_SUPPORT
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
#endif

namespace TNet
{
/// <summary>
/// Data Node is a hierarchical data type containing a name and a value, as well as a variable number of children.
/// Data Nodes can be serialized to and from IO data streams.
/// Think of it as an alternative to having to include a huge 1 MB+ XML parsing library in your project.
/// 
/// Basic Usage:
/// To create a new node: new DataNode (name, value).
/// To add a new child node: dataNode.AddChild("Scale", Vector3.one).
/// To retrieve a Vector3 value: dataNode.GetChild<Vector3>("Scale").
/// 
/// To make it possible to serialize custom classes into text format, make sure to override your class's ToString()
/// function, and add a "static object FromString (string data);" function to actually perform parsing of the same
/// string, returning an instance of your class created from the text data. For example:
/// 
/// public class MyClass
/// {
///     public int someID = 0;
///     
///     public override string ToString () { return someID.ToString(); }
///     
///     static object FromString (string data)
///     {
///         MyClass inst = new MyClass();
///         int.TryParse(data, out inst.someID);
///         return inst;
///     }
/// }
/// </summary>

[Serializable]
public class DataNode
{
	/// <summary>
	/// Data node's name.
	/// </summary>

	public string name;

	/// <summary>
	/// Data node's value.
	/// </summary>

	public object value;

	/// <summary>
	/// List of child nodes.
	/// </summary>

	public List<DataNode> children = new List<DataNode>();

	/// <summary>
	/// Type the value is currently in.
	/// </summary>

	public Type type { get { return (value != null) ? value.GetType() : typeof(void); } }

	public DataNode () { }
	public DataNode (string name) { this.name = name; }
	public DataNode (string name, object value) { this.name = name; this.value = value; }

	/// <summary>
	/// Clear the value and the list of children.
	/// </summary>

	public void Clear ()
	{
		value = null;
		children.Clear();
	}

	/// <summary>
	/// Get the node's value cast into the specified type.
	/// </summary>

	public object Get (Type type) { return ConvertValue(value, type); }

	/// <summary>
	/// Retrieve the value cast into the appropriate type.
	/// </summary>

	public T Get<T> ()
	{
		if (value is T) return (T)value;
		object retVal = Get(typeof(T));
		return (value != null) ? (T)retVal : default(T);
	}

	/// <summary>
	/// Convenience function to add a new child node.
	/// </summary>

	public DataNode AddChild ()
	{
		DataNode tn = new DataNode();
		children.Add(tn);
		return tn;
	}

	/// <summary>
	/// Add a new child node without checking to see if another child with the same name already exists.
	/// </summary>

	public DataNode AddChild (string name)
	{
		DataNode node = AddChild();
		node.name = name;
		return node;
	}

	/// <summary>
	/// Add a new child node without checking to see if another child with the same name already exists.
	/// </summary>

	public DataNode AddChild (string name, object value)
	{
		DataNode node = AddChild();
		node.name = name;
		node.value = (value is Enum) ? value.ToString() : value;
		return node;
	}

	/// <summary>
	/// Set a child value. Will add a new child if a child with the same name is not already present.
	/// </summary>

	public DataNode SetChild (string name, object value)
	{
		DataNode node = GetChild(name);
		if (node == null) node = AddChild();
		node.name = name;
		node.value = (value is Enum) ? value.ToString() : value;
		return node;
	}

	/// <summary>
	/// Retrieve a child by name.
	/// </summary>

	public DataNode GetChild (string name)
	{
		for (int i = 0; i < children.size; ++i)
			if (children[i].name == name)
				return children[i];
		return null;
	}

	/// <summary>
	/// Get the value of the existing child.
	/// </summary>

	public T GetChild<T> (string name)
	{
		DataNode node = GetChild(name);
		if (node == null) return default(T);
		return node.Get<T>();
	}

	/// <summary>
	/// Get the value of the existing child or the default value if the child is not present.
	/// </summary>

	public T GetChild<T> (string name, T defaultValue)
	{
		DataNode node = GetChild(name);
		if (node == null) return defaultValue;
		return node.Get<T>();
	}

#region Serialization

	/// <summary>
	/// Write the node hierarchy to the specified filename.
	/// </summary>

	public void Write (string path) { Write(path, false); }

	/// <summary>
	/// Read the node hierarchy from the specified file.
	/// </summary>

	static public DataNode Read (string path) { return Read(path, false); }

	/// <summary>
	/// Write the node hierarchy to the specified filename.
	/// </summary>

	public void Write (string path, bool binary)
	{
		if (binary)
		{
			FileStream stream = File.Create(path);
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteObject(this);
			writer.Close();
		}
		else
		{
			StreamWriter writer = new StreamWriter(path, false);
			Write(writer, 0);
			writer.Close();
		}
	}

	/// <summary>
	/// Read the node hierarchy from the specified file.
	/// </summary>

	static public DataNode Read (string path, bool binary)
	{
		if (binary)
		{
			FileStream stream = File.OpenRead(path);
			BinaryReader reader = new BinaryReader(stream);
			DataNode node = reader.ReadObject<DataNode>();
			stream.Close();
			return node;
		}
		else
		{
			StreamReader reader = new StreamReader(path);
			DataNode node = Read(reader);
			reader.Close();
			return node;
		}
	}

	/// <summary>
	/// Write the node hierarchy to the stream reader, saving it in text format.
	/// </summary>

	public void Write (StreamWriter writer) { Write(writer, 0); }

	/// <summary>
	/// Read the node hierarchy from the stream reader containing data in text format.
	/// </summary>

	static public DataNode Read (StreamReader reader)
	{
		string line = GetNextLine(reader);
		int offset = CalculateTabs(line);
		DataNode node = new DataNode();
		node.Read(reader, line, ref offset);
		return node;
	}

	/// <summary>
	/// Convenience function for easy debugging -- convert the entire data into the string representation form.
	/// </summary>

	public override string ToString ()
	{
		string text = "";
		Write(ref text, 0);
		return text;
	}

	/// <summary>
	/// Convert the node's value to a human-readable string.
	/// </summary>

	string GetValueString ()
	{
		if (type == typeof(string)) return "\"" + value + "\"";
		if (type == typeof(Int32) || type == typeof(float)) return value.ToString();
		if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Color))
			return "(" + GetValueDataString() + ")";
		return string.Format("{0}({1})", Serialization.TypeToName(type), GetValueDataString());
	}

	/// <summary>
	/// Write the node into the string.
	/// </summary>

	void Write (ref string data, int tab)
	{
		if (!string.IsNullOrEmpty(name))
		{
			for (int i = 0; i < tab; ++i) data += "\t";
			data += Escape(name);
			if (value != null) data += " = " + GetValueString();
			data += "\n";
			for (int i = 0; i < children.size; ++i)
				children[i].Write(ref data, tab + 1);
		}
	}

	/// <summary>
	/// Write the node into the stream writer.
	/// </summary>

	void Write (StreamWriter writer, int tab)
	{
		if (!string.IsNullOrEmpty(name))
		{
			for (int i = 0; i < tab; ++i)
				writer.Write("\t");

			writer.Write(Escape(name));

			if (value != null)
			{
				writer.Write(" = ");
				writer.Write(GetValueString());
			}
			writer.Write("\n");

			for (int i = 0; i < children.size; ++i)
				children[i].Write(writer, tab + 1);
		}
	}

	/// <summary>
	/// Read this node and all of its children from the stream reader.
	/// </summary>

	string Read (StreamReader reader, string line, ref int offset)
	{
		if (line != null)
		{
			int expected = offset;
			SetFromLine(line, expected);

			line = GetNextLine(reader);
			offset = CalculateTabs(line);

			while (line != null)
			{
				if (offset == expected + 1)
				{
					line = AddChild().Read(reader, line, ref offset);
				}
				else break;
			}
		}
		return line;
	}
#endregion
#region Private Functions

	/// <summary>
	/// Convert the node's value to a human-readable string.
	/// </summary>

	string GetValueDataString ()
	{
		if (value is float)
		{
			float f = (float)value;
			return f.ToString(CultureInfo.InvariantCulture);
		}
		else if (value is Vector2)
		{
			Vector2 v = (Vector2)value;
			return v.x.ToString(CultureInfo.InvariantCulture) + ", " +
				v.y.ToString(CultureInfo.InvariantCulture);
		}
		else if (value is Vector3)
		{
			Vector3 v = (Vector3)value;
			return v.x.ToString(CultureInfo.InvariantCulture) + ", " +
				v.y.ToString(CultureInfo.InvariantCulture) + ", " +
				v.z.ToString(CultureInfo.InvariantCulture);
		}
		else if (value is Vector4)
		{
			Vector4 v = (Vector4)value;
			return v.x.ToString(CultureInfo.InvariantCulture) + ", " +
				v.y.ToString(CultureInfo.InvariantCulture) + ", " +
				v.z.ToString(CultureInfo.InvariantCulture) + ", " +
				v.w.ToString(CultureInfo.InvariantCulture);
		}
		else if (value is Quaternion)
		{
			Quaternion q = (Quaternion)value;
			Vector3 v = q.eulerAngles;
			return v.x.ToString(CultureInfo.InvariantCulture) + ", " +
				v.y.ToString(CultureInfo.InvariantCulture) + ", " +
				v.z.ToString(CultureInfo.InvariantCulture);
		}
		else if (value is Color)
		{
			Color v = (Color)value;
			return v.r.ToString(CultureInfo.InvariantCulture) + ", " +
				v.g.ToString(CultureInfo.InvariantCulture) + ", " +
				v.b.ToString(CultureInfo.InvariantCulture) + ", " +
				v.a.ToString(CultureInfo.InvariantCulture);
		}
		else if (value is Color32)
		{
			Color v = (Color32)value;
			return v.r + ", " + v.g + ", " + v.b + ", " + v.a;
		}
		else if (value is Rect)
		{
			Rect r = (Rect)value;
			return r.x.ToString(CultureInfo.InvariantCulture) + ", " +
				r.y.ToString(CultureInfo.InvariantCulture) + ", " +
				r.width.ToString(CultureInfo.InvariantCulture) + ", " +
				r.height.ToString(CultureInfo.InvariantCulture);
		}
		else if (value != null)
		{
			//if (value.GetType().IsClass) return "Class";
			return value.ToString().Replace("\n", "\\n");
		}
		return "";
	}

#if REFLECTION_SUPPORT
	static object[] mInvokeParams = new object[1];
#endif

	/// <summary>
	/// Set the node's value using its text representation.
	/// </summary>

	bool SetValue (string text, Type type, string[] parts)
	{
		if (type == null || type == typeof(void))
		{
			value = null;
		}
		else if (type == typeof(string))
		{
			value = text;
		}
		else if (type == typeof(bool))
		{
			bool b;
			if (bool.TryParse(text, out b)) value = b;
		}
		else if (type == typeof(byte))
		{
			byte b;
			if (byte.TryParse(text, out b)) value = b;
		}
		else if (type == typeof(Int16))
		{
			Int16 b;
			if (Int16.TryParse(text, out b)) value = b;
		}
		else if (type == typeof(UInt16))
		{
			UInt16 b;
			if (UInt16.TryParse(text, out b)) value = b;
		}
		else if (type == typeof(Int32))
		{
			Int32 b;
			if (Int32.TryParse(text, out b)) value = b;
		}
		else if (type == typeof(UInt32))
		{
			UInt32 b;
			if (UInt32.TryParse(text, out b)) value = b;
		}
		else if (type == typeof(float))
		{
			float b;
			if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out b)) value = b;
		}
		else if (type == typeof(double))
		{
			double b;
			if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out b)) value = b;
		}
		else if (type == typeof(Vector2))
		{
			if (parts == null) parts = text.Split(',');

			if (parts.Length == 2)
			{
				Vector2 v;
				if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v.x) &&
					float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v.y))
					value = v;
			}
		}
		else if (type == typeof(Vector3))
		{
			if (parts == null) parts = text.Split(',');

			if (parts.Length == 3)
			{
				Vector3 v;
				if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v.x) &&
					float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v.y) &&
					float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v.z))
					value = v;
			}
		}
		else if (type == typeof(Vector4))
		{
			if (parts == null) parts = text.Split(',');

			if (parts.Length == 4)
			{
				Vector4 v;
				if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v.x) &&
					float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v.y) &&
					float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v.z) &&
					float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out v.w))
					value = v;
			}
		}
		else if (type == typeof(Quaternion))
		{
			if (parts == null) parts = text.Split(',');

			if (parts.Length == 4)
			{
				Quaternion v;
				if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v.x) &&
					float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v.y) &&
					float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v.z) &&
					float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out v.w))
					value = v;
			}
		}
		else if (type == typeof(Color32))
		{
			if (parts == null) parts = text.Split(',');

			if (parts.Length == 4)
			{
				Color32 v;
				if (byte.TryParse(parts[0], out v.r) &&
					byte.TryParse(parts[1], out v.g) &&
					byte.TryParse(parts[2], out v.b) &&
					byte.TryParse(parts[3], out v.a))
					value = v;
			}
		}
		else if (type == typeof(Color))
		{
			if (parts == null) parts = text.Split(',');

			if (parts.Length == 4)
			{
				Color v;
				if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v.r) &&
					float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v.g) &&
					float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v.b) &&
					float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out v.a))
					value = v;
			}
		}
		else if (type == typeof(Rect))
		{
			if (parts == null) parts = text.Split(',');

			if (parts.Length == 4)
			{
				Vector4 v;
				if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v.x) &&
					float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v.y) &&
					float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v.z) &&
					float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out v.w))
					value = new Rect(v.x, v.y, v.z, v.w);
			}
		}
#if REFLECTION_SUPPORT
		else if (!type.IsSubclassOf(typeof(Component)))
		{
			try
			{
				MethodInfo info = type.GetMethod("FromString",
					BindingFlags.Public |
					BindingFlags.NonPublic |
					BindingFlags.Static);

				if (info != null)
				{
					mInvokeParams[0] = text.Replace("\\n", "\n");
					value = info.Invoke(null, mInvokeParams);
				}
				else return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning(ex.Message);
				return false;
			}
		}
#endif
		else return false;
		return true;
	}

	/// <summary>
	/// Helper function to set the node's name and value using the line that was read from a stream.
	/// </summary>

	void SetFromLine (string line, int offset)
	{
		int divider = line.IndexOf("=", offset);

		if (divider == -1)
		{
			name = Unescape(line.Substring(offset)).Trim();
			return;
		}

		name = Unescape(line.Substring(offset, divider - offset)).Trim();

		// Skip past the divider
		line = line.Substring(divider + 1).Trim();

		// No data available
		if (line.Length < 1) return;

		// There must be at least 3 characters present at this point
		if (line.Length > 2)
		{
			// If the line starts with a quote, it must also end with a quote
			if (line[0] == '"' && line[line.Length - 1] == '"')
			{
				value = line.Substring(1, line.Length - 2);
				return;
			}

			// If the line starts with an opening bracket, it must always end with a closing bracket
			if (line[0] == '(' && line[line.Length - 1] == ')')
			{
				line = line.Substring(1, line.Length - 2);
				string[] parts = line.Split(',');

				if (parts.Length == 1) { SetValue(line, typeof(float), null); return; }
				if (parts.Length == 2) { SetValue(line, typeof(Vector2), parts); return; }
				if (parts.Length == 3) { SetValue(line, typeof(Vector3), parts); return; }
				if (parts.Length == 4) { SetValue(line, typeof(Color), parts); return; }

				value = line;
				return;
			}
		}

		int dataStart = line.IndexOf('(');

		if (dataStart != -1)
		{
			Type type = typeof(string);

			// For some odd reason LastIndexOf() fails to find the last character of the string
			int dataEnd = (line[line.Length - 1] == ')') ? line.Length - 1 : line.LastIndexOf(')', dataStart);

			if (dataEnd != -1 && line.Length > 2)
			{
				string strType = line.Substring(0, dataStart);
				type = Serialization.NameToType(strType);
				line = line.Substring(dataStart + 1, dataEnd - dataStart - 1);
			}
			SetValue(line, type, null);
		}
		else if (line.IndexOf('.') != -1)
		{
			float f;

			if (float.TryParse(line, out f))
			{
				value = f;
				return;
			}
		}
		else
		{
			int i;

			if (int.TryParse(line, out i))
			{
				value = i;
				return;
			}
		}
		value = line;
	}
#endregion
#region Static Helper Functions

	/// <summary>
	/// Get the next line from the stream reader.
	/// </summary>

	static string GetNextLine (StreamReader reader)
	{
		string line = reader.ReadLine();

		while (line != null && line.Trim().StartsWith("//"))
		{
			line = reader.ReadLine();
			if (line == null) return null;
		}
		return line;
	}

	/// <summary>
	/// Calculate the number of tabs at the beginning of the line.
	/// </summary>

	static int CalculateTabs (string line)
	{
		if (line != null)
		{
			for (int i = 0; i < line.Length; ++i)
			{
				if (line[i] == '\t') continue;
				return i;
			}
		}
		return 0;
	}

	/// <summary>
	/// Escape the characters in the string.
	/// </summary>

	static string Escape (string val)
	{
		if (!string.IsNullOrEmpty(val))
		{
			val = val.Replace("\n", "\\n");
			val = val.Replace("\t", "\\t");
		}
		return val;
	}

	/// <summary>
	/// Recover escaped characters, converting them back to usable characters.
	/// </summary>

	static string Unescape (string val)
	{
		if (!string.IsNullOrEmpty(val))
		{
			val = val.Replace("\\n", "\n");
			val = val.Replace("\\t", "\t");
		}
		return val;
	}

	/// <summary>
	/// Helper function to convert the specified value into the provided type.
	/// </summary>

	static object ConvertValue (object value, Type type)
	{
		if (value == null) return null;
		if (type.IsAssignableFrom(value.GetType())) return value;

#if REFLECTION_SUPPORT
		if (type.IsEnum)
		{
			if (value.GetType() == typeof(Int32))
				return value;

			if (value.GetType() == typeof(string))
			{
				string strVal = (string)value;

				if (!string.IsNullOrEmpty(strVal))
				{
					string[] enumNames = Enum.GetNames(type);
					for (int i = 0; i < enumNames.Length; ++i)
						if (enumNames[i] == strVal)
							return Enum.GetValues(type).GetValue(i);
				}
			}
		}
#endif
		return null;
	}
#endregion
}
}
