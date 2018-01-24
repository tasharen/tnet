//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.Reflection;

namespace TNet
{
/// <summary>
/// Remote Function Call attribute. Used to identify functions that are supposed to be executed remotely.
/// </summary>

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RFC : Attribute
{
	public int id = 0;
	public string property;

	public RFC (string property = null)
	{
		this.property = property;
	}

	public RFC (int rid)
	{
		id = rid;
		property = null;
	}

	public string GetUniqueID (object target)
	{
		if (string.IsNullOrEmpty(property)) return null;
		return target.GetFieldOrPropertyValue<string>(property);
	}
}

/// <summary>
/// Remote Creation Call attribute. Used to identify functions that are supposed to executed when custom OnCreate packets arrive.
/// </summary>

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RCC : System.Attribute
{
	public int id = 0;
	public RCC () { }
	public RCC (int rid) { id = rid; }
}

/// <summary>
/// Functions gathered via reflection get cached along with their object references and expected parameter types.
/// </summary>

public class CachedFunc
{
	public object obj = null;
	public MethodInfo mi;
	public ParameterInfo[] parameters;

	/// <summary>
	/// Execute this function with the specified number of parameters.
	/// </summary>

	public object Execute (params object[] pars)
	{
		if (mi == null) return null;
		if (parameters == null)
			parameters = mi.GetParameters();

		try
		{
			return (parameters.Length == 1 && parameters[0].ParameterType == typeof(object[])) ?
				mi.Invoke(obj, new object[] { pars }) :
				mi.Invoke(obj, pars);
		}
		catch (System.Exception ex)
		{
			if (ex.GetType() == typeof(System.NullReferenceException)) return null;
			UnityTools.PrintException(ex, this, 0, mi.Name, pars);
			return null;
		}
	}
}
}
