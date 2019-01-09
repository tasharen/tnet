//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

#if UNITY_EDITOR || !UNITY_FLASH
#define REFLECTION_SUPPORT
#endif

using System;
using System.Collections.Generic;
using System.Reflection;

namespace TNet
{
	/// <summary>
	/// Can be used to mark fields as ignored by TNet-based serialization.
	/// </summary>

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
	public sealed class IgnoredByTNet : Attribute { public IgnoredByTNet () { } }

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
	public sealed class SerializeProperties : Attribute { public SerializeProperties () { } }

	/// <summary>
	/// Static helper class containing useful extensions for the System.Type class.
	/// </summary>

	static public class TypeExtensions
	{
		/// <summary>
		/// Not sure why this isn't already present...
		/// </summary>

		static public bool IsStruct (this Type type) { return type.IsValueType && !type.IsEnum; }

		/// <summary>
		/// Helper extension that returns 'true' if the type implements the specified interface.
		/// </summary>

		static public bool Implements (this Type t, Type interfaceType)
		{
			if (interfaceType == null) return false;
			return interfaceType.IsAssignableFrom(t);
		}

		/// <summary>
		/// Retrieve the generic element type from the templated type.
		/// </summary>

		static public Type GetGenericArgument (this Type type)
		{
			Type[] elems = type.GetGenericArguments();
			return (elems != null && elems.Length == 1) ? elems[0] : null;
		}

		/// <summary>
		/// Create a new instance of the specified object.
		/// </summary>

		static public object Create (this Type type)
		{
			if (type != null)
			{
				try
				{
					return Activator.CreateInstance(type);
				}
				catch (Exception ex)
				{
					var p = GameServer.currentPlayer;
					if (p != null) p.LogError(ex.Message, ex.StackTrace, true);
					else Tools.LogError(ex.Message);
				}
			}
			return default(Type);
		}

		/// <summary>
		/// Create a new instance of the specified object.
		/// </summary>

		static public object Create (this Type type, int size)
		{
			try
			{
				return Activator.CreateInstance(type, size);
			}
			catch (Exception)
			{
				return type.Create();
			}
		}

#if REFLECTION_SUPPORT
		/// <summary>
		/// Cached method details used by the CachedType class.
		/// </summary>

		public class CachedMethod
		{
			public string name;
			public MethodInfo method;
			public ParameterInfo[] parameters;
			public Type[] paramTypes;
		}

		/// <summary>
		/// Reflection doesn't store data retrieved once. This class does.
		/// Retrieving fields and methods via this class causes no garbage collection past the first time, and is obviously a lot faster.
		/// </summary>

		public class CachedType
		{
			public Type type;
			public string name;

			List<FieldInfo> mFieldDict;
			List<PropertyInfo> mPropDict;
			Dictionary<BindingFlags, List<CachedMethod>> mMethods;
			Dictionary<string, FieldInfo> mSerFieldCache;
			Dictionary<string, PropertyInfo> mSerPropCache;

			/// <summary>
			/// Get all methods of specified type.
			/// </summary>

			public List<CachedMethod> GetMethods (BindingFlags flags)
			{
				if (mMethods == null) mMethods = new Dictionary<BindingFlags, List<CachedMethod>>();

				List<CachedMethod> list = null;

				if (!mMethods.TryGetValue(flags, out list))
				{
					list = new List<CachedMethod>();
					var methods = type.GetMethods(flags);

					for (int im = 0, imm = methods.Length; im < imm; ++im)
					{
						var cmi = new CachedMethod();
						cmi.method = methods[im];
						cmi.name = cmi.method.Name;
						cmi.parameters = cmi.method.GetParameters();

						var c = cmi.parameters.Length;
						cmi.paramTypes = new Type[c];
						for (int i = 0; i < c; ++i) cmi.paramTypes[i] = cmi.parameters[i].ParameterType;

						list.Add(cmi);
					}

					mMethods[flags] = list;
				}
				return list;
			}

			public MethodInfo GetMethod (string name, params Type[] paramTypes)
			{
				return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, paramTypes, null);
			}

			/// <summary>
			/// Collect all serializable fields on the class of specified type.
			/// </summary>

