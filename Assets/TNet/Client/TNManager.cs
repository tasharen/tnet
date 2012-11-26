using System.IO;
using System.Collections.Generic;
using UnityEngine;
using TNet;

/// <summary>
/// Tasharen Network Manager tailored for Unity.
/// </summary>

[AddComponentMenu("TNet/Network Manager")]
public class TNManager : MonoBehaviour
{
	/// <summary>
	/// List of objects that can be instantiated by the network.
	/// </summary>

	public GameObject[] objects;

	// Network client
	Client mClient = new Client();

	// Instance pointer
	static TNManager mInstance;

	/// <summary>
	/// Whether we're currently connected.
	/// </summary>

	static public bool isConnected { get { return mInstance != null && mInstance.mClient.isConnected; } }

	/// <summary>
	/// Whether we're currently hosting.
	/// </summary>

	static public bool isHosting { get { return mInstance != null && mInstance.mClient.isHosting; } }

	/// <summary>
	/// Whether the player is currently in a channel.
	/// </summary>

	static public bool isInChannel { get { return mInstance != null && mInstance.mClient.isInChannel; } }

	/// <summary>
	/// Current ping to the server.
	/// </summary>

	static public int ping { get { return mInstance != null ? mInstance.mClient.ping : 0; } }

	/// <summary>
	/// Get or set the player's name as everyone sees him on the network.
	/// </summary>

	static public string playerName
	{
		get
		{
			return (mInstance != null) ? mInstance.mClient.name : "";
		}
		set
		{
			if (mInstance != null) mInstance.mClient.name = value;
		}
	}

	/// <summary>
	/// Get the player associated with the specified ID.
	/// </summary>

	static public ClientPlayer GetPlayer (int id)
	{
		return (mInstance != null) ? mInstance.mClient.GetPlayer(id) : null;
	}

	/// <summary>
	/// Connect to the specified destination.
	/// </summary>

	static public void Connect (string address, int port)
	{
		if (mInstance != null) mInstance.mClient.Connect(address, port);
	}

	/// <summary>
	/// Disconnect from the specified destination.
	/// </summary>

	static public void Disconnect () { if (mInstance != null) mInstance.mClient.Disconnect(); }

	/// <summary>
	/// Join the specified channel.
	/// </summary>
	/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
	/// <param name="password">Password for the channel. First player sets the password.</param>
	/// <param name="persistent">Whether the channel will remain active even when the last player leaves.</param>

	static public void JoinChannel (int channelID, string password, bool persistent)
	{
		if (mInstance != null) mInstance.mClient.JoinChannel(channelID, password, persistent);
	}

	/// <summary>
	/// Leave the channel we're in.
	/// </summary>

	static public void LeaveChannel () { if (mInstance != null) mInstance.mClient.LeaveChannel(); }

	/// <summary>
	/// Change the hosting player.
	/// </summary>

	static public void SetHost (ClientPlayer player)
	{
		if (mInstance != null) mInstance.mClient.SetHost(player);
	}

	/// <summary>
	/// Create the specified game object on all connected clients.
	/// Note that the object must be present in the TNManager's list of objects.
	/// </summary>

	static public void Create (GameObject go)
	{
		if (mInstance != null)
		{
			int index = mInstance.IndexOf(go);

			if (index != -1)
			{
				if (mInstance.mClient.isConnected)
				{
					BinaryWriter writer = mInstance.mClient.BeginSend(Packet.RequestCreate);
					writer.Write((short)index);
					writer.Write(go.GetComponent<TNObject>() != null ? (byte)1 : (byte)0);
					writer.Write((byte)0);
					mInstance.mClient.EndSend();
					return;
				}
			}
			else
			{
				Debug.LogError("You must add the object you're trying to create to the TNManager's list of objects", go);
			}
		}
		Instantiate(go);
	}

	/// <summary>
	/// Create a new object at the specified position and rotation.
	/// Note that the object must be present in the TNManager's list of objects.
	/// </summary>

	static public void Create (GameObject go, Vector3 pos, Quaternion rot)
	{
		if (mInstance != null)
		{
			int index = mInstance.IndexOf(go);

			if (index != -1)
			{
				if (mInstance.mClient.isConnected)
				{
					BinaryWriter writer = mInstance.mClient.BeginSend(Packet.RequestCreate);
					writer.Write((short)index);
					writer.Write(go.GetComponent<TNObject>() != null ? (byte)1 : (byte)0);
					writer.Write((byte)1);
					writer.Write(pos.x);
					writer.Write(pos.y);
					writer.Write(pos.z);
					writer.Write(rot.x);
					writer.Write(rot.y);
					writer.Write(rot.z);
					writer.Write(rot.w);
					mInstance.mClient.EndSend();
					return;
				}
			}
			else
			{
				Debug.LogError("You must add the object you're trying to create to the TNManager's list of objects", go);
			}
		}
		Instantiate(go, pos, rot);
	}

	/// <summary>
	/// Destroy the specified game object.
	/// </summary>

	static public void Destroy (GameObject go)
	{
		if (isConnected)
		{
			TNObject obj = go.GetComponent<TNObject>();

			if (obj != null)
			{
				BinaryWriter writer = mInstance.mClient.BeginSend(Packet.RequestDestroy);
				writer.Write(obj.id);
				mInstance.mClient.EndSend();
				return;
			}
		}
		Destroy(go);
	}

	/// <summary>
	/// Remove the specified buffered RFC call.
	/// </summary>

