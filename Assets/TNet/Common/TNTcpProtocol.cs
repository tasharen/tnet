//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Text;

#if UNITY_EDITOR
using UnityEngine;
#endif

namespace TNet
{
/// <summary>
/// Common network communication-based logic: sending and receiving of data via TCP.
/// </summary>

public class TcpProtocol : Player
{
	/// <summary>
	/// Whether the hosted server will respond to HTTP GET requests.
	/// </summary>

	static public bool httpGetSupport = false;

	/// <summary>
	/// When you have multiple network interfaces, it's often important to be able to specify
	/// which interface will actually be listened to messages. You can change it to IPv6, for example.
	/// </summary>

	static public IPAddress defaultListenerInterface = IPAddress.Any;

	public enum Stage
	{
		NotConnected,
		Connecting,
		Verifying,
		Connected,
		WebBrowser,
	}

	/// <summary>
	/// Current connection stage.
	/// </summary>

	public Stage stage = Stage.NotConnected;

	/// <summary>
	/// IP end point of whomever we're connected to.
	/// </summary>

	public IPEndPoint tcpEndPoint;

	/// <summary>
	/// Timestamp of when we received the last message.
	/// </summary>

	public long lastReceivedTime = 0;

	/// <summary>
	/// How long to allow this player to go without packets before disconnecting them.
	/// This value is in milliseconds, so 1000 means 1 second.
	/// </summary>
#if UNITY_EDITOR
	public long timeoutTime = 60000;
#else
	public long timeoutTime = 20000;
#endif

	// Incoming and outgoing queues
	Queue<Buffer> mIn = new Queue<Buffer>();
	Queue<Buffer> mOut = new Queue<Buffer>();

	// Buffer used for receiving incoming data
	byte[] mTemp = new byte[8192];

	// Current incoming buffer
	Buffer mReceiveBuffer;
	int mExpected = 0;
	int mOffset = 0;
	Socket mSocket;
	bool mNoDelay = false;
	IPEndPoint mFallback;
	List<Socket> mConnecting = new List<Socket>();

	// Static as it's temporary
	static Buffer mBuffer;

	/// <summary>
	/// Whether the connection is currently active.
	/// </summary>

	public bool isConnected { get { return stage == Stage.Connected; } }

	/// <summary>
	/// Socket used for communication.
	/// </summary>

	public Socket socket { get { return mSocket; } }

	/// <summary>
	/// If sockets are not used, an outgoing queue can be specified instead.
	/// </summary>

	public Queue<Buffer> sendQueue = null;

	/// <summary>
	/// Direct access to the incoming buffer to deposit messages in. Don't forget to lock it before using it.
	/// </summary>

	public Queue<Buffer> receiveQueue { get { return mIn; } }

	/// <summary>
	/// Whether the socket is currently connected. A socket can be connected while verifying the connection.
	/// In most cases you should use 'isConnected' instead.
	/// </summary>

	public bool isSocketConnected { get { return mSocket != null && mSocket.Connected; } }

	/// <summary>
	/// Whether we are currently trying to establish a new connection.
	/// </summary>

	public bool isTryingToConnect { get { return mConnecting.size != 0; } }

	/// <summary>
	/// Enable or disable the Nagle's buffering algorithm (aka NO_DELAY flag).
	/// Enabling this flag will improve latency at the cost of increased bandwidth.
	/// http://en.wikipedia.org/wiki/Nagle's_algorithm
	/// </summary>

	public bool noDelay
	{
		get
		{
			return mNoDelay;
		}
		set
		{
			if (mNoDelay != value)
			{
				mNoDelay = value;
#if !UNITY_WINRT
				mSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, mNoDelay);
#endif
			}
		}
	}

	/// <summary>
	/// Connected target's address.
	/// </summary>

	public string address { get { return (tcpEndPoint != null) ? tcpEndPoint.ToString() : "0.0.0.0:0"; } }

	/// <summary>
	/// Try to establish a connection with the specified remote destination.
	/// </summary>

