//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;

namespace TNet
{
/// <summary>
/// Attribute used to identify remotely called functions.
/// </summary>

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RFC : Attribute
{
	public int id = 0;
	public RFC () { }
	public RFC (int rid) { id = rid; }
}
}