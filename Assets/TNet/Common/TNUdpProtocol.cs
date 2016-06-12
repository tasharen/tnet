//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;

namespace TNet
{
/// <summary>
/// UDP class makes it possible to broadcast messages to players on the same network prior to establishing a connection.
/// </summary>

public class UdpProtocol
{
	public UdpProtocol () { }
	public UdpProtocol (string name) { this.name = name; }

	/// <summary>
	/// Unique name, in case you need to differentiate between protocols.
	/// </summary>

	public string name = "UDP Protocol";

	/// <summary>
	/// If 'true', TNet will use multicasting with new UDP sockets. If 'false', TNet will use broadcasting instead.
	/// Multicasting is the suggested way to go as it supports multiple network interfaces properly.
	/// It's important to set this prior to calling StartUDP or the change won't have any effect.
	/// </summary>

#if UNITY_IPHONE
	static public bool useMulticasting = false;
#else
	static public bool useMulticasting = true;
#endif

	/// <summary>
	/// When you have multiple network interfaces, it's often important to be able to specify
	/// which interface will actually be used to send UDP messages. By default this will be set
	/// to IPAddress.Any, but you can change it to be something else if you desire.
	/// It's important to set this prior to calling StartUDP or the change won't have any effect.
	/// </summary>

	static public IPAddress defaultNetworkInterface = IPAddress.Any;

	/// <summary>
	/// Network interface used for broadcasts and multicasts (UDP lobby communication only).
	/// </summary>

	static public IPAddress defaultBroadcastInterface = IPAddress.Any;

	// Port used to listen and socket used to send and receive
	int mPort = -1;
	Socket mSocket;
	//List<UdpClient> mClients = new List<UdpClient>();

#if !UNITY_WEBPLAYER
	// Buffer used for receiving incoming data
	byte[] mTemp = new byte[8192];

	// End point of where the data is coming from
	EndPoint mEndPoint;
	bool mMulticast = true;

	// Default end point -- mEndPoint is reset to this value after every receive operation.
	EndPoint mDefaultEndPoint;

	// Cached broadcast end-point
	static IPAddress multicastIP = IPAddress.Parse("224.168.100.17");
	IPEndPoint mMulticastEndPoint = new IPEndPoint(multicastIP, 0);
	IPEndPoint mBroadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, 0);
#endif

	// Incoming message queue
	protected Queue<Datagram> mIn = new Queue<Datagram>();
	protected Queue<Datagram> mOut = new Queue<Datagram>();

	/// <summary>
	/// Whether we can send or receive through the UDP socket.
	/// </summary>

	public bool isActive { get { return mPort != -1; } }

	/// <summary>
	/// Port used for listening.
	/// </summary>

	public int listeningPort { get { return mPort > 0 ? mPort : 0; } }

	/// <summary>
	/// Start UDP, but don't bind it to a specific port. This means we will be able to send, but not receive.
	/// </summary>

	public bool Start () { return Start(0); }

#if UNITY_FLASH || UNITY_WEBPLAYER
	// UDP is not supported by Flash.
	public bool Start (int port)
	{
#if UNITY_EDITOR
		UnityEngine.Debug.LogWarning("[TNet] Unity doesn't support UDP on Flash or Web Player targets");
#endif
		return false;
	}

	// UDP is not supported by Flash.
	public bool Start (int port, IPAddress defaultNetworkInterface) { return Start(port); }
#else

	/// <summary>
	/// Start listening for incoming messages on the specified port.
	/// </summary>

	public bool Start (int port) { return Start(port, defaultNetworkInterface); }

	/// <summary>
	/// Start listening for incoming messages on the specified port and network interface.
	/// </summary>

