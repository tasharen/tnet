//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using TNet;
using System.Collections;

/// <summary>
/// This script is attached to visible regions in the multi-channel example scene, and
/// is used both to show which channels the player is currently in (by coloring the renderer),
/// as well as to keep a list of regions that can be joined by the ExamplePlayerAvatar script.
/// </summary>

public class ExampleRegion : MonoBehaviour
{
	static public List<ExampleRegion> list = new List<ExampleRegion>();

	public int channelID = 0;

	void Start () { UpdateRenderer(); }

	void OnEnable ()
	{
		list.Add(this);
		TNManager.onJoinChannel += OnJoinChannel;
		TNManager.onLeaveChannel += OnLeaveChannel;
	}

	void OnDisable ()
	{
		list.Remove(this);
		TNManager.onJoinChannel -= OnJoinChannel;
		TNManager.onLeaveChannel -= OnLeaveChannel;
	}

	void OnJoinChannel (int channelID, bool success, string msg)
	{
		if (channelID == this.channelID) UpdateRenderer();
	}

	void OnLeaveChannel (int channelID)
	{
		if (channelID == this.channelID) UpdateRenderer();
	}

	void UpdateRenderer ()
	{
		Renderer ren = GetComponent<Renderer>();
		
		if (ren != null)
		{
			Color c = TNManager.IsInChannel(channelID) ? Color.green : Color.red;
			c.a = 0.25f;
			ren.material.color = c;
		}
	}
}
