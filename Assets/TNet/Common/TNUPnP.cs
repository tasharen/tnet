//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

// Unity has an outdated version of Mono that doesn't have the NetworkInformation namespace.
#if !UNITY_3_4 && !UNITY_3_5 && !UNITY_4_0 && !UNITY_4_1 && !UNITY_4_2 && !UNITY_4_3 && !UNITY_4_4
using System.Net.NetworkInformation;
#endif

namespace TNet
{
/// <summary>
/// Universal Plug & Play functionality: auto-detect external IP and open external ports.
/// Technically this class would be a fair bit shorter if I had used an XML parser...
/// However I'd rather not, as adding the XML library also adds 1 megabyte to the executable's size in Unity.
/// 
/// Example usage:
/// UPnP p = new UPnP();
/// p.OpenTCP(5127);
/// 
/// Don't worry about closing ports. This class will do it for you when its instance gets destroyed.
/// </summary>

public class UPnP
{
	public enum Status
	{
		Inactive,
		Searching,
		Success,
		Failure,
	}

	Status mStatus = Status.Inactive;
	IPAddress mLocalAddress = IPAddress.None;
	IPAddress mGatewayAddress = IPAddress.None;
	IPAddress mExternalAddress = IPAddress.None;
	
	string mGatewayURL = null;
	string mControlURL = null;
	string mServiceType = null;
	List<Thread> mThreads = new List<Thread>();
	List<int> mPorts = new List<int>();

	public delegate void OnPortRequest (UPnP up, int port, ProtocolType protocol, bool success);

	class ExtraParams
	{
		public Thread th;
		public string action;
		public string request;
		public int port;
		public ProtocolType protocol;
		public OnPortRequest callback;
	}

	/// <summary>
	/// Name that will show up on the gateway's list.
	/// </summary>

	public string name = "TNetServer";

	/// <summary>
	/// Current UPnP status.
	/// </summary>

	public Status status { get { return mStatus; } }

	/// <summary>
	/// Local IP address, such as 192.168.1.10
	/// </summary>

	public IPAddress localAddress { get { return mLocalAddress; } }

	/// <summary>
	/// Gateway's IP address, such as 192.168.1.1
	/// </summary>

	public IPAddress gatewayAddress { get { return mGatewayAddress; } }

	/// <summary>
	/// External IP address, such as 50.128.231.100.
	/// </summary>

	public IPAddress externalAddress { get { return mExternalAddress; } }

	/// <summary>
	/// Whether there are threads active.
	/// </summary>

	public bool hasThreadsActive { get { return mThreads.size > 0; } }

	/// <summary>
	/// Start the Universal Plug & Play discovery process.
	/// </summary>

	public UPnP ()
	{
		IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName());

		for (int i = 0; i < ips.Length; ++i)
		{
			IPAddress addr = ips[i];

			if (IsValidAddress(addr))
			{
				mLocalAddress = addr;
				mExternalAddress = addr;
				break;
			}
		}

		Thread th = new Thread(ThreadDiscover);
		mThreads.Add(th);
		th.Start(th);
	}

	/// <summary>
	/// Wait for all threads to finish.
	/// </summary>

	~UPnP ()
	{
		// Close all ports that we've opened
		for (int i = mPorts.size; i > 0; )
		{
			int id = mPorts[--i];
			int port = (id >> 8);
			bool tcp = (port & 1) == 1;
			Close(port, tcp, null);
		}

		// Wait for all ports to close
		while (mThreads.size > 0) Thread.Sleep(1);
	}

	/// <summary>
	/// Helper function that determines if this is a valid address.
	/// </summary>

	static bool IsValidAddress (IPAddress address)
	{
		if (address.AddressFamily != AddressFamily.InterNetwork) return false;
		if (address.Equals(IPAddress.Loopback)) return false;
		if (address.Equals(IPAddress.None)) return false;
		if (address.Equals(IPAddress.Any)) return false;
		return true;
	}

