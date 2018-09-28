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
		/// <summary>
		/// Optional RFC ID, which should be in 1-254 range (inclusive). For example: [RFC(123)]. This is useful for frequent packets,
		/// as using tno.Send(123, ...) requires less bytes than tno.Send("RFC_name", ...) -- however in vast majority of the cases,
		/// it's not advisable to use IDs as it makes debugging more difficult just to save a few bytes per packet.
		/// </summary>

		public int id = 0;

		/// <summary>
		/// Name of the optional property that will be used to uniquely identify this RFC in addition to its name. This can be useful if you have
		/// multiple RFCs with an identical name underneath the same TNObject. For example, in Project 5: Sightseer, a vehicle contains multiple
		/// attachment points, with each attachment point having a "set installed item" RFC. This is done by giving all attachment points a unique
		/// identifier, ("uniqueID"), which is basically a public field set in inspector on the vehicle's prefab (but can also be a property).
		/// 
		/// RFCs then look like this:
		/// [RFC("uniqueID")] void MyRFC (...);
		/// 
		/// The syntax to send an RFC to a specific uniquely-identified child is like this:
		/// tno.Send("MyRFC/" + uniqueID, ...);
		/// </summary>

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
			if (pars == null && mParamCount != 0) pars = new object[parameters.Length];
			if (mParamCount == 1 && parameters[0].ParameterType == typeof(object[])) pars = new object[] { pars };

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
			catch (Exception ex)
			{
				if (ex.GetType() == typeof(NullReferenceException)) return null;

				var tryAgain = false;

				if (mParamCount == pars.Length)
				{
					if (mTypes == null)
					{
						mTypes = new Type[mParamCount];
						for (int i = 0; i < mParamCount; ++i) mTypes[i] = parameters[i].ParameterType;
					}

					for (int i = 0; i < mParamCount; ++i)
					{
						var passed = (pars[i] != null) ? pars[i].GetType() : mTypes[i];

						if (mTypes[i] != passed)
						{
							pars[i] = Serialization.CastValue(pars[i], mTypes[i]);
							if (pars[i] != null) tryAgain = true;
						}
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
					catch (Exception ex2) { ex = ex2; }
				}

				UnityTools.PrintException(ex, this, 0, mi.Name, pars);
				return null;
			}
		}
	}
}
