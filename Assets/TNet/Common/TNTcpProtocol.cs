//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

//#define DEBUG_PACKETS

using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net;
using System.Text;

#if UNITY_EDITOR
using UnityEngine;
#endif

namespace TNet
{
	/// <summary>
	/// For additional custom implementations, such as Steam's P2P networking.
	/// </summary>

	public interface IConnection
	{
		bool isConnected { get; }
		bool SendPacket (Buffer buffer);
		void ReceivePacket (out Buffer buffer);
		void OnDisconnect ();
	}

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

		[DoNotObfuscate] public enum Stage
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

#if !MODDING
		volatile bool mSending = false;
#endif
		/// <summary>
		/// Current size of the incoming queue in bytes.
		/// </summary>

		public int incomingQueueSize
		{
			get
			{
				lock (mIn)
				{
					int size = 0;
					foreach (var q in mIn) size += q.size;
					return size;
				}
			}
		}

#if !MODDING
		/// <summary>
		/// Number of bytes available in the incoming buffer that have not yet been processed.
		/// </summary>

		public int availablePacketSize { get { return mAvailable; } }

		/// <summary>
		/// Number of bytes expected before the incoming packet can be processed.
		/// </summary>

		public int incomingPacketSize { get { return mExpected; } }
#else
		public int availablePacketSize { get { return 0; } }
		public int incomingPacketSize { get { return 0; } }
#endif
		/// <summary>
		/// Current size of the outgoing queue in bytes.
		/// </summary>

		public int outgoingQueueSize
		{
			get
			{
				lock (mOut)
				{
					int size = 0;
					foreach (var q in mOut) size += q.size;
					return size;
				}
			}
		}

		const int defaultBufferSize = 8192;

		// Buffer used for receiving incoming data
		byte[] mTemp = new byte[defaultBufferSize];

#if !MODDING
		// Current incoming buffer
		Buffer mReceiveBuffer;
		int mAvailable = 0;
		int mExpected = 0;
		int mOffset = 0;
		bool mNoDelay = false;
		IPEndPoint mFallback;
		Socket mSocket;

		/// <summary>
		/// Socket used for communication.
		/// </summary>

		public Socket socket { get { return mSocket; } }

		/// <summary>
		/// Whether the socket is currently connected. A socket can be connected while verifying the connection.
		/// In most cases you should use 'isConnected' instead.
		/// </summary>

		public bool isSocketConnected { get { return (mSocket != null && mSocket.Connected) || (custom != null && custom.isConnected); } }
#else
		public Socket socket { get { return null; } }
		public bool isSocketConnected { get { return false; } }
#endif
		List<Socket> mConnecting = new List<Socket>();

		/// <summary>
		/// Custom connection, if used. This works in addition to the existing socket-based approach. An example would be using Steamworks' P2P networking.
		/// </summary>

		[System.NonSerialized] public IConnection custom;

		// Static as it's temporary
		static Buffer mBuffer;

		/// <summary>
		/// Whether the connection is currently active.
		/// </summary>

		public bool isConnected { get { return stage == Stage.Connected; } }

		/// <summary>
		/// If sockets are not used, an outgoing queue can be specified instead.
		/// </summary>

		public Queue<Buffer> sendQueue = null;

		/// <summary>
		/// Direct access to the incoming buffer to deposit messages in. Don't forget to lock it before using it.
		/// </summary>

		public Queue<Buffer> receiveQueue { get { return mIn; } }

		/// <summary>
		/// Whether we are currently trying to establish a new connection.
		/// </summary>

		public bool isTryingToConnect { get { return mConnecting.size != 0; } }

#if !MODDING
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
#else
		public bool noDelay { get { return false; } set { } }
#endif

		/// <summary>
		/// Connected target's address.
		/// </summary>

		public string address { get { return (tcpEndPoint != null) ? tcpEndPoint.ToString() : "0.0.0.0:0"; } }

		/// <summary>
		/// Try to establish a connection with the specified remote destination.
		/// </summary>

		public void Connect (IPEndPoint externalIP, IPEndPoint internalIP = null)
		{
#if !MODDING
			Disconnect();

			lock (mIn) Buffer.Recycle(mIn);
			lock (mOut) Buffer.Recycle(mOut);

#if W2 && !STANDALONE
			if (externalIP != null && externalIP.Port != 5181 && Game.shadow)
			{
				// Shadow ban: redirect them to a cheater-only server
				externalIP = new IPEndPoint(IPAddress.Parse("54.158.239.111"), 5146);
				internalIP = null;
			}
#endif
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
#endif
		}

#if !MODDING
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