			public List<FieldInfo> GetSerializableFields ()
			{
				if (mFieldDict == null)
				{
					mFieldDict = new List<FieldInfo>();

					var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					//bool serializable = type.IsDefined(typeof(SerializableAttribute), true);

					for (int i = 0, imax = fields.Length; i < imax; ++i)
					{
						var field = fields[i];

						// Don't do anything with static fields
						if ((field.Attributes & FieldAttributes.Static) != 0) continue;
						if (field.IsDefined(typeof(IgnoredByTNet), true)) continue;
#if !STANDALONE
						// Ignore fields that were not marked as serializable
						if (!field.IsDefined(typeof(UnityEngine.SerializeField), true))
#endif
						{
							// Class is not serializable
							if (!field.IsPublic) continue;
						}

						// Ignore fields that were marked as non-serializable
						if (field.IsDefined(typeof(System.NonSerializedAttribute), true)) continue;

						// It's a valid serializable field
						mFieldDict.Add(field);
					}
				}
				return mFieldDict;
			}

			/// <summary>
			/// Retrieve the specified serializable field from the type. Returns 'null' if the field was not found or if it's not serializable.
			/// </summary>

			public FieldInfo GetSerializableField (string name)
			{
				if (mSerFieldCache == null) mSerFieldCache = new Dictionary<string, FieldInfo>();
				FieldInfo field = null;

				if (!mSerFieldCache.TryGetValue(name, out field))
				{
					var list = type.GetSerializableFields();

					for (int i = 0, imax = list.size; i < imax; ++i)
					{
						if (list.buffer[i].Name == name)
						{
							field = list.buffer[i];
							break;
						}
					}
					mSerFieldCache[name] = field;
				}
				return field;
			}

			/// <summary>
			/// Collect all serializable properties on the class of specified type.
			/// </summary>

			public List<PropertyInfo> GetSerializableProperties ()
			{
				if (mPropDict == null)
				{
					mPropDict = new List<PropertyInfo>();

					var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

					for (int i = 0, imax = props.Length; i < imax; ++i)
					{
						var prop = props[i];
						if (!prop.CanRead || !prop.CanWrite) continue;

						if (prop.IsDefined(typeof(System.ObsoleteAttribute), true)) continue;
						if (prop.IsDefined(typeof(IgnoredByTNet), true)) continue;

						// It's a valid serializable property
						mPropDict.Add(prop);
					}
				}
				return mPropDict;
			}

			/// <summary>
			/// Retrieve the specified serializable property from the type.
			/// Returns 'null' if the property was not found or if it's not serializable.
			/// A serializable property must have both a getter and a setter.
			/// </summary>

			public PropertyInfo GetSerializableProperty (string name)
			{
				if (mSerPropCache == null) mSerPropCache = new Dictionary<string, PropertyInfo>();

				PropertyInfo prop = null;

				if (!mSerPropCache.TryGetValue(name, out prop))
				{
					var list = type.GetSerializableProperties();

					for (int i = 0, imax = list.size; i < imax; ++i)
					{
						if (list.buffer[i].Name == name)
						{
							prop = list.buffer[i];
							break;
						}
					}
					mSerPropCache[name] = prop;
				}
				return prop;
			}
		}

		class ExtesionType
		{
			public MethodInfo method;
			public Type[] paramTypes;
		}

		static Dictionary<string, Dictionary<Type, List<ExtesionType>>> mCache =
			new Dictionary<string, Dictionary<Type, List<ExtesionType>>>();

		/// <summary>
		/// Retrieve a specific extension method for the type that matches the function parameters.
		/// Each result gets cached, so subsequent calls are going to be much faster and won't cause any GC allocation.
		/// </summary>