	public void Connect (IPEndPoint externalIP, IPEndPoint internalIP = null)
	{
		Disconnect();

		lock (mIn) Buffer.Recycle(mIn);
		lock (mOut) Buffer.Recycle(mOut);

		if (externalIP != null)
		{
			// Some routers, like Asus RT-N66U don't support NAT Loopback, and connecting to an external IP
			// will connect to the router instead. So if it's a local IP, connect to it first.
			if (internalIP != null && Tools.GetSubnet(Tools.localAddress) == Tools.GetSubnet(internalIP.Address))
			{
				tcpEndPoint = internalIP;
				mFallback = externalIP;
			}
			else
			{
				tcpEndPoint = externalIP;
				mFallback = internalIP;
			}
			ConnectToTcpEndPoint();
		}
	}

	/// <summary>
	/// Try to establish a connection with the current tcpEndPoint.
	/// </summary>

	bool ConnectToTcpEndPoint ()
	{
		if (tcpEndPoint != null)
		{
			stage = Stage.Connecting;

			try
			{
				lock (mConnecting)
				{
					mSocket = new Socket(tcpEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
					mConnecting.Add(mSocket);
				}

				IAsyncResult result = mSocket.BeginConnect(tcpEndPoint, OnConnectResult, mSocket);
				Thread th = new Thread(CancelConnect);
				th.Start(result);
				return true;
			}
			catch (System.Exception ex)
			{
				RespondWithError(ex);
			}
		}
		else RespondWithError("Unable to resolve the specified address");
		return false;
	}

	/// <summary>
	/// Try to establish a connection with the fallback end point.
	/// </summary>

	bool ConnectToFallback ()
	{
		tcpEndPoint = mFallback;
		mFallback = null;
		return (tcpEndPoint != null) && ConnectToTcpEndPoint();
	}

	/// <summary>
	/// Default timeout on a connection attempt it something around 15 seconds, which is ridiculously long.
	/// </summary>

	void CancelConnect (object obj)
	{
		IAsyncResult result = (IAsyncResult)obj;
#if !UNITY_WINRT
		if (result != null && !result.AsyncWaitHandle.WaitOne(3000, true))
		{
			try
			{
				Socket sock = (Socket)result.AsyncState;

				if (sock != null)
				{
					sock.Close();

					lock (mConnecting)
					{
						// Last active connection attempt
						if (mConnecting.size > 0 && mConnecting[mConnecting.size - 1] == sock)
						{
							mSocket = null;

							if (!ConnectToFallback())
							{
								RespondWithError("Unable to connect");
								Close(false);
							}
						}
						mConnecting.Remove(sock);
					}
				}
			}
			catch (System.Exception) { }
		}
#endif
	}

	/// <summary>
	/// Connection attempt result.
	/// </summary>

	void OnConnectResult (IAsyncResult result)
	{
		Socket sock = (Socket)result.AsyncState;

		// Windows handles async sockets differently than other platforms, it seems.
		// If a socket is closed, OnConnectResult() is never called on Windows.
		// On the mac it does get called, however, and if the socket is used here
		// then a null exception gets thrown because the socket is not usable by this point.
		if (sock == null) return;

		if (mSocket != null && sock == mSocket)
		{
			bool success = true;
			string errMsg = "Failed to connect";

			try
			{
#if !UNITY_WINRT
				sock.EndConnect(result);
#endif
			}
			catch (System.Exception ex)
			{
				if (sock == mSocket) mSocket = null;
				sock.Close();
				errMsg = ex.Message;
				success = false;
			}

			if (success)
			{
				// Request a player ID
				stage = Stage.Verifying;
				BinaryWriter writer = BeginSend(Packet.RequestID);
				writer.Write(version);
				writer.Write(string.IsNullOrEmpty(name) ? "Guest" : name);
				writer.Write(dataNode);

				EndSend();
				StartReceiving();
			}
			else if (!ConnectToFallback())
			{
				RespondWithError(errMsg);
				Close(false);
			}
		}

		// We are no longer trying to connect via this socket
		lock (mConnecting) mConnecting.Remove(sock);
	}

	/// <summary>
	/// Disconnect the player, freeing all resources.
	/// </summary>

	public void Disconnect (bool notify = false)
	{
		try
		{
			lock (mConnecting)
			{
				for (int i = mConnecting.size; i > 0; )
				{
					Socket sock = mConnecting[--i];
					mConnecting.RemoveAt(i);
					if (sock != null) sock.Close();
				}
			}

			if (mSocket != null)
			{
				Close(notify || mSocket.Connected);
			}
			else if (sendQueue != null)
			{
				Close(true);
				sendQueue = null;
			}
		}
		catch (System.Exception)
		{
			lock (mConnecting) mConnecting.Clear();
			mSocket = null;
		}
	}

	/// <summary>
	/// Close the connection.
	/// </summary>

	public void Close (bool notify) { lock (mOut) CloseNotThreadSafe(notify); }

	/// <summary>
	/// Close the connection.
	/// </summary>

	void CloseNotThreadSafe (bool notify)
	{
		Buffer.Recycle(mOut);
		stage = Stage.NotConnected;

		if (mSocket != null)
		{
			try
			{
				if (mSocket.Connected) mSocket.Shutdown(SocketShutdown.Both);
				mSocket.Close();
			}
			catch (System.Exception) {}
			mSocket = null;

			if (notify)
			{
				Buffer buffer = Buffer.Create();
				buffer.BeginPacket(Packet.Disconnect);
				buffer.EndTcpPacketWithOffset(4);

				lock (mIn)
				{
					Buffer.Recycle(mIn);
					mIn.Enqueue(buffer);
				}
			}
			else lock (mIn) Buffer.Recycle(mIn);
		}
		else if (notify && sendQueue != null)
		{
			sendQueue = null;
			Buffer buffer = Buffer.Create();
			buffer.BeginPacket(Packet.Disconnect);
			buffer.EndTcpPacketWithOffset(4);

			lock (mIn)
			{
				Buffer.Recycle(mIn);
				mIn.Enqueue(buffer);
			}
		}

		if (mReceiveBuffer != null)
		{
			mReceiveBuffer.Recycle();
			mReceiveBuffer = null;
		}
	}

	/// <summary>
	/// Release the buffers.
	/// </summary>

	public void Release ()
	{
		lock (mOut) CloseNotThreadSafe(false);
		dataNode = null;
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (Packet type)
	{
		mBuffer = Buffer.Create();
		return mBuffer.BeginPacket(type);
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (byte packetID)
	{
		mBuffer = Buffer.Create();
		return mBuffer.BeginPacket(packetID);
	}

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	public void EndSend ()
	{
		if (mBuffer != null)
		{
			mBuffer.EndPacket();
			SendTcpPacket(mBuffer);
			mBuffer.Recycle();
			mBuffer = null;
		}
	}

	/// <summary>
	/// Send the specified packet. Marks the buffer as used.
	/// </summary>

	public void SendTcpPacket (Buffer buffer)
	{
		buffer.MarkAsUsed();
		BinaryReader reader = buffer.BeginReading();

#if DEBUG_PACKETS && !STANDALONE
		Packet packet = (Packet)buffer.PeekByte(4);
		if (packet != Packet.RequestPing && packet != Packet.ResponsePing)
			UnityEngine.Debug.Log("Sending: " + packet + " to " + name + " (" + (buffer.size - 5).ToString("N0") + " bytes)");
#endif

		if (mSocket != null && mSocket.Connected)
		{
			lock (mOut)
			{
				mOut.Enqueue(buffer);

				// If it's the first packet, let's begin the send process
				if (mOut.Count == 1)
				{
					try
					{
#if !UNITY_WINRT
						mSocket.BeginSend(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, OnSend, buffer);
#endif
					}
					catch (System.Exception ex)
					{
						mOut.Clear();
						buffer.Recycle();
						RespondWithError(ex);
						CloseNotThreadSafe(false);
					}
				}
			}
			return;
		}
		
		if (sendQueue != null)
		{
			if (buffer.position != 0)
			{
				// Offline mode sends packets individually and they should not be reused
#if UNITY_EDITOR
				Debug.LogWarning("Packet's position is " + buffer.position + " instead of 0. Potentially sending the same packet more than once. Ignoring...");
#endif
				return;
			}

			// Skip the packet's size
			int size = reader.ReadInt32();

			if (size == buffer.size)
			{
				// Note that after this the buffer can no longer be used again as its offset is +4
				lock (sendQueue) sendQueue.Enqueue(buffer);
				return;
			}

			// Multi-part packet -- split it up into separate ones
			lock (sendQueue)
			{
				for (;;)
				{
					byte[] bytes = reader.ReadBytes(size);

					Buffer temp = Buffer.Create();
					BinaryWriter writer = temp.BeginWriting();
					writer.Write(size);
					writer.Write(bytes);
					temp.BeginReading(4);
					temp.EndWriting();
					sendQueue.Enqueue(temp);

					if (buffer.size > 0) size = reader.ReadInt32();
					else break;
				}
			}
		}
		
		buffer.Recycle();
	}

	/// <summary>
	/// Send completion callback. Recycles the buffer.
	/// </summary>

	void OnSend (IAsyncResult result)
	{
		if (stage == Stage.NotConnected) return;
		int bytes;

		try
		{
#if !UNITY_WINRT
			bytes = mSocket.EndSend(result);
			Buffer buff = (Buffer)result.AsyncState;

			// If not everything was sent...
			if (bytes < buff.size)
			{
				try
				{
					// Advance the position and send the rest
					buff.position += bytes;
					mSocket.BeginSend(buff.buffer, buff.position, buff.size, SocketFlags.None, OnSend, buff);
					return;
				}
				catch (Exception ex)
				{
					RespondWithError(ex);
					CloseNotThreadSafe(false);
				}
			}
#endif
			lock (mOut)
			{
				// The buffer has been sent and can now be safely recycled
				Buffer b = (mOut.Count != 0) ? mOut.Dequeue() : null;
				if (b != null) b.Recycle();

#if !UNITY_WINRT
				if (bytes > 0 && mSocket != null && mSocket.Connected)
				{
					// Nothing else left -- just exit
					if (mOut.Count == 0) return;

					try
					{
						Buffer next = mOut.Peek();
						mSocket.BeginSend(next.buffer, next.position, next.size, SocketFlags.None, OnSend, next);
					}
					catch (Exception ex)
					{
						RespondWithError(ex);
						CloseNotThreadSafe(false);
					}
				}
				else CloseNotThreadSafe(true);
#endif
			}
		}
		catch (System.Exception ex)
		{
			bytes = 0;
			Close(true);
			RespondWithError(ex);
		}
	}

	/// <summary>
	/// Start receiving incoming messages on the current socket.
	/// </summary>

	public void StartReceiving () { StartReceiving(null); }

	/// <summary>
	/// Start receiving incoming messages on the specified socket (for example socket accepted via Listen).
	/// </summary>

	public void StartReceiving (Socket socket)
	{
		if (socket != null)
		{
			Close(false);
			mSocket = socket;
		}

		if (mSocket != null && mSocket.Connected)
		{
			// We are now verifying the connection
			stage = Stage.Verifying;

			// Save the timestamp
			lastReceivedTime = DateTime.UtcNow.Ticks / 10000;

			// Queue up the read operation
			try
			{
				// Save the address
				tcpEndPoint = (IPEndPoint)mSocket.RemoteEndPoint;
#if !UNITY_WINRT
				mSocket.BeginReceive(mTemp, 0, mTemp.Length, SocketFlags.None, OnReceive, mSocket);
#endif
			}
			catch (System.Exception ex)
			{
				if (!(ex is SocketException)) RespondWithError(ex);
				Disconnect(true);
			}
		}
	}

	/// <summary>
	/// Extract the first incoming packet.
	/// </summary>

	public bool ReceivePacket (out Buffer buffer)
	{
		lock (mIn)
		{
			if (mIn.Count != 0)
			{
				buffer = mIn.Dequeue();
				return buffer != null;
			}
		}
		buffer = null;
		return false;
	}

	/// <summary>
	/// Receive incoming data.
	/// </summary>

	void OnReceive (IAsyncResult result)
	{
		if (stage == Stage.NotConnected) return;
		int bytes = 0;
		Socket socket = (Socket)result.AsyncState;

		try
		{
#if !UNITY_WINRT
			bytes = socket.EndReceive(result);
#endif
			if (socket != mSocket) return;
		}
		catch (System.Exception ex)
		{
			if (socket != mSocket) return;
			if (!(ex is SocketException)) RespondWithError(ex);
			Disconnect(true);
			return;
		}
		lastReceivedTime = DateTime.UtcNow.Ticks / 10000;

		if (bytes == 0)
		{
			Close(true);
		}
		else if (ProcessBuffer(bytes))
		{
			if (stage == Stage.NotConnected) return;

			try
			{
#if !UNITY_WINRT
				// Queue up the next read operation
				mSocket.BeginReceive(mTemp, 0, mTemp.Length, SocketFlags.None, OnReceive, mSocket);
#endif
			}
			catch (System.Exception ex)
			{
				if (!(ex is SocketException)) RespondWithError(ex);
				Close(false);
			}
		}
		else Close(true);
	}

	/// <summary>
	/// See if the received packet can be processed and split it up into different ones.
	/// </summary>

	bool ProcessBuffer (int bytes)
	{
		if (mReceiveBuffer == null)
		{
			// Create a new packet buffer
			mReceiveBuffer = Buffer.Create();
			mReceiveBuffer.BeginWriting(false).Write(mTemp, 0, bytes);
			mExpected = 0;
			mOffset = 0;
		}
		else
		{
			// Append this data to the end of the last used buffer
			mReceiveBuffer.BeginWriting(true).Write(mTemp, 0, bytes);
		}

		for (int available = mReceiveBuffer.size - mOffset; available >= 4; )
		{
			// Figure out the expected size of the packet
			if (mExpected == 0)
			{
				mExpected = mReceiveBuffer.PeekInt(mOffset);

				// "GET " -- HTTP GET request sent by a web browser
				if (mExpected == 542393671)
				{
					if (httpGetSupport)
					{
						if (stage == Stage.Verifying || stage == Stage.WebBrowser)
						{
							stage = Stage.WebBrowser;
							string request = Encoding.ASCII.GetString(mReceiveBuffer.buffer, mOffset, available);
							mReceiveBuffer.BeginPacket(Packet.RequestHTTPGet).Write(request);
							mReceiveBuffer.EndPacket();
							mReceiveBuffer.BeginReading(4);
							lock (mIn) mIn.Enqueue(mReceiveBuffer);
							mReceiveBuffer = null;
							mExpected = 0;
							mOffset = 0;
						}
						return true;
					}

					mReceiveBuffer.Recycle();
					mReceiveBuffer = null;
					mExpected = 0;
					mOffset = 0;
					Disconnect();
					return false;
				}
				else if (mExpected < 0 || mExpected > 16777216)
				{
#if UNITY_EDITOR
					UnityEngine.Debug.LogError("Malformed data packet: " + mOffset + ", " + available + " / " + mExpected);
#else
					Tools.LogError("Malformed data packet: " + mOffset + ", " + available + " / " + mExpected);
#endif
					mReceiveBuffer.Recycle();
					mReceiveBuffer = null;
					mExpected = 0;
					mOffset = 0;
					Disconnect();
					return false;
				}
			}

			// The first 4 bytes of any packet always contain the number of bytes in that packet
			available -= 4;

			// If the entire packet is present
			if (available == mExpected)
			{
				// Reset the position to the beginning of the packet
				mReceiveBuffer.BeginReading(mOffset + 4);

				// This packet is now ready to be processed
				lock (mIn) mIn.Enqueue(mReceiveBuffer);

				mReceiveBuffer = null;
				mExpected = 0;
				mOffset = 0;
				break;
			}
			else if (available > mExpected)
			{
				// There is more than one packet. Extract this packet fully.
				int realSize = mExpected + 4;
				Buffer temp = Buffer.Create();

				// Extract the packet and move past its size component
				BinaryWriter bw = temp.BeginWriting(false);
				bw.Write(mReceiveBuffer.buffer, mOffset, realSize);
				temp.BeginReading(4);

				// This packet is now ready to be processed
				lock (mIn) mIn.Enqueue(temp);

				// Skip this packet
				available -= mExpected;
				mOffset += realSize;
				mExpected = 0;
			}
			else break;
		}
		return true;
	}

	/// <summary>
	/// Log an error message.
	/// </summary>

	public void LogError (string error, string stack = null, bool logInFile = true)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(name);
		sb.Append(" (");
		sb.Append(address);

		if (aliases != null)
			for (int i = 0; i < aliases.size; ++i)
				sb.Append(", " + aliases.buffer[i]);

		sb.Append("): ");
		sb.Append(error);

		Tools.LogError(sb.ToString(), stack, logInFile);
	}

	/// <summary>
	/// Log an error message.
	/// </summary>

	public void Log (string msg, string stack = null, bool logInFile = true)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(name);
		sb.Append(" (");
		sb.Append(address);

		if (aliases != null)
			for (int i = 0; i < aliases.size; ++i)
				sb.Append(", " + aliases.buffer[i]);

		sb.Append("): ");
		sb.Append(msg);

		if (stack != null)
		{
			sb.Append("\n");
			sb.Append(stack);
		}
		Tools.Log(sb.ToString(), logInFile);
	}

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	public void RespondWithError (string error) { RespondWithError(Buffer.Create(), error); }

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	public void RespondWithError (Exception ex)
	{
#if UNITY_EDITOR
		Debug.LogError(ex.Message + "\n" + ex.StackTrace);
#endif
		var buffer = Buffer.Create();
		var error = ex.Message;
		buffer.BeginPacket(Packet.Error).Write(error);
		buffer.EndTcpPacketWithOffset(4);
		lock (mIn) mIn.Enqueue(buffer);
		LogError(ex.Message, ex.StackTrace, true);
	}

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	void RespondWithError (Buffer buffer, string error)
	{
#if UNITY_EDITOR
		Debug.LogError(error);
#endif
		buffer.BeginPacket(Packet.Error).Write(error);
		buffer.EndTcpPacketWithOffset(4);
		lock (mIn) mIn.Enqueue(buffer);
	}

	/// <summary>
	/// Verify the connection. Returns 'true' if successful.
	/// </summary>

	public bool VerifyRequestID (BinaryReader reader, Buffer buffer, bool uniqueID)
	{
		Packet request = (Packet)reader.ReadByte();

		if (request == Packet.RequestID)
		{
			int theirVer = reader.ReadInt32();

			if (theirVer == version)
			{
				if (uniqueID) lock (mLock) { id = ++mPlayerCounter; }
				else id = 0;
				name = reader.ReadString();
				dataNode = reader.ReadDataNode();
				stage = TcpProtocol.Stage.Connected;
#if STANDALONE
				if (id != 0) Tools.Log(name + " (" + address + "): Connected [" + id + "]");
#endif
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Verify the connection. Returns 'true' if successful.
	/// </summary>

	public bool VerifyResponseID (Packet packet, BinaryReader reader)
	{
		if (packet == Packet.ResponseID)
		{
			int serverVersion = reader.ReadInt32();

			if (serverVersion != 0 && serverVersion == version)
			{
				id = reader.ReadInt32();
				stage = Stage.Connected;
				return true;
			}
			else
			{
				id = 0;
				RespondWithError("Version mismatch! Server is running a different protocol version!");
				Close(false);
				return false;
			}
		}
		else if (packet == Packet.Error)
		{
			string text = reader.ReadString();
			RespondWithError("Expected a response ID, got " + packet + ": " + text);
			Close(false);
#if UNITY_EDITOR
			UnityEngine.Debug.LogWarning("[TNet] VerifyResponseID expected ResponseID, got " + packet + ": " + text);
#endif
			return false;
		}

		RespondWithError("Expected a response ID, got " + packet);
		Close(false);
#if UNITY_EDITOR
		UnityEngine.Debug.LogWarning("[TNet] VerifyResponseID expected ResponseID, got " + packet);
#endif
		return false;
	}
}
}