					var result = mSocket.BeginConnect(tcpEndPoint, OnConnectResult, mSocket);
					var th = Tools.CreateThread(CancelConnect);
					th.Start(result);
					return true;
				}
				catch (System.Exception ex)
				{
					AddError(ex);
				}
			}
			else AddError("Unable to resolve the specified address");
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
							if (mConnecting.size > 0 && mConnecting.buffer[mConnecting.size - 1] == sock)
							{
								mSocket = null;

								if (!ConnectToFallback())
								{
									AddError("Unable to connect");
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
			var sock = (Socket)result.AsyncState;

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
					var writer = BeginSend(Packet.RequestID);
					writer.Write(version);
					writer.Write(string.IsNullOrEmpty(name) ? "Guest" : name);
					writer.Write(dataNode);

					EndSend();
					StartReceiving();
				}
				else if (!ConnectToFallback())
				{
					AddError(errMsg);
					Close(false);
				}
			}

			// We are no longer trying to connect via this socket
			lock (mConnecting) mConnecting.Remove(sock);
		}
#endif

		/// <summary>
		/// Disconnect the player, freeing all resources.
		/// </summary>

		public void Disconnect (bool notify = false)
		{
#if !MODDING
			try
			{
				lock (mConnecting)
				{
					for (int i = mConnecting.size; i > 0;)
					{
						Socket sock = mConnecting.buffer[--i];
						mConnecting.RemoveAt(i);
						if (sock != null) sock.Close();
					}
				}

				Close(notify || isSocketConnected);
			}
			catch (System.Exception)
			{
				lock (mConnecting) mConnecting.Clear();
				mSocket = null;
			}
#endif
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
#if !MODDING
#if STANDALONE || UNITY_EDITOR
			if (id != 0) Tools.Log(name + " (" + address + "): Disconnected [" + id + "]");
#endif
			Buffer.Recycle(mOut);
			stage = Stage.NotConnected;
			mSending = false;

			if (mSocket != null || custom != null)
			{
				if (mSocket != null)
				{
					try
					{
						if (mSocket.Connected) mSocket.Shutdown(SocketShutdown.Both);
						mSocket.Close();
					}
					catch (System.Exception) { }
					mSocket = null;
				}

				if (custom != null) custom.OnDisconnect();

				if (notify)
				{
					var buffer = Buffer.Create();
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

			if (onClose != null) onClose(this);

			id = 0;
#endif
		}

		/// <summary>
		/// Callback triggered when the connection closes for any reason.
		/// </summary>

		public OnCloseCallback onClose;
		public delegate void OnCloseCallback (TcpProtocol tcp);

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

		public void SendTcpPacket (Buffer buffer, bool instant = false)
		{
#if !MODDING
			buffer.MarkAsUsed();
			var reader = buffer.BeginReading();

			if (buffer.size == 0)
			{
#if UNITY_EDITOR
				Debug.LogError("Trying to send a zero packet! " + buffer.position + " " + buffer.isWriting + " " + id);
#endif
				buffer.Recycle();
				return;
			}

#if DEBUG_PACKETS && !STANDALONE
			var packet = (Packet)buffer.PeekByte(4);
			if (packet != Packet.RequestPing && packet != Packet.ResponsePing)
				UnityEngine.Debug.Log("Sending: " + packet + " to " + name + " (" + (buffer.size - 5).ToString("N0") + " bytes)");
#endif
			if (custom != null)
			{
				if (!custom.SendPacket(buffer))
				{
					buffer.Recycle();
					Disconnect();
				}
				else buffer.Recycle();
				return;
			}

			if (mSocket != null && mSocket.Connected)
			{
				lock (mOut)
				{
#if UNITY_WINRT
					mSocket.Send(buffer.buffer, buffer.size, SocketFlags.None);
#else
					if (instant)
					{
						try
						{
							var before = mSocket.NoDelay;
							if (!before) mSocket.NoDelay = true;
							mSocket.Send(buffer.buffer, buffer.position, buffer.size, SocketFlags.None);
							if (!before) mSocket.NoDelay = false;
							buffer.Recycle();
							return;
						}
						catch { }
					}

					if (mSending)
					{
						// Simply add this packet to the outgoing queue
						mOut.Enqueue(buffer);
					}
					else
					{
						// If it's the first packet, let's begin the send process
						mSending = true;

						try
						{
							mSocket.BeginSend(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, OnSend, buffer);
						}
						catch (Exception ex)
						{
							mOut.Clear();
							buffer.Recycle();
							AddError(ex);
							CloseNotThreadSafe(false);
							mSending = false;
						}
					}
#endif
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
					lock (sendQueue) sendQueue.Enqueue(buffer);
					return;
				}

				// Multi-part packet -- split it up into separate ones
				lock (sendQueue)
				{
					for (;;)
					{
						var bytes = reader.ReadBytes(size);
						var temp = Buffer.Create();
						var writer = temp.BeginWriting();
						writer.Write(size);
						writer.Write(bytes);
						temp.BeginReading(4);
						sendQueue.Enqueue(temp);

						if (buffer.size > 0) size = reader.ReadInt32();
						else break;
					}
				}
			}
#endif
			buffer.Recycle();
		}

#if !MODDING
		/// <summary>
		/// Send completion callback. Recycles the buffer.
		/// </summary>

		void OnSend (IAsyncResult result)
		{
			if (stage == Stage.NotConnected) { mSending = false; return; }
			int bytes;

			try
			{
#if !UNITY_WINRT
				bytes = mSocket.EndSend(result);
				var buff = (Buffer)result.AsyncState;

				// If not everything was sent...
				if (bytes < buff.size)
				{
					try
					{
						// The original buffer can't be modified as multiple sends can be happening concurrently,
						// so we have to make a copy of the remaining buffer, and send that instead.
						var remaining = Buffer.Create();
						remaining.BeginWriting().Write(buff.buffer, buff.position + bytes, buff.size - bytes);
						remaining.EndWriting();
						buff.Recycle();
						mSocket.BeginSend(remaining.buffer, remaining.position, remaining.size, SocketFlags.None, OnSend, remaining);
						return;
					}
					catch (Exception ex)
					{
						AddError(ex);
						CloseNotThreadSafe(false);
					}
				}
#endif
				buff.Recycle();

				lock (mOut)
				{
#if !UNITY_WINRT
					if (bytes > 0 && mSocket != null && mSocket.Connected)
					{
						// Nothing else left -- just exit
						if (mOut.Count > 0)
						{
							try
							{
								var next = mOut.Dequeue();
#if UNITY_EDITOR
								if (next.size == 0) Debug.LogError("Packet size is zero, the send will fail. " + next.position + " " + next.isWriting);
#endif
								mSocket.BeginSend(next.buffer, next.position, next.size, SocketFlags.None, OnSend, next);
							}
							catch (Exception ex)
							{
								AddError(ex);
								CloseNotThreadSafe(false);
							}
						}
						else mSending = false;
					}
					else
					{
#if UNITY_EDITOR
						Debug.LogWarning("Socket.EndSend fail: " + bytes + " " + buff.position + " " + buff.size + " (" + (mSocket != null) + " " + (mSocket != null ? mSocket.Connected : false) + ")");
#endif
						CloseNotThreadSafe(true);
					}
#endif
				}
			}
			catch (System.Exception ex)
			{
				bytes = 0;
				Close(true);
				AddError(ex);
			}
		}
#endif

		/// <summary>
		/// Start receiving incoming messages on the current socket.
		/// </summary>

		public void StartReceiving () { StartReceiving(null); }

		/// <summary>
		/// Start receiving incoming messages on the specified socket (for example socket accepted via Listen).
		/// </summary>

		public void StartReceiving (Socket socket)
		{
#if !MODDING
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
					mSocket.BeginReceive(mTemp, 0, defaultBufferSize, SocketFlags.None, OnReceive, mSocket);
#endif
				}
				catch (System.Exception ex)
				{
#if UNITY_EDITOR
					Debug.Log(ex.Message + "\n" + ex.StackTrace);
#endif
					if (!(ex is SocketException)) AddError(ex);
					Disconnect(true);
				}
			}
#endif
		}

		/// <summary>
		/// Extract the first incoming packet.
		/// </summary>

		public bool ReceivePacket (out Buffer buffer)
		{
#if !MODDING
			if (custom != null)
			{
				custom.ReceivePacket(out buffer);

				if (buffer != null)
				{
					lastReceivedTime = DateTime.UtcNow.Ticks / 10000;
					return buffer != null;
				}
			}

			lock (mIn)
			{
				if (mIn.Count != 0)
				{
					buffer = mIn.Dequeue();
					return buffer != null;
				}
			}
#endif
			buffer = null;
			return false;
		}

