//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
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

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
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
		try
		{
			return Activator.CreateInstance(type);
		}
		catch (Exception ex)
		{
			Tools.LogError(ex.Message);
			return null;
		}
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
	class CacheItem
	{
		public MethodInfo method;
		public Type[] parameters;
	}

	static Dictionary<string, Dictionary<Type, List<CacheItem>>> mCache =
		new Dictionary<string, Dictionary<Type, List<CacheItem>>>();

	/// <summary>
	/// Retrieve a specific extension method for the type that matches the function parameters.
	/// Each result gets cached, so subsequent calls are going to be much faster and won't cause any GC allocation.
	/// </summary>

	static public MethodInfo GetMethodOrExtension (this Type type, string name, params Type[] paramTypes)
	{
		Dictionary<Type, List<CacheItem>> cachedMethod;

		if (!mCache.TryGetValue(name, out cachedMethod) || cachedMethod == null)
		{
			cachedMethod = new Dictionary<Type, List<CacheItem>>();
			mCache.Add(name, cachedMethod);
		}

		List<CacheItem> cachedList = null;

		if (!cachedMethod.TryGetValue(type, out cachedList) || cachedList == null)
		{
			cachedList = new List<CacheItem>();
			cachedMethod.Add(type, cachedList);
		}

		for (int b = 0; b < cachedList.size; ++b)
		{
			CacheItem item = cachedList[b];
			bool isValid = true;

			if (item.parameters != paramTypes)
			{
				if (item.parameters.Length == paramTypes.Length)
				{
					for (int i = 0, imax = item.parameters.Length; i < imax; ++i)
					{
						if (item.parameters[i] != paramTypes[i])
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
		
		CacheItem ci = new CacheItem();
		ci.method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, paramTypes, null);
		if (ci.method == null) ci.method = type.GetExtensionMethod(name, paramTypes);
		ci.parameters = paramTypes;
		cachedList.Add(ci);
		return ci.method;
	}

	static Assembly[] mCachedAssemblies = null;
	static List<Type> mCachedTypes = null;
	static List<string> mCachedTypeNames = null;
	static Dictionary<Assembly, Type[]> mAssemblyTypes = null;

	static void CacheTypes ()
	{
		mCachedAssemblies = System.AppDomain.CurrentDomain.GetAssemblies();

		List<Assembly> refined = new List<Assembly>();
		mAssemblyTypes = new Dictionary<Assembly, Type[]>();
		mCachedTypes = new List<Type>();
		mCachedTypeNames = new List<string>();

		foreach (Assembly asm in mCachedAssemblies)
		{
			if (asm.FullName.StartsWith("Mono.")) continue;

			try
			{
				Type[] tpl = asm.GetTypes();

				foreach (Type t in tpl)
				{
					mCachedTypes.Add(t);
					mCachedTypeNames.Add(t.ToString());
				}

				refined.Add(asm);
				mAssemblyTypes[asm] = tpl;
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

		mCachedAssemblies = refined.ToArray();
	}

	/// <summary>
	/// Get the cached list of currently loaded assemblies.
	/// </summary>

	static public Assembly[] GetAssemblies (bool update = false)
	{
		if (mCachedAssemblies == null || update) CacheTypes();
		return mCachedAssemblies;
	}

	/// <summary>
	/// Get the cached list of currently loaded types.
	/// </summary>

	static public List<Type> GetTypes (bool update = false)
	{
		if (mCachedTypes == null || update) CacheTypes();
		return mCachedTypes;
	}

	/// <summary>
	/// Get the cached list of currently loaded types.
	/// </summary>

	static public List<string> GetTypeNames (bool update = false)
	{
		if (mCachedTypeNames == null || update) CacheTypes();
		return mCachedTypeNames;
	}

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
			Type t = mCachedTypes[b];
			MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

			for (int im = 0, imm = methods.Length; im < imm; ++im)
			{
				MethodInfo m = methods[im];
				if (m.Name != name) continue;

				ParameterInfo[] pts = m.GetParameters();

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

				if (isValid) return m;
			}
		}
		return null;
	}

	/// <summary>
	/// Convenience function that will invoke the specified method or extension, if possible. Return value will be 'true' if successful.
	/// </summary>

	static public bool Invoke (this Type type, string methodName, params object[] parameters)
	{
		Type[] types = new Type[parameters.Length];
		for (int i = 0, imax = parameters.Length; i < imax; ++i)
			types[i] = parameters[i].GetType();

		MethodInfo mi = type.GetMethodOrExtension(methodName, types);
		if (mi == null) return false;

		// Extension methods need to pass the object as the first parameter ('this' reference)
		if (mi.IsStatic && mi.ReflectedType != type)
		{
			object[] extended = new object[parameters.Length + 1];
			extended[0] = null;
			for (int i = 0, imax = parameters.Length; i < imax; ++i)
				extended[i + 1] = parameters[i];

			// Note that if 'Type' is a struct, any changes to the 'obj' done inside the invocation
			// will not propagate outside that function. It seems to be a limitation of how the variable
			// is passed to the extension function (as a part of the 'extended array').
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

		Type type = obj.GetType();
		Type[] types = new Type[parameters.Length];
		for (int i = 0, imax = parameters.Length; i < imax; ++i)
			types[i] = parameters[i].GetType();

		MethodInfo mi = type.GetMethodOrExtension(methodName, types);
		if (mi == null) return false;

		// Extension methods need to pass the object as the first parameter ('this' reference)
		if (mi.IsStatic && mi.ReflectedType != type)
		{
			object[] extended = new object[parameters.Length + 1];
			extended[0] = obj;
			for (int i = 0, imax = parameters.Length; i < imax; ++i)
				extended[i + 1] = parameters[i];

			// Note that if 'Type' is a struct, any changes to the 'obj' done inside the invocation
			// will not propagate outside that function. It seems to be a limitation of how the variable
			// is passed to the extension function (as a part of the 'extended array').
			mi.Invoke(obj, extended);
			return true;
		}

		mi.Invoke(obj, parameters);
		return true;
	}

	// Cached for speed
	static Dictionary<Type, List<FieldInfo>> mFieldDict = new Dictionary<Type, List<FieldInfo>>();

	/// <summary>
	/// Collect all serializable fields on the class of specified type.
	/// </summary>

	static public List<FieldInfo> GetSerializableFields (this Type type, bool includePrivate = false)
	{
		List<FieldInfo> list = null;

		if (!mFieldDict.TryGetValue(type, out list) || list == null)
		{
			list = new List<FieldInfo>();
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			bool serializable = type.IsDefined(typeof(System.SerializableAttribute), true);

			for (int i = 0, imax = fields.Length; i < imax; ++i)
			{
				FieldInfo field = fields[i];

				// Don't do anything with static fields
				if ((field.Attributes & FieldAttributes.Static) != 0) continue;
				if (field.IsDefined(typeof(IgnoredByTNet), true)) continue;
#if !STANDALONE
				// Ignore fields that were not marked as serializable
				if (!field.IsDefined(typeof(UnityEngine.SerializeField), true))
#endif
				{
					// Class is not serializable
					if (!serializable) continue;

					// It's not a public field
					if (!includePrivate && (field.Attributes & FieldAttributes.Public) == 0) continue;
				}

				// Ignore fields that were marked as non-serializable
				if (field.IsDefined(typeof(System.NonSerializedAttribute), true)) continue;

				// It's a valid serializable field
				list.Add(field);
			}
			mFieldDict[type] = list;
		}
		return list;
	}

	static Dictionary<Type, Dictionary<string, FieldInfo>> mSerFieldCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();

	/// <summary>
	/// Retrieve the specified serializable field from the type. Returns 'null' if the field was not found or if it's not serializable.
	/// </summary>

	static public FieldInfo GetSerializableField (this Type type, string name)
	{
		Dictionary<string, FieldInfo> cache = null;

		if (!mSerFieldCache.TryGetValue(type, out cache) || cache == null)
		{
			cache = new Dictionary<string, FieldInfo>();
			mSerFieldCache.Add(type, cache);
		}

		FieldInfo field = null;

		if (!cache.TryGetValue(name, out field) || cache == null)
		{
			List<FieldInfo> list = type.GetSerializableFields();

			for (int i = 0, imax = list.size; i < imax; ++i)
			{
				if (list[i].Name == name)
				{
					field = list[i];
					break;
				}
			}
			cache[name] = field;
		}
		return field;
	}

	// Cached for speed
	static Dictionary<Type, List<PropertyInfo>> mPropDict = new Dictionary<Type, List<PropertyInfo>>();

	/// <summary>
	/// Collect all serializable properties on the class of specified type.
	/// </summary>

	static public List<PropertyInfo> GetSerializableProperties (this Type type, bool includePrivate = false)
	{
		List<PropertyInfo> list = null;

		if (!mPropDict.TryGetValue(type, out list) || list == null)
		{
			list = new List<PropertyInfo>();
			PropertyInfo[] props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

			for (int i = 0, imax = props.Length; i < imax; ++i)
			{
				PropertyInfo prop = props[i];
				if (!prop.CanRead || !prop.CanWrite) continue;

				if (prop.IsDefined(typeof(System.ObsoleteAttribute), true)) continue;
				if (prop.IsDefined(typeof(IgnoredByTNet), true)) continue;

				// It's a valid serializable property
				list.Add(prop);
			}
			mPropDict[type] = list;
		}
		return list;
	}

	static Dictionary<Type, Dictionary<string, PropertyInfo>> mSerPropCache = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

	/// <summary>
	/// Retrieve the specified serializable property from the type.
	/// Returns 'null' if the property was not found or if it's not serializable.
	/// A serializable property must have both a getter and a setter.
	/// </summary>

	static public PropertyInfo GetSerializableProperty (this Type type, string name)
	{
		Dictionary<string, PropertyInfo> cache = null;

		if (!mSerPropCache.TryGetValue(type, out cache) || cache == null)
		{
			cache = new Dictionary<string, PropertyInfo>();
			mSerPropCache.Add(type, cache);
		}

		PropertyInfo prop = null;

		if (!cache.TryGetValue(name, out prop))
		{
			List<PropertyInfo> list = type.GetSerializableProperties();

			for (int i = 0, imax = list.size; i < imax; ++i)
			{
				if (list[i].Name == name)
				{
					prop = list[i];
					break;
				}
			}
			cache[name] = prop;
		}
		return prop;
	}

#if NETFX_CORE
	// I have no idea why Microsoft decided to rename these...
	static public FieldInfo GetField (this Type type, string name) { return type.GetRuntimeField(name); }
	static public PropertyInfo GetProperty (this Type type, string name) { return type.GetRuntimeProperty(name); }
#endif
#endif
}
}