	public bool Start (int port, IPAddress ni)
	{
		Stop();

		mPort = port;
		mSocket = new Socket(ni.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

#if !UNITY_WEBPLAYER
		// Web player doesn't seem to support broadcasts
		mSocket.MulticastLoopback = true;
		mMulticast = useMulticasting;

		try
		{
			if (useMulticasting)
			{
				List<IPAddress> ips = Tools.localAddresses;

				foreach (IPAddress ip in ips)
				{
					MulticastOption opt = new MulticastOption(multicastIP, ip);
					mSocket.SetSocketOption(ni.AddressFamily == AddressFamily.InterNetworkV6 ?
						SocketOptionLevel.IPv6 : SocketOptionLevel.IP, SocketOptionName.AddMembership, opt);
				}
			}
			else mSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
		}
		catch (System.Exception) { }
#endif
		// Port zero means we will be able to send, but not receive
		if (mPort == 0) return true;

		try
		{
			// Use the default network interface if one wasn't explicitly chosen
 #if (UNITY_IPHONE && !UNITY_EDITOR) //|| UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
			IPAddress networkInterface = useMulticasting ? multicastIP : ni;
#else
			IPAddress networkInterface = ni;
 #endif
			mEndPoint = new IPEndPoint(networkInterface, 0);
			mDefaultEndPoint = new IPEndPoint(networkInterface, 0);

			// Bind the socket to the specific network interface and start listening for incoming packets
			mSocket.Bind(new IPEndPoint(networkInterface, mPort));
			mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mEndPoint, OnReceive, null);
		}
#if UNITY_EDITOR
		catch (System.Exception ex)
		{
			UnityEngine.Debug.LogError("[TNet] Udp.Start: " + ex.Message);
			Stop();
			return false;
		}
#elif DEBUG
		catch (System.Exception ex) { Tools.Print("Udp.Start: " + ex.Message); Stop(); return false; }
#else
		catch (System.Exception) { Stop(); return false; }
#endif
		return true;
	}
#endif // UNITY_FLASH

	/// <summary>
	/// Stop listening for incoming packets.
	/// </summary>

	public void Stop ()
	{
		mPort = -1;

		if (mSocket != null)
		{
			mSocket.Close();
			mSocket = null;
		}

		Buffer.Recycle(mIn);
		Buffer.Recycle(mOut);
	}

#if !UNITY_WEBPLAYER
	/// <summary>
	/// Receive incoming data.
	/// </summary>

	void OnReceive (IAsyncResult result)
	{
		if (!isActive) return;
		int bytes = 0;

		try
		{
			bytes = mSocket.EndReceiveFrom(result, ref mEndPoint);
		}
		catch (System.Exception ex)
		{
			Error(new IPEndPoint(Tools.localAddress, 0), ex.Message.Trim() + " (" + name + ")");
		}

		if (bytes > 4)
		{
			// This datagram is now ready to be processed
			Buffer buffer = Buffer.Create();
			buffer.BeginWriting(false).Write(mTemp, 0, bytes);
			buffer.BeginReading(4);

			// The 'endPoint', gets reassigned rather than updated.
			Datagram dg = new Datagram();
			dg.buffer = buffer;
			dg.ip = (IPEndPoint)mEndPoint;
			lock (mIn) mIn.Enqueue(dg);
		}

		// Queue up the next receive operation
		if (mSocket != null)
		{
			mEndPoint = mDefaultEndPoint;

			try
			{
				mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mEndPoint, OnReceive, null);
			}
			catch (System.Exception ex)
			{
				Error(new IPEndPoint(Tools.localAddress, 0), ex.Message);
			}
		}
	}
