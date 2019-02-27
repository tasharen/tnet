//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

#if UNITY_EDITOR || (!UNITY_FLASH && !NETFX_CORE && !UNITY_WP8 && !UNITY_WP_8_1)
#define REFLECTION_SUPPORT

// Enabling this would allow you to create IDataNodeSerializable-like serialization on all classes via extensions, such as:
//		void Serialize (this CustomClassType obj, DataNode node);
//		void Deserialize (this CustomClassType obj, DataNode node);
// Note that this would also only affect DataNode's text serialization, since IBinarySerializable is used for binary saves.

#define SERIALIZATION_WITHOUT_INTERFACE
#endif

using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

#if !STANDALONE
using UnityEngine;
#endif

#if REFLECTION_SUPPORT
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
#endif

namespace TNet
{
	/// <summary>
	/// Implementing the IDataNodeSerializable interface in your class will make it possible to serialize
	/// that class into the Data Node format more efficiently.
	/// </summary>

	public interface IDataNodeSerializable
	{
		/// <summary>
		/// Serialize the object's data into Data Node.
		/// </summary>

		void Serialize (DataNode node);

		/// <summary>
		/// Deserialize the object's data from Data Node.
		/// </summary>

		void Deserialize (DataNode node);
	}

	/// <summary>
	/// Data Node is a hierarchical data type containing a name and a value, as well as a variable number of children.
	/// Data Nodes can be serialized to and from IO data streams.
	/// Think of it as an alternative to having to include a huge 1 MB+ XML parsing library in your project.
	///
	/// Basic Usage:
	/// To create a new node: new DataNode (name, value).
	/// To add a new child node: dataNode.AddChild("Scale", Vector3.one).
	/// To retrieve a Vector3 value: dataNode.GetChild<Vector3>("Scale").
	/// </summary>

	[Serializable]
	public class DataNode
	{
		[DoNotObfuscate] public enum SaveType
		{
			Text,
			Binary,
			Compressed,
		}

		// This many child nodes have to be present before a lookup dictionary will be used to speed up GetChild(name) calls.
		// The lookup dictionary is created when GetChild(name) is used, and is only populated one GetChild call at a time.
		const int LOOKUP_CHILD_REQUIREMENT = 8;

		// Must remain 4 bytes long
		static byte[] mLZMA = new byte[] { (byte)'C', (byte)'D', (byte)'0', (byte)'1' };

		// Actual saved value
		object mValue = null;

		// Temporary flag that gets set to 'true' after text-based deserialization
		[NonSerialized] bool mResolved = true;

		/// <summary>
		/// Data node's name.
		/// </summary>

		public string name;

		/// <summary>
		/// Data node's value.
		/// </summary>

		public object value
		{
			set
			{
				mValue = value;
				mResolved = true;
			}
			get
			{
				// ResolveValue returns 'false' when children were used by the custom data type and should now be ignored.
				if (!mResolved && !ResolveValue(null))
				{
					children.Clear();
					mCache = null;
				}
				return mValue;
			}
		}

		/// <summary>
		/// Whether this node is serializable or not.
		/// A node must have a value or children for it to be serialized. Otherwise there isn't much point in doing so.
		/// </summary>

		public bool isSerializable { get { return value != null || children.size > 0; } }

		/// <summary>
		/// List of child nodes.
		/// </summary>

		public List<DataNode> children = new List<DataNode>();

		// Used to speed up GetChild lookups. Filled automatically on GetChild() if there are more than a handful of nodes present.
		[System.NonSerialized] protected System.Collections.Generic.Dictionary<string, DataNode> mCache = null;

		/// <summary>
		/// Type the value is currently in.
		/// </summary>

		public Type type { get { return (value != null) ? mValue.GetType() : typeof(void); } }

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
			mCache = null;
		}

		/// <summary>
		/// Mark the child list as having changed. Call it after modifying the 'children' manually.
		/// </summary>

		public void MarkChildrenAsChanged () { mCache = null; }

		/// <summary>
		/// Get the node's value cast into the specified type.
		/// </summary>

		public object Get (Type type) { return Serialization.ConvertObject(value, type); }

		/// <summary>
		/// Retrieve the value cast into the appropriate type.
		/// </summary>