	/// <summary>
	/// Gateway discovery logic is done on a separate thread so that it's not blocking the main thread.
	/// </summary>

	void ThreadDiscover (object obj)
	{
		Thread th = (Thread)obj;
		mStatus = Status.Searching;

		// Unity has an outdated version of Mono that doesn't have the NetworkInformation namespace.
#if !UNITY_3_4 && !UNITY_3_5 && !UNITY_4_0 && !UNITY_4_1 && !UNITY_4_2 && !UNITY_4_3 && !UNITY_4_4
		NetworkInterface[] networks = NetworkInterface.GetAllNetworkInterfaces();

		for (int i = 0; i < networks.Length; ++i)
		{
			NetworkInterface ni = networks[i];
			GatewayIPAddressInformationCollection gis = ni.GetIPProperties().GatewayAddresses;

			for (int b = 0; b < gis.Count; ++b)
			{
				IPAddress address = gis[b].Address;
				
				if (IsValidAddress(address) && ThreadConnect(address))
				{
					mStatus = Status.Success;
					mGatewayAddress = address;
					lock (mThreads) mThreads.Remove(th);
					return;
				}
			}
		}
#else
		string local = localAddress.ToString();
		string gateway = local.Substring(0, local.LastIndexOf('.')) + ".1";
		IPAddress address = IPAddress.Parse(gateway);

		if (IsValidAddress(address) && ThreadConnect(address))
		{
			mStatus = Status.Success;
			mGatewayAddress = address;
			lock (mThreads) mThreads.Remove(th);
			return;
		}
#endif
		mStatus = Status.Failure;
		lock (mThreads) mThreads.Remove(th);
	}

	/// <summary>
	/// Try to initialize UPnP with the specified gateway address.
	/// </summary>

	bool ThreadConnect (IPAddress address)
	{
		string request = "M-SEARCH * HTTP/1.1\r\n" +
			"HOST: " + address + ":1900\r\n" +
			"ST:upnp:rootdevice\r\n" +
			"MAN:\"ssdp:discover\"\r\n" +
			"MX:3\r\n\r\n";

		// UPnP gateway is listening for HTTP-based communication via UDP
		Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 3000);

		// Send the discovery packet to the gateway
		byte[] bytes = Encoding.ASCII.GetBytes(request);
		socket.SendTo(bytes, bytes.Length, SocketFlags.None, new IPEndPoint(address, 1900));

		// Receive a response
		EndPoint sourceAddress = new IPEndPoint(IPAddress.Any, 0);
		byte[] data = new byte[2048];
		int count = socket.ReceiveFrom(data, ref sourceAddress);
		socket.Close();
		string response = Encoding.ASCII.GetString(data, 0, count);

		// Find the "Location" header
		int index = response.IndexOf("LOCATION:");
		if (index == -1) return false;
		index += 9;
		int end = response.IndexOf('\r', index);
		if (end == -1) return false;

		// Base URL: http://192.168.2.1:2555/upnp/f3710630-b3ce-348c-b5a5-4c9d74f6ee99/desc.xml
		string baseURL = response.Substring(index, end - index).Trim();

		// Gateway URL: http://192.168.2.1:2555
		int offset = baseURL.IndexOf("://");
		offset = baseURL.IndexOf('/', offset + 3);
		mGatewayURL = baseURL.Substring(0, offset);

		// Get the port control URL
		if (!GetControlURL(baseURL)) return false;

