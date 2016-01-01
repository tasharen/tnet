//------------------------------------------
//            Tasharen Network
// Copyright © 2012-2016 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// Instantiated objects can be easily transferred from one channel to another.
/// The most obvious usage for this would be moving player avatars or NPCs from one region to another.
/// If a player is present in the region the object gets transferred to, they won't see any changes.
/// If the player is not present, the object will simply act as if it was destroyed. For players
/// that are present in the region it's transferred to that are not present in the region the object
/// was transferred from, the object will simply be created and all of its RFCs will be executed
/// as if it was loaded from a save file.
/// </summary>

[ExecuteInEditMode]
public class ExampleTransfer : MonoBehaviour
{
	int mChannelID = 0;

	void Start ()
	{
		mChannelID = TNManager.lastChannelID;
	}

	void OnGUI ()
	{
		int chan1 = mChannelID;
		int chan2 = mChannelID + 1;
		Rect rect = new Rect(Screen.width * 0.5f - 200f * 0.5f, Screen.height * 0.5f - 100f, 200f, 220f);

		GUILayout.BeginArea(rect);
		{
			string channels = null;
			List<Channel> list = TNManager.channels;

			if (list != null)
			{
				foreach (Channel ch in list)
				{
					if (string.IsNullOrEmpty(channels)) channels = ch.id.ToString();
					else channels += ", " + ch.id;
				}
			}

			GUI.color = Color.black;
			GUILayout.Label("Subscribed channel list: " + (channels ?? "none"));
			GUI.color = Color.white;

			// Create a new cube in a random position
			if (GUILayout.Button("Create a Cube in Channel #" + chan1))
			{
				Vector3 pos = new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
				TNManager.Create(chan1, "AutoSync Cube", pos, Quaternion.identity, true);
			}

			// Transfer one of the cubes to another channel. If there is a player in that channel, they will see the cube appear.
			if (GUILayout.Button("Transfer a Cube to Channel #" + chan1))
			{
				TNObject[] tnos = FindObjectsOfType<TNObject>();

				foreach (TNObject tno in tnos)
				{
					if (tno.isMine && tno.channelID == chan2)
					{
						tno.TransferToChannel(chan1);
						break;
					}
				}
			}

			// Transfer one of the cubes to another channel. If there is a player in that channel, they will see the cube appear.
			if (GUILayout.Button("Transfer a Cube to Channel #" + chan2))
			{
				TNObject[] tnos = FindObjectsOfType<TNObject>();

				foreach (TNObject tno in tnos)
				{
					if (tno.isMine && tno.channelID == chan1)
					{
						tno.TransferToChannel(chan2);
						break;
					}
				}
			}

			if (!TNManager.IsInChannel(chan1))
			{
				// Join the second channel without leaving the first one
				if (GUILayout.Button("Join Channel #" + chan1))
					TNManager.JoinChannel(chan1, true);
			}
			else if (GUILayout.Button("Leave Channel #" + chan1))
				TNManager.LeaveChannel(chan1);

			if (!TNManager.IsInChannel(chan2))
			{
				// Join the second channel without leaving the first one
				if (GUILayout.Button("Join Channel #" + chan2))
					TNManager.JoinChannel(chan2, true);
			}
			else if (GUILayout.Button("Leave Channel #" + chan2))
				TNManager.LeaveChannel(chan2);
		}
		GUILayout.EndArea();
	}
}