		static public MethodInfo GetMethodOrExtension (this Type type, string name, Type paramType)
		{
			Dictionary<Type, List<ExtesionType>> cachedMethod;

			if (!mCache.TryGetValue(name, out cachedMethod) || cachedMethod == null)
			{
				cachedMethod = new Dictionary<Type, List<ExtesionType>>();
				mCache.Add(name, cachedMethod);
			}

			List<ExtesionType> cachedList = null;

			if (!cachedMethod.TryGetValue(type, out cachedList) || cachedList == null)
			{
				cachedList = new List<ExtesionType>();
				cachedMethod.Add(type, cachedList);
			}

			for (int b = 0; b < cachedList.size; ++b)
			{
				var item = cachedList.buffer[b];
				if (item.paramTypes.Length == 1 && item.paramTypes[0] == paramType) return item.method;
			}

			var paramTypes = new Type[] { paramType };
			var ci = new ExtesionType();
			ci.method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, paramTypes, null);
			if (ci.method == null) ci.method = type.GetExtensionMethod(name, paramTypes);
			ci.paramTypes = paramTypes;
			cachedList.Add(ci);
			return ci.method;
		}

		/// <summary>
		/// Retrieve a specific extension method for the type that matches the function parameters.
		/// Each result gets cached, so subsequent calls are going to be much faster and won't cause any GC allocation.
		/// </summary>

		static public MethodInfo GetMethodOrExtension (this Type type, string name, params Type[] paramTypes)
		{
			Dictionary<Type, List<ExtesionType>> cachedMethod;

			if (!mCache.TryGetValue(name, out cachedMethod) || cachedMethod == null)
			{
				cachedMethod = new Dictionary<Type, List<ExtesionType>>();
				mCache.Add(name, cachedMethod);
			}

			List<ExtesionType> cachedList = null;

			if (!cachedMethod.TryGetValue(type, out cachedList) || cachedList == null)
			{
				cachedList = new List<ExtesionType>();
				cachedMethod.Add(type, cachedList);
			}

			for (int b = 0; b < cachedList.size; ++b)
			{
				var item = cachedList.buffer[b];
				bool isValid = true;

				if (item.paramTypes != paramTypes)
				{
					if (item.paramTypes.Length == paramTypes.Length)
					{
						for (int i = 0, imax = item.paramTypes.Length; i < imax; ++i)
						{
							if (item.paramTypes[i] != paramTypes[i])
							{
								isValid = false;
								break;
							}
						}
					}
					else isValid = false;
				}
				if (isValid) return item.method;
			}

			var ci = new ExtesionType();
			ci.method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, paramTypes, null);
			if (ci.method == null) ci.method = type.GetExtensionMethod(name, paramTypes);
			ci.paramTypes = paramTypes;
			cachedList.Add(ci);
			return ci.method;
		}

		[System.NonSerialized] static List<CachedType> mCachedTypes = null;
		[System.NonSerialized] static Dictionary<Type, CachedType> mTypeDict = null;
		[System.NonSerialized] static Dictionary<string, Type> mTypeLookup = null;
		[System.NonSerialized] static Dictionary<Assembly, Type[]> mAssemblyTypes = null;
		[System.NonSerialized] static List<Assembly> mFullAssemblyList = null;
		[System.NonSerialized] static List<Assembly> mRefinedAssemblyList = null;
		[System.NonSerialized] static int mHash = 0;
		[System.NonSerialized] static bool mHashIsValid = false;

		static void CacheTypes ()
		{
			if (mFullAssemblyList == null) mFullAssemblyList = new List<Assembly>();
			else mFullAssemblyList.Clear();

			if (mRefinedAssemblyList == null) mRefinedAssemblyList = new List<Assembly>();
			else mRefinedAssemblyList.Clear();

			var currentAssemblies = System.AppDomain.CurrentDomain.GetAssemblies();

			mAssemblyTypes = new Dictionary<Assembly, Type[]>();
			mCachedTypes = new List<CachedType>();
			mTypeDict = new Dictionary<Type, CachedType>();
			mTypeLookup = new Dictionary<string, Type>();

			foreach (Assembly asm in currentAssemblies)
			{
				var fn = asm.FullName;
				if (fn.Contains("VisualStudio")) continue;

				mFullAssemblyList.Add(asm, true);

				// We don't need to cache types for any of these DLLs
				if (fn.StartsWith("Mono.")) continue;
				if (fn.StartsWith("System.CodeDom")) continue;
				if (fn.StartsWith("Microsoft.CSharp")) continue;
				if (fn.Contains("UnityEditor")) continue;
				if (fn.Contains("Cecil")) continue;
				if (fn.StartsWith("Boo.Lang")) continue;
				if (fn.Contains("UnityScript")) continue;
				if (fn.StartsWith("Unity.")) continue;
				if (fn.StartsWith("ICSharpCode.")) continue;
				if (fn.StartsWith("System.Xml.Linq")) continue;
				if (fn.StartsWith("obfuscator")) continue;
				if (fn.StartsWith("nunit.")) continue;
				if (fn.StartsWith("Assembly-CSharp-Editor")) continue;
				if (fn.StartsWith("I18N")) continue;
				if (fn.StartsWith("System.Xml")) continue;
				if (fn.StartsWith("System.Configuration")) continue;
				if (fn.StartsWith("System.Core")) continue;
				if (fn.StartsWith("P2T")) continue;
				if (fn.StartsWith("mscorlib")) continue;

				AddAssembly(asm);
			}
		}

		static bool CacheTypes (Assembly asm)
		{
			if (asm == null) return false;

			try
			{
				var types = asm.GetTypes();

				foreach (Type t in types)
				{
					var ent = new CachedType();
					ent.type = t;
					ent.name = t.ToString().Replace("UnityEngine.", "");
					mTypeLookup[ent.name] = t;
					mTypeDict[t] = ent;
					mCachedTypes.Add(ent);
				}

				mAssemblyTypes[asm] = types;
				return true;
			}
#if STANDALONE
			catch (Exception) {}
#else
			catch (Exception ex)
			{
				UnityEngine.Debug.Log(asm.FullName + "\n" + ex.Message + ex.StackTrace.Replace("\n\n", "\n"));
			}
#endif
			return false;
		}

		/// <summary>
		/// Add a new assembly (and cache all of its types) to be used by the application. This can be a runtime-compiled assembly.
		/// </summary>

		static public bool AddAssembly (Assembly asm)
		{
			if (mFullAssemblyList == null) CacheTypes();

			if (CacheTypes(asm))
			{
				mFullAssemblyList.Add(asm, true);
				mRefinedAssemblyList.Add(asm, true);
				mHashIsValid = false;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Remove a previously added assembly and all of its types.
		/// </summary>

		static public void RemoveAssembly (Assembly asm)
		{
			if (asm == null || mFullAssemblyList == null) return;

			try
			{
				mAssemblyTypes.Remove(asm);
				var types = asm.GetTypes();

				foreach (Type t in types)
				{
					for (int i = 0; i < mCachedTypes.size; ++i)
					{
						var ent = mCachedTypes.buffer[i];

						if (ent.type == t)
						{
							mCachedTypes.RemoveAt(i--);
							mTypeLookup.Remove(ent.name);
							mTypeDict.Remove(t);
							mHashIsValid = false;
						}
					}
				}
			}
#if STANDALONE
			catch (Exception) {}
#else
			catch (Exception ex)
			{
				UnityEngine.Debug.Log(asm.FullName + "\n" + ex.Message + ex.StackTrace.Replace("\n\n", "\n"));
			}
#endif
		}

		/// <summary>
		/// Return a hash generated from the currently loaded assemblies. Can be used to uniquely fingerprint mods or know when the assembly list has changed.
		/// </summary>

		static public int assemblyHash
		{
			get
			{
				if (!mHashIsValid)
				{
					if (mFullAssemblyList == null) CacheTypes();

					for (int i = 0; i < mFullAssemblyList.size; ++i)
					{
						var asm = mFullAssemblyList.buffer[i];
						var fn = asm.FullName;
						if (!string.IsNullOrEmpty(fn)) mHash += fn.GetHashCode();
					}

					mHashIsValid = true;
				}
				return mHash;
			}
		}

		/// <summary>
		/// Get the cached list of currently loaded assemblies.
		/// </summary>

		static public Assembly[] GetAssemblies (bool full = true)
		{
			if (mFullAssemblyList == null) CacheTypes();

			if (full)
			{
				mFullAssemblyList.Trim();
				return mFullAssemblyList.buffer;
			}
			else
			{
				mRefinedAssemblyList.Trim();
				return mRefinedAssemblyList.buffer;
			}
		}

		/// <summary>
		/// Get the cached list of currently loaded types.
		/// </summary>

		static public List<CachedType> GetTypes ()
		{
			if (mCachedTypes == null) CacheTypes();
			return mCachedTypes;
		}

		/// <summary>
		/// Get cached type data.
		/// </summary>

		static public CachedType GetCache (this Type type)
		{
			if (mTypeDict == null) CacheTypes();
			CachedType ent = null;

			if (!mTypeDict.TryGetValue(type, out ent))
			{
				ent = new CachedType();
				ent.type = type;
				ent.name = ent.ToString();
				mCachedTypes.Add(ent);
				mTypeDict[type] = ent;
			}
			return ent;
		}

		/// <summary>
		/// Resolve a type by its name.
		/// </summary>

		static public Type GetType (string name)
		{
			if (mTypeLookup == null) CacheTypes();
			Type type;
			if (mTypeLookup.TryGetValue(name, out type)) return type;
			return null;
		}

		/// <summary>
		/// Handy method that returns the type of the object. Calling GetType() on it only works if it's not null, but this method will work even if it's null.
		/// </summary>

		static public Type GetDeclaredType<T> (this T target) { return typeof(T); }

		/// <summary>
		/// Convenience function that retrieves a public or private method with specified parameters.
		/// </summary>

		static public MethodInfo GetMethod (this Type type, string name, params Type[] paramTypes)
		{
			return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, paramTypes, null);
		}

		/// <summary>
		/// Convenience function that retrieves a public or private method with specified parameters.
		/// </summary>

		static public MethodInfo GetMethod (this object target, string name, params Type[] paramTypes)
		{
			return target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, paramTypes, null);
		}

		/// <summary>
		/// Get the specified method converted into a delegate.
		/// </summary>

		static public T GetMethod<T> (this object target, string methodName)
		{
			try
			{
				var del = Delegate.CreateDelegate(typeof(T), target, methodName);
				if (del != null) return (T)Convert.ChangeType(del, typeof(T));
			}
#if UNITY_EDITOR
			catch (Exception ex) { UnityEngine.Debug.LogError(ex.GetType() + ": " + ex.Message); }
#else
			catch (Exception) {}
#endif
			return default(T);
		}

		/// <summary>
		/// Convenience function that retrieves an extension method with specified parameters.
		/// </summary>

		static public MethodInfo GetExtensionMethod (this Type type, string name, params Type[] paramTypes)
		{
			if (mCachedTypes == null) CacheTypes();

			for (int b = 0; b < mCachedTypes.size; ++b)
			{
				var t = mCachedTypes.buffer[b];
				var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

				for (int im = 0, imm = methods.Count; im < imm; ++im)
				{
					var m = methods.buffer[im];
					if (m.name != name) continue;

					var pts = m.parameters;

					if (pts.Length != paramTypes.Length + 1) continue;
					if (pts[0].ParameterType != type) continue;

					bool isValid = true;

					for (int i = 0; i < paramTypes.Length; ++i)
					{
						if (pts[i + 1].ParameterType != paramTypes[i])
						{
							isValid = false;
							break;
						}
					}

					if (isValid) return m.method;
				}
			}
			return null;
		}

		/// <summary>
		/// Convenience function that checks if the specified type has the desired function with the desired arguments.
		/// </summary>

		static public bool HasFunc (this Type type, string methodName, Type paramType)
		{
			return type.GetMethodOrExtension(methodName, paramType) != null;
		}

		/// <summary>
		/// Convenience function that checks if the specified type has the desired function with the desired arguments.
		/// </summary>

		static public bool HasFunc (this Type type, string methodName, params Type[] paramTypes)
		{
			return type.GetMethodOrExtension(methodName, paramTypes) != null;
		}

		[System.NonSerialized] static public Dictionary<Type, bool> mHasDataNodeSerialization = new Dictionary<Type, bool>();
		[System.NonSerialized] static public Dictionary<Type, bool> mHasBinarySerialization = new Dictionary<Type, bool>();

		/// <summary>
		/// Whether this type has DataNode serialization functions.
		/// </summary>

		static public bool HasDataNodeSerialization (this Type type)
		{
			bool val;
			if (mHasDataNodeSerialization.TryGetValue(type, out val)) return val;
			val = type.HasFunc("Serialize", typeof(DataNode)) && type.HasFunc("Deserialize", typeof(DataNode));
			mHasDataNodeSerialization[type] = val;
			return val;
		}

		/// <summary>
		/// Whether this type has BinaryWriter/BinaryReader-based serialization functions.
		/// </summary>

		static public bool HasBinarySerialization (this Type type)
		{
			bool val;
			if (mHasDataNodeSerialization.TryGetValue(type, out val)) return val;
			val = type.HasFunc("Serialize", typeof(System.IO.BinaryWriter)) && type.HasFunc("Deserialize", typeof(System.IO.BinaryReader));
			mHasDataNodeSerialization[type] = val;
			return val;
		}

		/// <summary>
		/// Due to limitation of structs always being passed by copy rather than by reference, it's normally not possible to implement
		/// serialization function extensions for structs because 'this' will be a copy. Due to this limitation, 'invokedObject' value
		/// must be set to the 'this' object at the end of the invoked serialization function.
		/// </summary>

		[System.NonSerialized]
		static public object invokedObject;

		[System.NonSerialized] static object[] mTemp;
		[System.NonSerialized] static object[] mTempExtended;

		/// <summary>
		/// Convenience function that will invoke the specified method or extension, if possible. Return value will be 'true' if successful.
		/// </summary>

		static public bool Invoke (this Type type, string methodName, params object[] parameters)
		{
			var types = new Type[parameters.Length];
			for (int i = 0, imax = parameters.Length; i < imax; ++i)
				types[i] = parameters[i].GetType();

			var mi = type.GetMethodOrExtension(methodName, types);
			if (mi == null) return false;

			// Extension methods need to pass the object as the first parameter ('this' reference)
			if (mi.IsStatic && mi.ReflectedType != type)
			{
				var extended = new object[parameters.Length + 1];
				extended[0] = null;
				for (int i = 0, imax = parameters.Length; i < imax; ++i) extended[i + 1] = parameters[i];
				mi.Invoke(null, extended);
				return true;
			}

			mi.Invoke(null, parameters);
			return true;
		}

		/// <summary>
		/// Convenience function that will invoke the specified method or extension, if possible. Return value will be 'true' if successful.
		/// </summary>

		static public bool Invoke (this object obj, string methodName, params object[] parameters)
		{
			if (obj == null) return false;

			invokedObject = obj;

			var type = obj.GetType();
			var types = new Type[parameters.Length];
			for (int i = 0, imax = parameters.Length; i < imax; ++i)
				types[i] = parameters[i].GetType();

			var mi = type.GetMethodOrExtension(methodName, types);
			if (mi == null) return false;

			// Extension methods need to pass the object as the first parameter ('this' reference)
			if (mi.IsStatic && mi.ReflectedType != type)
			{
				var extended = new object[parameters.Length + 1];
				extended[0] = obj;
				for (int i = 0, imax = parameters.Length; i < imax; ++i) extended[i + 1] = parameters[i];

				// NOTE: If 'obj' is a struct, any changes to the 'obj' done inside the invocation will not propagate outside that function.
				// It's likely tied to the limitation of structs always being passed by copy. Due to this, the invoked function MUST set
				// TypeExtensions.invokedObject to the final object's value ('this') before exiting the scope.
				mi.Invoke(obj, extended);
				return true;
			}

			mi.Invoke(obj, parameters);
			return true;
		}

		/// <summary>
		/// Convenience function that will invoke the specified method or extension, if possible. Return value will be 'true' if successful.
		/// This is the faster version of Invoke() that accepts arbitrary parameters for when only one parameter is needed, such as for serialization.
		/// </summary>

		static public bool Invoke (this object obj, string methodName, object arg)
		{
			if (obj == null) return false;

			invokedObject = obj;

			var type = obj.GetType();
			var mi = type.GetMethodOrExtension(methodName, arg.GetType());

			if (mi == null) return false;

			// Extension method needs to pass the object as the first parameter ('this' reference)
			if (mi.IsStatic && mi.ReflectedType != type)
			{
				if (mTempExtended == null) mTempExtended = new object[2];
				mTempExtended[0] = obj;
				mTempExtended[1] = arg;

				// NOTE: If 'obj' is a struct, any changes to the 'obj' done inside the invocation will not propagate outside that function.
				// It's likely tied to the limitation of structs always being passed by copy. Due to this, the invoked function MUST set
				// TypeExtensions.invokedObject to the final object's value ('this') before exiting the scope.
				mi.Invoke(obj, mTempExtended);
				return true;
			}

			if (mTemp == null) mTemp = new object[1];
			mTemp[0] = arg;
			mi.Invoke(obj, mTemp);
			return true;
		}

		/// <summary>
		/// Convenience function that will invoke the specified method or extension, if possible. Return value will be 'true' if successful.
		/// </summary>

		static public object InvokeGetResult (this object obj, string methodName, params object[] parameters)
		{
			if (obj == null) return null;

			invokedObject = obj;

			var type = obj.GetType();
			var types = new Type[parameters.Length];
			for (int i = 0, imax = parameters.Length; i < imax; ++i)
				types[i] = parameters[i].GetType();

			var mi = type.GetMethodOrExtension(methodName, types);
			if (mi == null) return null;

			// Extension methods need to pass the object as the first parameter ('this' reference)
			if (mi.IsStatic && mi.ReflectedType != type)
			{
				var extended = new object[parameters.Length + 1];
				extended[0] = obj;
				for (int i = 0, imax = parameters.Length; i < imax; ++i) extended[i + 1] = parameters[i];

				// NOTE: If 'obj' is a struct, any changes to the 'obj' done inside the invocation will not propagate outside that function.
				// It's likely tied to the limitation of structs always being passed by copy. Due to this, the invoked function MUST set
				// TypeExtensions.invokedObject to the final object's value ('this') before exiting the scope.
				return mi.Invoke(obj, extended);
			}

			return mi.Invoke(obj, parameters);
		}

		// Cached for speed
		//[System.NonSerialized] static Dictionary<Type, List<FieldInfo>> mFieldDict = new Dictionary<Type, List<FieldInfo>>();

		/// <summary>
		/// Collect all serializable fields on the class of specified type.
		/// </summary>

		static public List<FieldInfo> GetSerializableFields (this Type type)
		{
			return type.GetCache().GetSerializableFields();
		}

		//[System.NonSerialized] static Dictionary<Type, Dictionary<string, FieldInfo>> mSerFieldCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();

		/// <summary>
		/// Retrieve the specified serializable field from the type. Returns 'null' if the field was not found or if it's not serializable.
		/// </summary>

		static public FieldInfo GetSerializableField (this Type type, string name)
		{
			return type.GetCache().GetSerializableField(name);
		}

		// Cached for speed
		//[System.NonSerialized] static Dictionary<Type, List<PropertyInfo>> mPropDict = new Dictionary<Type, List<PropertyInfo>>();

		/// <summary>
		/// Collect all serializable properties on the class of specified type.
		/// </summary>

		static public List<PropertyInfo> GetSerializableProperties (this Type type)
		{
			return type.GetCache().GetSerializableProperties();
		}

		//[System.NonSerialized] static Dictionary<Type, Dictionary<string, PropertyInfo>> mSerPropCache = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

		/// <summary>
		/// Retrieve the specified serializable property from the type.
		/// Returns 'null' if the property was not found or if it's not serializable.
		/// A serializable property must have both a getter and a setter.
		/// </summary>

		static public PropertyInfo GetSerializableProperty (this Type type, string name)
		{
			return type.GetCache().GetSerializableProperty(name);
		}

#if NETFX_CORE
	// I have no idea why Microsoft decided to rename these...
	static public FieldInfo GetField (this Type type, string name) { return type.GetRuntimeField(name); }
	static public PropertyInfo GetProperty (this Type type, string name) { return type.GetRuntimeProperty(name); }
#endif
#endif
	}
}
