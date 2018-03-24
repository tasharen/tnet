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

		ParameterInfo[] mParams;
		Type[] mTypes;
		int mParamCount = 0;
		bool mAutoCast = false;

		public ParameterInfo[] parameters
		{
			get
			{
				if (mParams == null)
				{
					if (mi == null) return null;
					mParams = mi.GetParameters();
					mParamCount = parameters.Length;
				}
				return mParams;
			}
		}

		/// <summary>
		/// Execute this function with the specified number of parameters.
		/// </summary>

		public object Execute (params object[] pars)
		{
			if (mi == null) return null;

			var parameters = this.parameters;
			if (parameters.Length == 1 && parameters[0].ParameterType == typeof(object[])) pars = new object[] { pars };

			try
			{
				if (mAutoCast)
				{
					for (int i = 0; i < mParamCount; ++i)
					{
						var passed = pars[i].GetType();
						if (mTypes[i] != passed) pars[i] = Serialization.CastValue(pars[i], mTypes[i]);
					}
				}
				return mi.Invoke(obj, pars);
			}
			catch (System.Exception ex)
			{
				if (ex.GetType() == typeof(System.NullReferenceException)) return null;

				if (mTypes == null)
				{
					mTypes = new Type[mParamCount];
					for (int i = 0; i < mParamCount; ++i) mTypes[i] = parameters[i].ParameterType;
				}

				var tryAgain = false;

				for (int i = 0; i < mParamCount; ++i)
				{
					var passed = pars[i].GetType();

					if (mTypes[i] != passed)
					{
						pars[i] = Serialization.CastValue(pars[i], mTypes[i]);
						if (pars[i] != null) tryAgain = true;
					}
				}

				if (tryAgain)
				{
					try
					{
						if (parameters.Length == 1 && parameters[0].ParameterType == typeof(object[])) pars = new object[] { pars };
						var retVal = mi.Invoke(obj, pars);
						mAutoCast = true;
						return retVal;
					}
					catch (System.Exception ex2) { ex = ex2; }
				}

				UnityTools.PrintException(ex, this, 0, mi.Name, pars);
				return null;
			}
		}
	}
}