		public T Get<T> ()
		{
			if (value is T) return (T)mValue;
			var converted = Serialization.Convert<T>(mValue);
			if (mValue is byte[] && converted != null) mValue = converted;
			return converted;
		}

		/// <summary>
		/// Retrieve the value cast into the appropriate type.
		/// </summary>

		public T Get<T> (T defaultVal)
		{
			if (value is T) return (T)mValue;
			var converted = Serialization.Convert(mValue, defaultVal);
			if (mValue is byte[] && converted != null) mValue = converted;
			return converted;
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
			node.value = value;
			return node;
		}

		/// <summary>
		/// Add a new child node after checking to see if it already exists. If it does, the existing value is returned.
		/// </summary>

		public DataNode AddMissingChild (string name, object value)
		{
			DataNode node = GetChild(name);
			if (node != null) return node;
			node = AddChild();
			node.name = name;
			node.value = value;
			return node;
		}

		/// <summary>
		/// Set the specified child, replacing an existing one if one already exists with the same name.
		/// </summary>

		public DataNode ReplaceChild (DataNode child)
		{
			if (child == null) return null;

			mCache = null;

			for (int i = 0; i < children.size; ++i)
			{
				if (children.buffer[i].name == child.name)
				{
					if (child.value == null && child.children.size == 0)
					{
						children.RemoveAt(i);
						return child;
					}

					children.buffer[i] = child;
					return children.buffer[i];
				}
			}

			children.Add(child);
			return child;
		}

		/// <summary>
		/// Set a child value. Will add a new child if a child with the same name is not already present.
		/// </summary>

		public DataNode SetChild (string name, object value)
		{
			DataNode node = GetChild(name);
			if (node == null) node = AddChild();
			node.name = name;
			node.value = value;
			return node;
		}

		/// <summary>
		/// Find a child with the specified name starting at the chosen path.
		/// </summary>

		public DataNode FindChild (string name, string path = null)
		{
			if (!string.IsNullOrEmpty(path))
			{
				var node = GetHierarchy(path);
				if (node == null) return null;
				return node.FindChild(name);
			}

			var ch = GetChild(name);
			if (ch != null) return ch;

			for (int i = 0; i < children.size; ++i)
			{
				var child = children.buffer[i].FindChild(name);
				if (child != null) return child;
			}
			return null;
		}

		/// <summary>
		/// Retrieve a child by its path.
		/// </summary>

		public DataNode GetHierarchy (string path)
		{
			if (path.IndexOf('\\') != -1) path = path.Replace("\\", "/");

			if (path.IndexOf('/') != -1)
			{
#if UNITY_EDITOR && !UNITY_4_7
				UnityEngine.Profiling.Profiler.BeginSample("DataNode.GetHierarchy(path)");
#endif
				var split = path.Split('/');
				DataNode node = this;
				int index = 0;

				while (node != null && index < split.Length)
				{
					bool found = false;

					for (int i = 0; i < node.children.size; ++i)
					{
						if (node.children.buffer[i].name == split[index])
						{
							node = node.children.buffer[i];
							++index;
							found = true;
							break;
						}
					}

					if (!found) return null;
				}
#if UNITY_EDITOR && !UNITY_4_7
				UnityEngine.Profiling.Profiler.EndSample();
#endif
				return node;
			}
			return GetChild(path);
		}

		/// <summary>
		/// Retrieve a child by its path.
		/// </summary>

		public T GetHierarchy<T> (string path)
		{
			var node = GetHierarchy(path);
			if (node == null) return default(T);
			var value = node.value;
			if (value is T) return (T)node.value;
			return Serialization.Convert<T>(value);
		}

		/// <summary>
		/// Retrieve a child by its path.
		/// </summary>

		public T GetHierarchy<T> (string path, T defaultValue)
		{
			var node = GetHierarchy(path);
			if (node == null) return defaultValue;
			var value = node.value;
			if (value is T) return (T)node.value;
			return Serialization.Convert<T>(value, defaultValue);
		}

		/// <summary>
		/// Set a node's value given its hierarchical path.
		/// </summary>