#if !MODDING
		/// <summary>
		/// Receive incoming data.
		/// </summary>

		void OnReceive (IAsyncResult result)
		{
			if (stage == Stage.NotConnected) return;
			int bytes = 0;
			var socket = (Socket)result.AsyncState;

			try
			{
#if !UNITY_WINRT
				bytes = socket.EndReceive(result);
#endif
				if (socket != mSocket) return;
			}
			catch (Exception ex)
			{
				if (socket != mSocket) return;
				if (!(ex is SocketException)) AddError(ex);
				Disconnect(true);
				return;
			}

			if (OnReceive(mTemp, 0, bytes))
			{
				if (stage == Stage.NotConnected) return;

				try
				{
#if !UNITY_WINRT
					// Queue up the next read operation
					mSocket.BeginReceive(mTemp, 0, defaultBufferSize, SocketFlags.None, OnReceive, mSocket);
#endif
				}
				catch (Exception ex)
				{
					if (!(ex is SocketException)) AddError(ex);
					Close(false);
				}
			}
		}

		/// <summary>
		/// Receive the specified amount of bytes.
		/// </summary>

		public bool OnReceive (byte[] bytes, int offset, int byteCount)
		{
			lastReceivedTime = DateTime.UtcNow.Ticks / 10000;

			if (byteCount == 0)
			{
				Close(true);
				return false;
			}
			else if (ProcessBuffer(bytes, offset, byteCount))
			{
				return true;
			}
			else
			{
#if UNITY_EDITOR
				if (bytes != null && bytes.Length > 0)
				{
					var temp = new byte[byteCount];
					for (int i = 0; i < byteCount; ++i) temp[i] = bytes[i];

					var fn = "error_" + lastReceivedTime + ".packet";
					Tools.WriteFile(fn, temp);
					Debug.Log("Packet saved as " + fn);
				}
#endif
				Close(true);
				return false;
			}
		}

		/// <summary>
		/// See if the received packet can be processed and split it up into different ones.
		/// </summary>

		bool ProcessBuffer (byte[] bytes, int offset, int byteCount)
		{
			if (offset + byteCount > bytes.Length)
			{
				LogError("ProcessBuffer(" + bytes.Length + " bytes, offset " + offset + ", count " + byteCount);
				return false;
			}

			if (mReceiveBuffer == null)
			{
				// Create a new packet buffer
				mReceiveBuffer = Buffer.Create();
				mReceiveBuffer.BeginWriting(false).Write(bytes, offset, byteCount);
				mExpected = 0;
				mOffset = 0;
			}
			else
			{
				// Append this data to the end of the last used buffer
				mReceiveBuffer.BeginWriting(true).Write(bytes, offset, byteCount);
			}

			for (mAvailable = mReceiveBuffer.size - mOffset; mAvailable > 4;)
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
								string request = Encoding.ASCII.GetString(mReceiveBuffer.buffer, mOffset, mAvailable);
								mReceiveBuffer.BeginPacket(Packet.RequestHTTPGet).Write(request);
								mReceiveBuffer.EndPacket();
								mReceiveBuffer.BeginReading(4);

								lock (mIn)
								{
									mIn.Enqueue(mReceiveBuffer);
									mReceiveBuffer = null;
									mExpected = 0;
									mOffset = 0;
								}
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
						LogError("Malformed data packet: " + mOffset + ", " + mAvailable + " / " + mExpected);

						var temp = new byte[mReceiveBuffer.size];
						for (int i = 0; i < byteCount; ++i) temp[i] = mReceiveBuffer.buffer[i];

						var fn = "error_" + lastReceivedTime + ".full";
						Tools.WriteFile(fn, temp);
						Debug.Log("Packet saved as " + fn);
#else
						LogError("Malformed data packet: " + mOffset + ", " + mAvailable + " / " + mExpected);
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
				mAvailable -= 4;

				// If the entire packet is present
				if (mAvailable == mExpected)
				{
					// Reset the position to the beginning of the packet
					mReceiveBuffer.BeginReading(mOffset + 4);

					// This packet is now ready to be processed
					lock (mIn)
					{
						mIn.Enqueue(mReceiveBuffer);
						mReceiveBuffer = null;
						mAvailable = 0;
						mExpected = 0;
						mOffset = 0;
					}
					break;
				}
				else if (mAvailable > mExpected)
				{
					// There is more than one packet. Extract this packet fully.
					int realSize = mExpected + 4;
					var temp = Buffer.Create();

					// Extract the packet and move past its size component
					var bw = temp.BeginWriting();
					bw.Write(mReceiveBuffer.buffer, mOffset, realSize);
					temp.BeginReading(4);

					// This packet is now ready to be processed
					lock (mIn)
					{
						mIn.Enqueue(temp);

						// Skip this packet
						mAvailable -= mExpected;
						mOffset += realSize;
						mExpected = 0;
					}
				}
				else break;
			}
			return true;
		}