#endif

	/// <summary>
	/// Extract the first incoming packet.
	/// </summary>

	public bool ReceivePacket (out Buffer buffer, out IPEndPoint source)
	{
		if (mPort == 0)
		{
			Stop();
			throw new System.InvalidOperationException("You must specify a non-zero port to UdpProtocol.Start() before you can receive data.");
		}
		else if (mIn.Count != 0)
		{
			lock (mIn)
			{
				Datagram dg = mIn.Dequeue();
				buffer = dg.buffer;
				source = dg.ip;
				return true;
			}
		}
		buffer = null;
		source = null;
		return false;
	}

	/// <summary>
	/// Send an empty packet to the target destination.
	/// Can be used for NAT punch-through, or just to keep a UDP connection alive.
	/// Empty packets are simply ignored.
	/// </summary>

	public void SendEmptyPacket (IPEndPoint ip)
	{
		Buffer buffer = Buffer.Create();
		buffer.BeginPacket(Packet.Empty);
		buffer.EndPacket();
		Send(buffer, ip);
		buffer.Recycle();
	}

	/// <summary>
	/// Send the specified buffer to the entire LAN.
	/// </summary>

	public void Broadcast (Buffer buffer, int port)
	{
		if (buffer != null)
		{
#if UNITY_WEBPLAYER || UNITY_FLASH
#if UNITY_EDITOR
			UnityEngine.Debug.LogError("[TNet] Sending broadcasts doesn't work in the Unity Web Player or Flash");
#endif
#else
			IPEndPoint endPoint = mMulticast ? mMulticastEndPoint : mBroadcastEndPoint;
			endPoint.Port = port;

			try
			{
				mSocket.SendTo(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, endPoint);
			}
			catch (System.Exception ex)
			{
				Error(null, ex.Message);
			}
#endif
		}
	}

	/// <summary>
	/// Send the specified datagram.
	/// </summary>

	public void Send (Buffer buffer, IPEndPoint ip)
	{
		if (ip.Address.Equals(IPAddress.Broadcast))
		{
			Broadcast(buffer, ip.Port);
		}
		else if (mSocket != null)
		{
			if (mSocket.AddressFamily != ip.AddressFamily)
			{
#if UNITY_EDITOR
				UnityEngine.Debug.LogError("UDP socket " + name + " (" + mSocket.AddressFamily + ") and the desired address " + ip.Address +
					" (" + ip.AddressFamily + ") were started with different address families");
#else
				Tools.LogError("UDP socket " + name + " (" + mSocket.AddressFamily + ") and the desired address " + ip.Address +
					" (" + ip.AddressFamily + ") were started with different address families");
#endif
				return;
			}

			buffer.MarkAsUsed();
			buffer.BeginReading();

			lock (mOut)
			{
				Datagram dg = new Datagram();
				dg.buffer = buffer;
				dg.ip = ip;
				mOut.Enqueue(dg);

				if (mOut.Count == 1)
				{
					try
					{
						// If it's the first datagram, begin the sending process
						mSocket.BeginSendTo(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, ip, OnSend, null);
					}
					catch (Exception ex)
					{
						Tools.LogError(ex.Message + "\n" + ex.StackTrace);
						buffer.Recycle();
					}
				}
			}
		}
		else Tools.LogError("The socket is null. Did you forget to call UdpProtocol.Start()?");
	}

	/// <summary>
	/// Send completion callback. Recycles the datagram.
	/// </summary>

	void OnSend (IAsyncResult result)
	{
		if (!isActive) return;
		int bytes = 0;

		try
		{
			bytes = mSocket.EndSendTo(result);
		}
		catch (System.Exception ex)
		{
			bytes = -1;
#if STANDALONE
			Tools.Print(ex.Message.Trim() + " (" + name + ")");
#else
			UnityEngine.Debug.Log("[TNet] OnSend (" + mSocket.AddressFamily + "): " + ex.Message.Trim() + " (" + name + ")");
#endif
		}

		lock (mOut)
		{
			mOut.Dequeue().buffer.Recycle();

			if (bytes > 0 && mSocket != null && mOut.Count != 0)
			{
				// If there is another packet to send out, let's send it
				Datagram dg = mOut.Peek();
				mSocket.BeginSendTo(dg.buffer.buffer, dg.buffer.position, dg.buffer.size, SocketFlags.None, dg.ip, OnSend, null);
			}
		}
	}

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	public void Error (IPEndPoint ip, string error)
	{
#if UNITY_EDITOR
		UnityEngine.Debug.LogError(error);
#endif
		Buffer buffer = Buffer.Create();
		buffer.BeginPacket(Packet.Error).Write(error);
		buffer.EndTcpPacketWithOffset(4);

		Datagram dg = new Datagram();
		dg.buffer = buffer;
		dg.ip = ip;
		lock (mIn) mIn.Enqueue(dg);
	}
}
}