		public DataNode SetHierarchy (string path, object obj)
		{
			DataNode node = this;

			if (!string.IsNullOrEmpty(path))
			{
				if (path.IndexOf('\\') == -1 && path.IndexOf('/') == -1)
				{
					if (obj == null)
					{
						RemoveChild(path);
						return null;
					}

					node = GetChild(path, true);
				}
				else
				{
					path = path.Replace("\\", "/");
					var names = path.Split('/');
					DataNode parent = null;
					int index = 0;

					while (node != null && index < names.Length)
					{
						bool found = false;

						for (int i = 0; i < node.children.size; ++i)
						{
							if (node.children.buffer[i].name == names[index])
							{
								parent = node;
								node = node.children.buffer[i];
								++index;
								found = true;
								break;
							}
						}

						if (!found)
						{
							// No need to do anything -- the requested path is already missing
							if (obj == null) return parent;

							// Add a new node
							parent = node;
							node = node.AddChild(names[index]);
							++index;
						}
					}

					if (node != null && obj == null)
					{
						parent.RemoveChild(names[index - 1]);
						return parent;
					}
				}
			}

			if (obj is DataNode)
			{
				var other = (obj as DataNode);
				node.value = other.value;
				node.children.Clear();
				node.mCache = null;
				for (int i = 0; i < other.children.size; ++i)
					node.children.Add(other.children.buffer[i].Clone());
			}
			else node.value = obj;
			return node;
		}

		/// <summary>
		/// Remove the specified child from the list. Returns the parent node of the removed node if successful.
		/// </summary>

		public DataNode RemoveHierarchy (string path) { return SetHierarchy(path, null); }

		/// <summary>
		/// Retrieve a child by name, optionally creating a new one if the child doesn't already exist.
		/// </summary>

		public DataNode GetChild (string name, bool createIfMissing = false)
		{
			// Automatically create a lookup dictionary
			if (mCache == null && children.size >= LOOKUP_CHILD_REQUIREMENT)
				mCache = new Dictionary<string, DataNode>();

			// Try to use the lookup dictionary if possible
			if (mCache != null)
			{
				DataNode ch;
				if (mCache.TryGetValue(name, out ch)) return ch;
			}

			for (int i = 0; i < children.size; ++i)
			{
				var ch = children.buffer[i];

				if (ch.name == name)
				{
					// Add this record to the dictionary to speed up future lookups
					if (mCache != null) mCache[name] = ch;
					return ch;
				}
			}

			if (createIfMissing) return AddChild(name);
			return null;
		}

		/// <summary>
		/// Get the value of the existing child.
		/// </summary>

		public T GetChild<T> (string name)
		{
			var node = GetChild(name);
			if (node == null) return default(T);
			return node.Get<T>();
		}

		/// <summary>
		/// Get the value of the existing child or the default value if the child is not present.
		/// </summary>

		public T GetChild<T> (string name, T defaultValue)
		{
			var node = GetChild(name);
			if (node == null) return defaultValue;
			return node.Get(defaultValue);
		}

		/// <summary>
		/// Remove the specified child from the list.
		/// </summary>