		// Get the external IP address
		return GetExternalAddress();
	}

	/// <summary>
	/// Determine the external IP address.
	/// </summary>

	bool GetExternalAddress ()
	{
		string response = SendRequest("GetExternalIPAddress", null, 5000);
		if (string.IsNullOrEmpty(response)) return false;

		string tag = "<NewExternalIPAddress>";
		int start = response.IndexOf(tag);
		if (start == -1) return false;
		start += tag.Length;

		int end = response.IndexOf("</NewExternalIPAddress>", start);
		if (end == -1) return false;

		string address = response.Substring(start, end - start);
		return IPAddress.TryParse(address, out mExternalAddress);
	}

	/// <summary>
	/// Helper function that returns the response of the specified web request.
	/// </summary>

	static string GetResponse (WebRequest request)
	{
		string response = "";

		try
		{
			WebResponse webResponse = request.GetResponse();
			Stream stream = webResponse.GetResponseStream();

			byte[] bytes = new byte[2048];

			for (; ; )
			{
				int count = stream.Read(bytes, 0, bytes.Length);
				if (count > 0) response += Encoding.ASCII.GetString(bytes, 0, count);
				else break;
			}
		}
		catch (System.Exception)
		{
			return null;
		}
		return response;
	}

	/// <summary>
	/// Get the port control URL from the gateway.
	/// </summary>

	bool GetControlURL (string url)
	{
		string response = GetResponse(WebRequest.Create(url));
		if (string.IsNullOrEmpty(response)) return false;

		// For me the full hierarchy of nodes was:
		// root -> device -> deviceList -> device (again) -> deviceList (again) -> service,
		// where the <serviceType> node had an identifier ending in one of the prefixes below.
		// The service node with this type then contained <controlURL> node with the actual URL.
		// TLDR: It's just a hell of a lot easier to go straight for the prize instead.

		// IP gateway (Router, cable modem)
		mServiceType = "WANIPConnection";
		int offset = response.IndexOf(mServiceType);

		// PPP gateway (ADSL modem)
		if (offset == -1)
		{
			mServiceType = "WANPPPConnection";
			offset = response.IndexOf(mServiceType);
			if (offset == -1) return false;
		}

		int end = response.IndexOf("</service>", offset);
		if (end == -1) return false;

		int start = response.IndexOf("<controlURL>", offset, end - offset);
		if (start == -1) return false;
		start += 12;

		end = response.IndexOf("</controlURL>", start, end - start);
		if (end == -1) return false;

		// Final URL
		mControlURL = mGatewayURL + response.Substring(start, end - start);
		return true;
	}

	/// <summary>
	/// Send a SOAP request to the gateway.
	/// </summary>

	string SendRequest (string action, string content, int timeout)
	{
		string request = "<?xml version=\"1.0\"?>\n" +
			"<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=" +
			"\"http://schemas.xmlsoap.org/soap/encoding/\">\n<s:Body>\n" +
			"<m:" + action + " xmlns:m=\"" + mServiceType + "\">\n";

		if (!string.IsNullOrEmpty(content)) request += content;

		request += "</m:" + action + ">\n</s:Body>\n</s:Envelope>\n";

		byte[] b = Encoding.UTF8.GetBytes(request);

		try
		{
			WebRequest web = HttpWebRequest.Create(mControlURL);
			web.Timeout = timeout;
			web.Method = "POST";
			web.Headers.Add("SOAPACTION", "\"" + mControlURL + "#" + action + "\"");
			web.ContentType = "text/xml";
			web.ContentLength = b.Length;
			web.GetRequestStream().Write(b, 0, b.Length);
			return GetResponse(web);
		}
		catch (System.Exception)
		{
			return null;
		}
	}

	/// <summary>
	/// Open up a TCP port on the gateway.
	/// </summary>

	public void OpenTCP (int port) { Open(port, true, null); }

	/// <summary>
	/// Open up a UDP port on the gateway.
	/// </summary>

	public void OpenUDP (int port) { Open(port, false, null); }

	/// <summary>
	/// Open up a TCP port on the gateway.
	/// </summary>

	public void OpenTCP (int port, OnPortRequest callback) { Open(port, true, callback); }

	/// <summary>
	/// Open up a UDP port on the gateway.
	/// </summary>

	public void OpenUDP (int port, OnPortRequest callback) { Open(port, false, callback); }

	/// <summary>
	/// Open up a port on the gateway.
	/// </summary>

	void Open (int port, bool tcp, OnPortRequest callback)
	{
		int id = (port << 8) | (tcp ? 1 : 0);

		if (mStatus != Status.Failure && !mPorts.Contains(id))
		{
			mPorts.Add(id);

			ExtraParams xp = new ExtraParams();
			xp.callback = callback;
			xp.port = port;
			xp.protocol = tcp ? ProtocolType.Tcp : ProtocolType.Udp;
			xp.action = "AddPortMapping";
			xp.request = "<NewRemoteHost></NewRemoteHost>\n" +
				"<NewExternalPort>" + port + "</NewExternalPort>\n" +
				"<NewProtocol>" + (tcp ? "TCP" : "UDP") + "</NewProtocol>\n" +
				"<NewInternalPort>" + port + "</NewInternalPort>\n" +
				"<NewInternalClient>" + localAddress + "</NewInternalClient>\n" +
				"<NewEnabled>1</NewEnabled>\n" +
				"<NewPortMappingDescription>" + name + "</NewPortMappingDescription>\n" +
				"<NewLeaseDuration>0</NewLeaseDuration>\n";

			if (callback != null)
			{
				xp.th = new Thread(ThreadRequest);
				lock (mThreads) mThreads.Add(xp.th);
				xp.th.Start(xp);
			}
			else ThreadRequest(xp);
		}
		else if (callback != null)
		{
			callback(this, port, tcp ? ProtocolType.Tcp : ProtocolType.Udp, false);
		}
	}

	/// <summary>
	/// Stop port forwarding that was set up earlier.
	/// </summary>

	public void CloseTCP (int port) { Close(port, true, null); }

	/// <summary>
	/// Stop port forwarding that was set up earlier.
	/// </summary>

	public void CloseUDP (int port) { Close(port, false, null); }

	/// <summary>
	/// Stop port forwarding that was set up earlier.
	/// </summary>

	public void CloseTCP (int port, OnPortRequest callback) { Close(port, true, callback); }

	/// <summary>
	/// Stop port forwarding that was set up earlier.
	/// </summary>

	public void CloseUDP (int port, OnPortRequest callback) { Close(port, false, callback); }

	/// <summary>
	/// Stop port forwarding that was set up earlier.
	/// </summary>

	void Close (int port, bool tcp, OnPortRequest callback)
	{
		int id = (port << 8) | (tcp ? 1 : 0);

		if (mStatus != Status.Failure && mPorts.Remove(id))
		{
			ExtraParams xp = new ExtraParams();
			xp.callback = callback;
			xp.port = port;
			xp.protocol = tcp ? ProtocolType.Tcp : ProtocolType.Udp;
			xp.action = "DeletePortMapping";
			xp.request = "<NewRemoteHost></NewRemoteHost>\n" +
				"<NewExternalPort>" + port + "</NewExternalPort>\n" +
				"<NewProtocol>" + (tcp ? "TCP" : "UDP") + "</NewProtocol>\n";

			if (callback != null)
			{
				xp.th = new Thread(ThreadRequest);
				lock (mThreads) mThreads.Add(xp.th);
				xp.th.Start(xp);
			}
			else ThreadRequest(xp);
		}
		else if (callback != null)
		{
			callback(this, port, tcp ? ProtocolType.Tcp : ProtocolType.Udp, false);
		}
	}

	/// <summary>
	/// Thread callback that requests a port to be opened.
	/// </summary>

	void ThreadRequest (object obj)
	{
		while (mStatus == Status.Searching) Thread.Sleep(1);
		ExtraParams xp = (ExtraParams)obj;
		string response = (mStatus == Status.Success) ? SendRequest(xp.action, xp.request, 10000) : null;
		if (xp.callback != null)
			xp.callback(this, xp.port, xp.protocol, !string.IsNullOrEmpty(response));
		if (xp.th != null) lock (mThreads) mThreads.Remove(xp.th);
		//Console.WriteLine("Result: " + response);
	}
}
}