	static public void RemoveBufferedRFC (int objID, short rfcID)
	{
		if (mInstance != null && mInstance.mClient.isConnected)
		{
			BinaryWriter writer = mInstance.mClient.BeginSend(Packet.RequestRemoveRFC);
			writer.Write(objID);
			writer.Write(rfcID);
			mInstance.mClient.EndSend();
		}
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	static public BinaryWriter BeginSend (Packet type) { return mInstance.mClient.BeginSend(type); }

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	static public BinaryWriter BeginSend (byte packetID) { return mInstance.mClient.BeginSend(packetID); }

	/// <summary>
	/// Send the outgoing buffer to the specified player.
	/// </summary>

	static public void EndSend () { mInstance.mClient.EndSend(); }

#region MonoBehaviour Functions

	/// <summary>
	/// Ensure that there is only one instance of this class present.
	/// </summary>

	void Awake ()
	{
		if (mInstance != null)
		{
			Destroy(gameObject);
		}
		else
		{
			mInstance = this;
			DontDestroyOnLoad(gameObject);

			mClient.onError = OnError;
			mClient.onConnect = OnConnect;
			mClient.onDisconnect = OnDisconnect;
			mClient.onPlayerJoined = OnPlayerJoined;
			mClient.onPlayerLeft = OnPlayerLeft;
			mClient.onChannelChanged = OnChannelChanged;
			mClient.onRenamePlayer = OnRenamePlayer;
			mClient.onCreate = OnCreateObject;
			mClient.onDestroy = OnDestroyObject;
			mClient.onForwardedPacket = OnForwardedPacket;
		}
	}

	/// <summary>
	/// Make sure we disconnect on exit.
	/// </summary>

	void OnDestroy () { if (isConnected) mClient.Disconnect(); }

	/// <summary>
	/// Find the index of the specified game object.
	/// </summary>

	int IndexOf (GameObject go)
	{
		for (int i = 0, imax = objects.Length; i < imax; ++i) if (objects[i] == go) return i;
		return -1;
	}

	/// <summary>
	/// Error notification.
	/// </summary>

	void OnError (string err) { Debug.LogError(err); }

	/// <summary>
	/// Connection result notification.
	/// </summary>

	void OnConnect (bool success, string message)
	{
		Debug.Log("Connected: " + success + "\n" + message);
	}

	/// <summary>
	/// Notification that happens when the client gets disconnected from the server.
	/// </summary>

	void OnDisconnect ()
	{
		Debug.Log("Disconnected");
	}

	/// <summary>
	/// Notification of a new player joining the channel.
	/// </summary>

	void OnPlayerJoined (ClientPlayer p)
	{
		Debug.Log("Player joined: " + p.name);
	}

	/// <summary>
	/// Notification of another player leaving the channel.
	/// </summary>

	void OnPlayerLeft (ClientPlayer p)
	{
		Debug.Log("Player left: " + p.name);
	}

	/// <summary>
	/// Notification of changing channels. If 'isInChannel' is 'false', then the player is not in any channel.
	/// </summary>

	void OnChannelChanged (bool isInChannel, string message)
	{
		Debug.Log("Channel: " + isInChannel + "\n" + message);
	}

	/// <summary>
	/// Notification of a player being renamed.
	/// </summary>

	void OnRenamePlayer (ClientPlayer p, string previous)
	{
		Debug.Log(previous + " is now known as " + p.name);

		// TODO: Broadcast() may not be the best solution here as some functions have 2 parameters...
		// What would be the ideal way to go here? TNManager.onConnect += ?
		// Broadcasts are still the most elegant solution by far, but how to make it clean?

		// - OnError
		// - OnConnect
		// - OnDisconnect
		// - OnPlayerJoined
		// - OnPlayerLeft
		// - OnChannelChanged
		// - OnRenamePlayer
	}

	/// <summary>
	/// Notification of a new object being created.
	/// </summary>

	void OnCreateObject (int objectID, int objID, BinaryReader reader)
	{
		GameObject go = null;

		int type = reader.ReadByte();

		if (type == 1)
		{
			Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
			Quaternion rot = new Quaternion(reader.ReadSingle(), reader.ReadSingle(),
				reader.ReadSingle(), reader.ReadSingle());
			go = Instantiate(objects[objectID], pos, rot) as GameObject;
		}
		else
		{
			go = Instantiate(objects[objectID]) as GameObject;
		}
		
		if (go != null && objID != 0)
		{
			TNObject obj = go.GetComponent<TNObject>();

			if (obj != null)
			{
				obj.id = objID;
			}
			else
			{
				Debug.LogWarning("The instantiated object has no TNObject component. Don't request a ObjectID when creating it.", go);
			}
		}
	}

	/// <summary>
	/// Notification of a network object being destroyed.
	/// </summary>

	void OnDestroyObject (int objID)
	{
		TNObject obj = TNObject.Find(objID);
		if (obj) Destroy(obj);
	}

	/// <summary>
	/// If custom functionality is needed, all unrecognized packets will arrive here.
	/// </summary>

	void OnForwardedPacket (BinaryReader reader)
	{
		int val = reader.ReadInt32();
		int objID = (val >> 8);
		int funcID = (val & 0xFF);

		if (funcID == 0)
		{
			TNObject.FindAndExecute(objID, reader.ReadString(), Tools.Read(reader));
		}
		else
		{
			TNObject.FindAndExecute(objID, funcID, Tools.Read(reader));
		}
	}

	/// <summary>
	/// Process incoming packets in the update function.
	/// </summary>

	void Update () { mClient.ProcessPackets(); }
#endregion
}