		public bool RemoveChild (string name)
		{
			if (mCache != null) mCache.Remove(name);

			for (int i = 0; i < children.size; ++i)
			{
				if (children.buffer[i].name == name)
				{
					children.RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Clone the DataNode, creating a copy.
		/// </summary>

		public DataNode Clone ()
		{
			var copy = new DataNode(name);
			copy.mValue = mValue;
			copy.mResolved = mResolved;
			for (int i = 0; i < children.size; ++i) copy.children.Add(children.buffer[i].Clone());
			return copy;
		}

		#region Serialization

		/// <summary>
		/// Write the node hierarchy to the specified filename.
		/// </summary>

		public bool Write (string path, SaveType type = SaveType.Text, bool inMyDocuments = false, bool allowConfigAccess = false)
		{
			bool retVal = false;
			var stream = new MemoryStream();

			if (type == SaveType.Binary)
			{
				var writer = new BinaryWriter(stream);
				writer.WriteObject(this);
				retVal = Tools.WriteFile(path, stream, inMyDocuments, allowConfigAccess);
				writer.Close();
			}
			else if (type == SaveType.Compressed)
			{
				var writer = new BinaryWriter(stream);
				writer.WriteObject(this);

				stream.Position = 0;
				var comp = LZMA.Compress(stream, mLZMA);

				if (comp != null)
				{
					retVal = Tools.WriteFile(path, comp, inMyDocuments, allowConfigAccess);
					comp.Close();
				}
				else retVal = Tools.WriteFile(path, stream, inMyDocuments, allowConfigAccess);
				writer.Close();
			}
			else
			{
				var writer = new StreamWriter(stream);
				Write(writer, 0);
				retVal = Tools.WriteFile(path, stream, inMyDocuments, allowConfigAccess);
				writer.Close();
			}
			return retVal;
		}

		/// <summary>
		/// Write the node hierarchy to the specified filename.
		/// </summary>

		[System.Obsolete("Use DataNode.Write(path, SaveType)")]
		public bool Write (string path, bool binary) { return Write(path, binary ? SaveType.Binary : SaveType.Text); }

		/// <summary>
		/// Read the node hierarchy from the specified file.
		/// </summary>

		static public DataNode Read (string path, bool allowConfigAccess = false)
		{
			return Read(Tools.ReadFile(path, allowConfigAccess));
		}

		/// <summary>
		/// Attempt to determine the saved data's format -- binary, compressed or text.
		/// </summary>

		static public SaveType GetSaveType (byte[] data)
		{
			if (data == null || data.Length < 4)
				return SaveType.Binary;

			if (data[0] == mLZMA[0] && data[1] == mLZMA[1] && data[2] == mLZMA[2] && data[3] == mLZMA[3])
				return SaveType.Compressed;

			for (int i = 0; i < 4; ++i)
			{
				byte ch = data[i];
				if (ch >= '!' && ch <= '~') continue;
				if (ch == ' ') continue;
				return SaveType.Binary;
			}
			return SaveType.Text;
		}

		/// <summary>
		/// Read the node hierarchy from the specified buffer.
		/// </summary>

		static public DataNode Read (byte[] data)
		{
			if (data == null || data.Length < 4) return null;
			return Read(data, GetSaveType(data));
		}

		/// <summary>
		/// Read the node hierarchy from the specified buffer. Kept for backwards compatibility.
		/// In most cases you will want to use the Read(bytes) function instead.
		/// </summary>

		[System.Obsolete("The 'binary' parameter is no longer used. Use DataNode.Read(bytes) instead")]
		static public DataNode Read (byte[] bytes, bool binary) { return Read(bytes); }

		/// <summary>
		/// Read the node hierarchy from the specified buffer.
		/// </summary>

		static public DataNode Read (byte[] bytes, SaveType type)
		{
			if (bytes == null || bytes.Length < 4) return null;

			if (type == SaveType.Text)
			{
				var stream = new MemoryStream(bytes);
				var reader = new StreamReader(stream);
				var node = Read(reader);
				reader.Close();
				return node;
			}
			else if (type == SaveType.Compressed)
			{
				bool skipPrefix = true;

				for (int i = 0; i < 4; ++i)
				{
					if (bytes[i] != mLZMA[i])
					{
						skipPrefix = false;
						break;
					}
				}

				bytes = LZMA.Decompress(bytes, skipPrefix ? 4 : 0);
			}
			{
				var stream = new MemoryStream(bytes);
				var reader = new BinaryReader(stream);
				var node = reader.ReadObject<DataNode>();
				reader.Close();
				return node;
			}
		}

		/// <summary>
		/// Just here for consistency.
		/// </summary>

		public void Write (BinaryWriter writer, bool compressed = false)
		{
			if (compressed)
			{
				var lzma = new LZMA();
				lzma.BeginWriting().WriteObject(this);
				var data = lzma.Compress();

				if (data != null)
				{
					for (int i = 0; i < 4; ++i) writer.Write(mLZMA[i]);
					writer.Write(data);
					return;
				}
			}
			writer.WriteObject(this);
		}

		/// <summary>
		/// Write the node hierarchy to the stream reader, saving it in text format.
		/// </summary>

		public void Write (StreamWriter writer, int tab = 0)
		{
			// Only proceed if this node has some data associated with it
			if (tab == 0 && string.IsNullOrEmpty(name))
			{
				Write(writer, "Version", value != null ? value : Player.version);
			}
			else Write(writer, string.IsNullOrEmpty(name) ? "DataNode" : name, value, tab);

			// Iterate through children
			for (int i = 0; i < children.size; ++i)
			{
				var child = children.buffer[i];

				if (child.isSerializable)
				{
					writer.Write('\n');
					child.Write(writer, tab + 1);
				}
			}

			if (tab == 0) writer.Flush();
		}

		/// <summary>
		/// Write the values into the stream writer.
		/// </summary>

		static void Write (StreamWriter writer, string name, object value, int tab = 0)
		{
			bool prefix = false;

			if (!string.IsNullOrEmpty(name))
			{
				prefix = true;
				writer.WriteTabs(tab);
				writer.Write(name);
			}
			else if (value != null)
			{
				writer.WriteTabs(tab);
			}

			if (value != null && !writer.WriteObject(value, prefix))
			{
				var type = value.GetType();

				if (value is DataNode)
				{
					if (prefix) writer.Write(" = ");
					writer.Write("DataNode");
					writer.Write('\n');
					var node = (DataNode)value;
					node.Write(writer, tab + 1);
					return;
				}

#if !STANDALONE
				if (value is AnimationCurve)
				{
					var ac = value as AnimationCurve;
					var kfs = ac.keys;
					type = typeof(Vector4[]);
					var imax = kfs.Length;
					var vs = new Vector4[imax];

					for (int i = 0; i < imax; ++i)
					{
						var kf = kfs[i];
						vs[i] = new Vector4(kf.time, kf.value, kf.inTangent, kf.outTangent);
					}
					value = vs;
				}
#endif
				// Save cloth skinning coefficients as a Vector2 array
				if (value is ClothSkinningCoefficient[])
				{
					var cf = value as ClothSkinningCoefficient[];
					type = typeof(Vector2[]);
					var imax = cf.Length;
					var vs = new Vector2[imax];

					for (int i = 0; i < imax; ++i)
					{
						vs[i].x = cf[i].maxDistance;
						vs[i].y = cf[i].collisionSphereDistance;
					}
					value = vs;
				}

				if (value is TList)
				{
					var list = value as TList;

					if (prefix) writer.Write(" = ");
					writer.Write(Serialization.TypeToName(type));

					if (list.Count > 0)
					{
						for (int i = 0, imax = list.Count; i < imax; ++i)
						{
							writer.Write('\n');
							Write(writer, null, list.Get(i), tab + 1);
						}
					}
					return;
				}

				if (value is System.Collections.IList)
				{
					var list = value as System.Collections.IList;

					if (prefix) writer.Write(" = ");
					writer.Write(Serialization.TypeToName(type));

					if (list.Count > 0)
					{
						for (int i = 0, imax = list.Count; i < imax; ++i)
						{
							writer.Write('\n');
							Write(writer, null, list[i], tab + 1);
						}
					}
					return;
				}

				// IDataNodeSerializable interface has serialization functions
				if (value is IDataNodeSerializable)
				{
					var ser = value as IDataNodeSerializable;
					var temp = mTemp;
					mTemp = null;
					if (temp == null) temp = new DataNode();
					ser.Serialize(temp);

					if (prefix) writer.Write(" = ");
					writer.Write(Serialization.TypeToName(type));

					for (int i = 0; i < temp.children.size; ++i)
					{
						var child = temp.children.buffer[i];
						writer.Write('\n');
						child.Write(writer, tab + 1);
					}

					temp.Clear();
					mTemp = temp;
					return;
				}

#if REFLECTION_SUPPORT
#if SERIALIZATION_WITHOUT_INTERFACE
				// Try custom serialization first
				if (type.HasDataNodeSerialization())
				{
					var temp = mTemp;
					mTemp = null;
					if (temp == null) temp = new DataNode();

					if (value.Invoke("Serialize", temp))
					{
						if (prefix) writer.Write(" = ");
						writer.Write(Serialization.TypeToName(type));

						for (int i = 0; i < temp.children.size; ++i)
						{
							var child = temp.children.buffer[i];
							writer.Write('\n');
							child.Write(writer, tab + 1);
						}

						temp.Clear();
						mTemp = temp;
						return;
					}
				}
#endif

				if (prefix) writer.Write(" = ");
				writer.Write(Serialization.TypeToName(type));
				var fields = type.GetSerializableFields();

				// We have fields to serialize
				for (int i = 0; i < fields.size; ++i)
				{
					var field = fields.buffer[i];
					var val = field.GetValue(value);

					if (val != null)
					{
						writer.Write('\n');
						Write(writer, field.Name, val, tab + 1);
					}
				}

				if (fields.size == 0 || type.IsDefined(typeof(SerializeProperties), true))
				{
					// We don't have fields to serialize, but we may have properties
					var props = type.GetSerializableProperties();

					if (props.size > 0)
					{
						for (int i = 0; i < props.size; ++i)
						{
							var prop = props.buffer[i];
							object val = prop.GetValue(value, null);

							if (val != null)
							{
								writer.Write('\n');
								Write(writer, prop.Name, val, tab + 1);
							}
						}
					}
				}
#endif
			}
		}

		[System.NonSerialized] static DataNode mTemp;

		/// <summary>
		/// Read the node hierarchy from the stream reader containing data in text format.
		/// </summary>

		static public DataNode Read (TextReader reader)
		{
			var line = GetNextLine(reader);
			var offset = CalculateTabs(line);
			var node = new DataNode();
			node.Read(reader, line, ref offset);
			return node;
		}

		/// <summary>
		/// Merge the current data with the specified. Returns whether some node's value was replaced.
		/// </summary>

		public bool Merge (DataNode other, bool replaceExisting = true)
		{
			bool replaced = false;

			if (other != null)
			{
				if (replaceExisting || value == null)
				{
					if (value != null && other.value != null) replaced = true;
					value = other.value;
				}

				for (int i = 0; i < other.children.size; ++i)
				{
					DataNode child = other.children.buffer[i];
					replaced |= GetChild(child.name, true).Merge(child, replaceExisting);
				}
			}
			return replaced;
		}

		/// <summary>
		/// Convenience function for easy debugging -- convert the entire data into the string representation form.
		/// </summary>

		public override string ToString ()
		{
			if (!isSerializable) return "";
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			Write(writer, 0);

			stream.Seek(0, SeekOrigin.Begin);
			var reader = new StreamReader(stream);
			var text = reader.ReadToEnd();
			stream.Close();
			return text;
		}

		/// <summary>
		/// Convert the DataNode into a binary array of specified type.
		/// </summary>

		public byte[] ToArray (SaveType type = SaveType.Binary)
		{
			var stream = new MemoryStream();

			if (type == SaveType.Text)
			{
				var writer = new StreamWriter(stream);
				Write(writer);
			}
			else
			{
				var writer = new BinaryWriter(stream);
				Write(writer, type == SaveType.Compressed);
			}

			var data = stream.ToArray();
			stream.Close();
			return data;
		}
		#endregion
		#region Private Functions

		/// <summary>
		/// Read this node and all of its children from the stream reader.
		/// </summary>

		string Read (TextReader reader, string line, ref int offset)
		{
			if (line != null)
			{
				int expected = offset;
				int divider = line.IndexOf("=", expected);

				if (divider == -1)
				{
					name = Serialization.Unescape(line.Substring(offset)).Trim();
					value = null;
				}
				else
				{
					name = Serialization.Unescape(line.Substring(offset, divider - offset)).Trim();
					mValue = Serialization.Unescape(line.Substring(divider + 1)).Trim();
					mResolved = false;
				}

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

		/// <summary>
		/// Process the string values, converting them to proper objects.
		/// Returns whether child nodes should be processed in turn.
		/// </summary>

		public bool ResolveValue (Type type = null)
		{
			if (mValue is string)
			{
				mResolved = true;
				string line = mValue as string;

				// Trim strings wrapped in quotes
				if (type == typeof(string))
				{
					if (line == "\"\"")
					{
						mValue = "";
					}
					else
					{
						int len = line.Length;
						if (len > 2 && line[0] == '"' && line[len - 1] == '"')
							mValue = line.Substring(1, len - 2);
					}
					return true;
				}

				// Try to resolve this type as a simple type
				if (Serialization.ReadObject(line, out mValue, type)) return true;

				// This type is either a class or an array
				if (type == null) type = Serialization.NameToType(line);

				if (type == null || type == typeof(void))
				{
					mValue = null;
					return true;
				}
				else if (type == typeof(DataNode))
				{
					mValue = children.buffer[0];
					children.Clear();
					return false;
				}
				else if (type.Implements(typeof(IDataNodeSerializable)))
				{
					var ds = (IDataNodeSerializable)type.Create();
					ds.Deserialize(this);
					mValue = ds;
					return false;
				}
#if SERIALIZATION_WITHOUT_INTERFACE
				else if (type.HasDataNodeSerialization())
				{
					mValue = type.Create();
					mValue.Invoke("Deserialize", this);
					mValue = TypeExtensions.invokedObject; // Failing to do this will break structs. See note in the Invoke() function.
					return false;
				}
#endif
#if !STANDALONE
				else if (type == typeof(AnimationCurve)) // NOTE: This is no longer used since AnimationCurves get serialized out as Vector4 arrays.
				{
#if UNITY_EDITOR
					Debug.Log("Still used");
#endif
					if (children.size != 0)
					{
						var cv = new AnimationCurve();
						var kfs = new Keyframe[children.size];

						for (int i = 0; i < children.size; ++i)
						{
							var child = children.buffer[i];

							if (child.value == null)
							{
								child.mValue = child.name;
								child.mResolved = false;
								child.ResolveValue(typeof(Vector4));

								var v = (Vector4)child.mValue;
								kfs[i] = new Keyframe(v.x, v.y, v.z, v.w);
							}
							else
							{
								var v = (Vector4)child.mValue;
								kfs[i] = new Keyframe(v.x, v.y, v.z, v.w);
							}
						}

						cv.keys = kfs;
						mValue = cv;
						children.Clear();
					}
					return false;
				}
				else if (type == typeof(LayerMask))
				{
					mValue = (LayerMask)Get<int>();
				}
#endif
				else
#if !STANDALONE
				if (!type.IsSubclassOf(typeof(Component)))
#endif
				{
					bool isIList = type.Implements(typeof(System.Collections.IList));
					bool isTList = (!isIList && type.Implements(typeof(TList)));
					mValue = (isTList || isIList) ? type.Create(children.size) : type.Create();

					if (mValue == null)
					{
						Tools.LogError("Unable to create a " + type);
						return true;
					}

					if (isTList)
					{
						TList list = mValue as TList;
						Type elemType = type.GetGenericArgument();

						if (elemType != null)
						{
							for (int i = 0; i < children.size; ++i)
							{
								var child = children.buffer[i];

								if (child.value == null)
								{
									child.mValue = child.name;
									child.mResolved = false;
									child.ResolveValue(elemType);
									list.Add(child.mValue);
								}
								else if (child.name == "Add")
								{
									child.ResolveValue(elemType);
									list.Add(child.mValue);
								}
								else Tools.LogError("Unexpected node in an array: " + child.name);
							}
							return false;
						}
						else Tools.LogError("Unable to determine the element type of " + type);
					}
					else if (isIList)
					{
						// This is for both List<Type> and Type[] arrays.
						System.Collections.IList list = mValue as System.Collections.IList;
						Type elemType = type.GetGenericArgument();
						if (elemType == null) elemType = type.GetElementType();
						bool fixedSize = (list.Count == children.size);

						if (elemType != null)
						{
							for (int i = 0; i < children.size; ++i)
							{
								var child = children.buffer[i];

								if (child.value == null)
								{
									child.mValue = child.name;
									child.mResolved = false;
									child.ResolveValue(elemType);

									if (fixedSize) list[i] = child.mValue;
									else list.Add(child.mValue);
								}
								else if (child.name == "Add")
								{
									child.ResolveValue(elemType);
									if (fixedSize) list[i] = child.mValue;
									else list.Add(child.mValue);
								}
								else Tools.LogError("Unexpected node in an array: " + child.name);
							}
							return false;
						}
						else Tools.LogError("Unable to determine the element type of " + type);
					}
					else
					{
						for (int i = 0; i < children.size; ++i)
						{
							var child = children.buffer[i];
							mValue.SetFieldOrPropertyValue(child.name, child.value);
						}
						return false;
					}
				}
				return true;
			}
			return true;
		}

#endregion
#region Static Helper Functions

		/// <summary>
		/// Get the next line from the stream reader.
		/// </summary>

		static string GetNextLine (TextReader reader)
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
#endregion

		/// <summary>
		/// Sum up the hashes of the entire DataNode tree to use for quick validation.
		/// </summary>

		public int CalculateHash () { return CalculateHash(this); }

		static int CalculateHash (DataNode node)
		{
			var hash = node.name.GetHashCode();
			if (node.value != null) hash += node.value.GetHashCode();
			for (int i = 0; i < node.children.size; ++i) hash += CalculateHash(node.children.buffer[i]);
			return hash;
		}
	}
}