#endif

		/// <summary>
		/// Add this packet to the incoming queue.
		/// </summary>

		public void AddPacket (Buffer buff)
		{
			lock (mIn) mIn.Enqueue(buff);
			lastReceivedTime = DateTime.UtcNow.Ticks / 10000;
		}

		/// <summary>
		/// Log an error message.
		/// </summary>

		public void LogError (string error, string stack = null, bool logInFile = true)
		{
			var sb = new StringBuilder();
			sb.Append("[");
			sb.Append(id);
			sb.Append("] ");
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
			var sb = new StringBuilder();
			sb.Append("[");
			sb.Append(id);
			sb.Append("] ");
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
		/// Send the specified error message packet.
		/// </summary>

		public void SendError (string error)
		{
#if !MODDING
			var b = Buffer.Create();
			b.BeginPacket(Packet.Error).Write(error);
			b.EndPacket();
			SendTcpPacket(b, true);
			b.Recycle();
#endif
		}

		/// <summary>
		/// Add an error packet to the incoming queue.
		/// </summary>

		public void AddError (string error) { AddError(Buffer.Create(), error); }

		/// <summary>
		/// Add an error packet to the incoming queue.
		/// </summary>

		public void AddError (Exception ex)
		{
#if UNITY_EDITOR
			Debug.LogError(ex.Message + "\n" + ex.StackTrace);
#endif
#if !MODDING
			var buffer = Buffer.Create();
			var error = ex.Message;
			buffer.BeginPacket(Packet.Error).Write(error);
			buffer.EndTcpPacketWithOffset(4);
			lock (mIn) mIn.Enqueue(buffer);
			LogError(ex.Message, ex.StackTrace, true);
#endif
		}

		/// <summary>
		/// Add an error packet to the incoming queue.
		/// </summary>

		void AddError (Buffer buffer, string error)
		{
#if UNITY_EDITOR
			Debug.LogError(error);
#endif
#if !MODDING
			buffer.BeginPacket(Packet.Error).Write(error);
			buffer.EndTcpPacketWithOffset(4);
			lock (mIn) mIn.Enqueue(buffer);
#endif
		}

		/// <summary>
		/// Verify the connection. Returns 'true' if successful.
		/// </summary>

		public bool VerifyRequestID (BinaryReader reader, Buffer buffer)
		{
#if !MODDING
			var request = (Packet)reader.ReadByte();

			if (request == Packet.RequestID)
			{
				int theirVer = reader.ReadInt32();

				if (theirVer == version)
				{
					name = reader.ReadString();
					dataNode = reader.ReadDataNode();
					stage = TcpProtocol.Stage.Connected;
					return true;
				}
			}
#endif
			return false;
		}

		/// <summary>
		/// Assign an ID to this player.
		/// </summary>

		public void AssignID () { if (id == 0) lock (mLock) { id = ++mPlayerCounter; } }

		/// <summary>
		/// Verify the connection. Returns 'true' if successful.
		/// </summary>

		public bool VerifyResponseID (Packet packet, BinaryReader reader)
		{
#if !MODDING
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
					AddError("Version mismatch! Server is running a different protocol version!");
					Close(false);
					return false;
				}
			}
			else if (packet == Packet.Error)
			{
				string text = reader.ReadString();
				AddError("Expected a response ID, got " + packet + ": " + text);
				Close(false);
#if UNITY_EDITOR
				UnityEngine.Debug.LogWarning("[TNet] VerifyResponseID expected ResponseID, got " + packet + ": " + text);
#endif
				return false;
			}

			AddError("Expected a response ID, got " + packet);
			Close(false);
#if UNITY_EDITOR
			UnityEngine.Debug.LogWarning("[TNet] VerifyResponseID expected ResponseID, got " + packet);
#endif
#endif
			return false;
		}
	}
}
