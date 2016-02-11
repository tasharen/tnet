//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
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
	public RFC () { }
	public RFC (int rid) { id = rid; }
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
	public MethodInfo func;
	public ParameterInfo[] parameters;

	/// <summary>
	/// Execute this function with the specified number of parameters.
	/// </summary>

	public object Execute (params object[] pars)
	{
		if (func == null) return null;
		if (parameters == null)
			parameters = func.GetParameters();

		try
		{
			return (parameters.Length == 1 && parameters[0].ParameterType == typeof(object[])) ?
				func.Invoke(obj, new object[] { pars }) :
				func.Invoke(obj, pars);
		}
		catch (System.Exception ex)
		{
			if (ex.GetType() == typeof(System.NullReferenceException)) return null;
			UnityTools.PrintException(ex, this, 0, func.Name, pars);
			return null;
		}
	}
}
}
