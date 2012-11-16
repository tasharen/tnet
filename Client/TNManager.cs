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
	static public TNManager instance;

	/// <summary>
	/// List of objects that can be instantiated by the network.
	/// </summary>

	public GameObject[] objects;

	/// <summary>
	/// Network client.
	/// </summary>

	public Client client = new Client();

	/// <summary>
	/// Whether we're currently connected.
	/// </summary>

	static public bool isConnected { get { return instance != null && instance.client.isConnected; } }

	/// <summary>
	/// Whether we're currently hosting.
	/// </summary>

	static public bool isHosting { get { return instance != null && instance.client.isHosting; } }

	/// <summary>
	/// Ensure that there is only one instance of this class present.
	/// </summary>

	void Awake ()
	{
		if (instance != null)
		{
			Destroy(gameObject);
		}
		else
		{
			instance = this;
			DontDestroyOnLoad(gameObject);

			client.onError = OnError;
			client.onConnect = OnConnect;
			client.onDisconnect = OnDisconnect;
			client.onPlayerJoined = OnPlayerJoined;
			client.onPlayerLeft = OnPlayerLeft;
			client.onChannelChanged = OnChannelChanged;
			client.onRenamePlayer = OnRenamePlayer;
			client.onCreate = OnCreateObject;
			client.onDestroy = OnDestroyView;
			client.onCustomPacket = OnCustomPacket;
		}
	}

	/// <summary>
	/// Find the index of the specified game object.
	/// </summary>

	int IndexOf (GameObject go)
	{
		for (int i = 0, imax = objects.Length; i < imax; ++i) if (objects[i] == go) return i;
		return -1;
	}

	/// <summary>
	/// Create the specified game object on all connected clients.
	/// Note that the object must be present in the TNManager's list of objects.
	/// </summary>

	public void Create (GameObject go)
	{
		int index = IndexOf(go);

		if (index != -1)
		{
			if (client.isConnected)
			{
				BinaryWriter writer = client.BeginSend(Packet.RequestCreate);
				writer.Write((short)index);
				writer.Write(go.GetComponent<TNView>() != null ? (byte)1 : (byte)0);
				writer.Write((byte)0);
				client.EndSend();
			}
			else Instantiate(go);
		}
		else
		{
			Debug.LogError("You must add the object you're trying to create to the TNManager's list of objects", go);
		}
	}

	/// <summary>
	/// Create a new object at the specified position and rotation.
	/// Note that the object must be present in the TNManager's list of objects.
	/// </summary>

	public void Create (GameObject go, Vector3 pos, Quaternion rot)
	{
		int index = IndexOf(go);

		if (index != -1)
		{
			if (client.isConnected)
			{
				BinaryWriter writer = client.BeginSend(Packet.RequestCreate);
				writer.Write((short)index);
				writer.Write(go.GetComponent<TNView>() != null ? (byte)1 : (byte)0);
				writer.Write((byte)1);
				writer.Write(pos.x);
				writer.Write(pos.y);
				writer.Write(pos.z);
				writer.Write(rot.x);
				writer.Write(rot.y);
				writer.Write(rot.z);
				writer.Write(rot.w);
				client.EndSend();
			}
			else Instantiate(go, pos, rot);
		}
		else
		{
			Debug.LogError("You must add the object you're trying to create to the TNManager's list of objects", go);
		}
	}

	/// <summary>
	/// Destroy the specified game object.
	/// </summary>

	public void Destroy (GameObject go)
	{
		if (isConnected)
		{
			TNView view = go.GetComponent<TNView>();

			if (view != null)
			{
				BinaryWriter writer = client.BeginSend(Packet.RequestDestroy);
				writer.Write(view.id);
				client.EndSend();
				return;
			}
		}
		Destroy(go);
	}

	/// <summary>
	/// Remove the specified buffered RFC call.
	/// </summary>

	public void RemoveBufferedRFC (int viewID, short rfcID)
	{
		if (client.isConnected)
		{
			BinaryWriter writer = client.BeginSend(Packet.RequestRemoveRFC);
			writer.Write(viewID);
			writer.Write(rfcID);
			client.EndSend();
		}
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

	void OnPlayerJoined (Player p)
	{
		Debug.Log("Player joined: " + p.name);
	}

	/// <summary>
	/// Notification of another player leaving the channel.
	/// </summary>

	void OnPlayerLeft (Player p)
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

	void OnRenamePlayer (Player p, string previous)
	{
		Debug.Log(previous + " is now known as " + p.name);
	}

	/// <summary>
	/// Notification of a new object being created.
	/// </summary>

	void OnCreateObject (int objectID, int viewID, BinaryReader reader)
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
		
		if (go != null && viewID != 0)
		{
			TNView view = go.GetComponent<TNView>();

			if (view != null)
			{
				view.id = viewID;
			}
			else
			{
				Debug.LogWarning("The instantiated object has no TNView component. Don't request a ViewID when creating it.", go);
			}
		}
	}

	/// <summary>
	/// Notification of a network view being destroyed.
	/// </summary>

	void OnDestroyView (int viewID)
	{
		TNView view = TNView.Find(viewID);
		if (view) Destroy(view);
	}

	/// <summary>
	/// If custom functionality is needed, all unrecognized packets will arrive here.
	/// </summary>

	void OnCustomPacket (int packetID, BinaryReader reader)
	{
		if (packetID == 0)
		{
			int viewID = reader.ReadInt32();
			int funcID = reader.ReadInt16();

			if (funcID == 0)
			{
				TNView.FindAndExecute(viewID, reader.ReadString(), Tools.Read(reader));
			}
			else
			{
				TNView.FindAndExecute(viewID, funcID, Tools.Read(reader));
			}
		}
	}

	/// <summary>
	/// Process incoming packets in the update function.
	/// </summary>

	void Update () { client.ProcessPackets(); }
}