//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;

#if UNITY_IPHONE || !UNITY_WEBPLAYER
using System.Net.NetworkInformation;
#endif

namespace TNet
{
/// <summary>
/// Generic sets of helper functions used within TNet.
/// </summary>

static public class Tools
{
	static string mChecker = null;

	/// <summary>
	/// Get or set the URL that will perform the IP check. The URL-returned value should be simply the IP address:
	/// "255.255.255.255".
	/// Note that the server must have a valid policy XML if it's accessed from a Unity web player build.
	/// </summary>

	static public string ipCheckerUrl
	{
		get
		{
			return mChecker;
		}
		set
		{
			if (mChecker != value)
			{
				mChecker = value;
				mLocalAddress = null;
				mExternalAddress = null;
			}
		}
	}

	static IPAddress mLocalAddress;
	static IPAddress mExternalAddress;

	/// <summary>
	/// Whether the external IP address is reliable. It's set to 'true' when it gets resolved successfully.
	/// </summary>

	static public bool isExternalIPReliable = false;

	/// <summary>
	/// Generate a random port from 10,000 to 60,000.
	/// </summary>

	static public int randomPort { get { return 10000 + (int)(System.DateTime.UtcNow.Ticks % 50000); } }

	static string mBasePath = null;

	/// <summary>
	/// Path to the persistent data path.
	/// </summary>

	static public string persistentDataPath
	{
		get
		{
			if (mBasePath == null)
			{
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IPHONE || UNITY_WEBPLAYER || UNITY_WINRT || UNITY_FLASH)
				mBasePath = UnityEngine.Application.persistentDataPath;
#else
				mBasePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
#endif
				mBasePath = mBasePath.Replace("\\", "/");
			}
			return mBasePath;
		}
		set
		{
			mBasePath = value;
		}
	}

#if !UNITY_WEBPLAYER && !UNITY_WINRT
	static List<NetworkInterface> mInterfaces = null;

	/// <summary>
	/// Return the list of operational network interfaces.
	/// </summary>

	static public List<NetworkInterface> networkInterfaces
	{
		get
		{
			if (mInterfaces == null)
			{
				mInterfaces = new List<NetworkInterface>();
				NetworkInterface[] list = NetworkInterface.GetAllNetworkInterfaces();

				foreach (NetworkInterface ni in list)
				{
					if (ni.Supports(NetworkInterfaceComponent.IPv4) &&
						(ni.OperationalStatus == OperationalStatus.Up ||
						ni.OperationalStatus == OperationalStatus.Unknown))
						mInterfaces.Add(ni);
				}
			}
			return mInterfaces;
		}
	}
#endif

	static List<IPAddress> mAddresses = null;

	/// <summary>
	/// Return the list of local addresses. There is usually more than one with today's computers (IPv4, IPv6, tunnels, VLANs, etc).
	/// </summary>

	static public List<IPAddress> localAddresses
	{
		get
		{
			if (mAddresses == null)
			{
				mAddresses = new List<IPAddress>();
#if !UNITY_WEBPLAYER && !UNITY_WINRT
				try
				{
					List<NetworkInterface> list = networkInterfaces;

					// First go through the network interfaces and find all non-tunnels
					for (int i = list.size; i > 0; )
					{
						NetworkInterface ni = list[--i];
						if (ni == null || ni.OperationalStatus != OperationalStatus.Up) continue;
						if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

						IPInterfaceProperties props = ni.GetIPProperties();
						if (props == null) continue;

						UnicastIPAddressInformationCollection uniAddresses = props.UnicastAddresses;
						if (uniAddresses == null) continue;

						//Tools.Log("Name: " + ni.Name + " (" + ni.Description + ")\n" +
						//	"Type: " + ni.NetworkInterfaceType + ", multicast: " + ni.SupportsMulticast + ", gateways: " + props.GatewayAddresses.Count);

						foreach (UnicastIPAddressInformation uni in uniAddresses)
						{
							//Tools.Log("    " + uni.Address + " -- " + IsValidAddress(uni.Address));
							if (IsValidAddress(uni.Address))
								mAddresses.Add(uni.Address);
						}
					}

					// Last, append the tunnels so they are at the end of the list
					for (int i = list.size; i > 0; )
					{
						NetworkInterface ni = list[--i];
						if (ni == null || ni.OperationalStatus != OperationalStatus.Up) continue;
						if (ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel) continue;

						IPInterfaceProperties props = ni.GetIPProperties();
						if (props == null) continue;

						UnicastIPAddressInformationCollection uniAddresses = props.UnicastAddresses;
						if (uniAddresses == null) continue;

						foreach (UnicastIPAddressInformation uni in uniAddresses)
						{
							if (IsValidAddress(uni.Address))
								mAddresses.Add(uni.Address);
						}
					}
				}
				catch (System.Exception) {}
#endif
#if !UNITY_IPHONE && !UNITY_EDITOR_OSX && !UNITY_STANDALONE_OSX && !UNITY_WINRT
				// Fallback method. This won't work on the iPhone, but seems to be needed on some platforms
				// where GetIPProperties either fails, or Unicast.Address access throws an exception.
				if (mAddresses.size == 0)
				{
					string hn = Dns.GetHostName();

					if (!string.IsNullOrEmpty(hn))
					{
						try
						{
							IPAddress[] ips = Dns.GetHostAddresses(hn);

							if (ips != null)
							{
								foreach (IPAddress ad in ips)
								{
									if (IsValidAddress(ad) && !mAddresses.Contains(ad))
										mAddresses.Add(ad);
								}
							}
						}
 #if UNITY_EDITOR
						catch (System.Exception ex)
						{
							UnityEngine.Debug.LogWarning(ex.Message + " (" + hn + ")");
						}
 #else
						catch (System.Exception) {}
 #endif
					}
				}
#endif
				// If everything else fails, simply use the loopback address
				if (mAddresses.size == 0) mAddresses.Add(IPAddress.Loopback);
			}
			return mAddresses;
		}
	}

	/// <summary>
	/// Default local IP address. Note that there can be more than one address in case of more than one network.
	/// </summary>

	static public IPAddress localAddress
	{
		get
		{
			if (mLocalAddress == null)
			{
				mLocalAddress = IPAddress.Loopback;
				List<IPAddress> list = localAddresses;

				if (list.size > 0)
				{
					mLocalAddress = mAddresses[0];

					for (int i = 0; i < mAddresses.size; ++i)
					{
						var addr = mAddresses[i];
						var fam = addr.AddressFamily;

						// Skip IPv6 addresses when using IPv4 and vice versa
						if (fam != TcpProtocol.defaultListenerInterface.AddressFamily) continue;

						if (fam == AddressFamily.InterNetwork)
						{
							// Hamachi IPs begin with 25
							if (addr.ToString().StartsWith("25.")) continue;
						}
						else if (fam == AddressFamily.InterNetworkV6)
						{
							// LAN IPv6 addresses start with "fe" -- so we want one of those
							if (!addr.ToString().ToLower().StartsWith("fe")) continue;
						}

						// This is a valid address
						mLocalAddress = addr;
						break;
					}
				}
			}
			return mLocalAddress;
		}
		set
		{
			mLocalAddress = value;

			if (value != null)
			{
				List<IPAddress> list = localAddresses;
				for (int i = 0; i < list.size; ++i)
					if (list[i] == value)
						return;
			}
#if UNITY_EDITOR
			UnityEngine.Debug.LogWarning("[TNet] " + value + " is not one of the local IP addresses. Strange things may happen.");
#else
			Tools.Print(value + " is not one of the local IP addresses. Strange things may happen.");
#endif
		}
	}

	/// <summary>
	/// Immediately retrieve the external IP address.
	/// Note that if the external address is not yet known, this operation may hold up the application.
	/// </summary>

	static public IPAddress externalAddress
	{
		get
		{
			if (mExternalAddress == null)
			{
				if (localAddress.AddressFamily == AddressFamily.InterNetworkV6) ResolveExternalIPv6Address();
				else ResolveExternalIPv4Address();
			}
			return mExternalAddress != null ? mExternalAddress : localAddress;
		}
	}

	public delegate void OnResolvedIPs (IPAddress local, IPAddress ext);

	/// <summary>
	/// Since calling "localAddress" and "externalAddress" would lock up the application, it's better to do it asynchronously.
	/// </summary>

	static public void ResolveIPs () { ResolveIPs(null); }

	/// <summary>
	/// Since calling "localAddress" and "externalAddress" would lock up the application, it's better to do it asynchronously.
	/// </summary>

	static public void ResolveIPs (OnResolvedIPs del)
	{
		if (isExternalIPReliable)
		{
			if (del != null) del(localAddress, externalAddress);
		}
		else
		{
			if (mOnResolve == null) mOnResolve = ResolveDummyFunc;

			lock (mOnResolve)
			{
				if (del != null) mOnResolve += del;

				if (mResolveThread == null)
				{
					mResolveThread = new Thread(ResolveThread);
					mResolveThread.Start();
				}
			}
		}
	}

	static void ResolveDummyFunc (IPAddress a, IPAddress b) {}
	static OnResolvedIPs mOnResolve;
	static Thread mResolveThread;

	/// <summary>
	/// Thread function that resolves IP addresses.
	/// </summary>

	static void ResolveThread ()
	{
		IPAddress local = localAddress;
		IPAddress ext = externalAddress;

		lock (mOnResolve)
		{
			if (mOnResolve != null) mOnResolve(local, ext);
			mResolveThread = null;
			mOnResolve = null;
		}
	}

	/// <summary>
	/// Determine the external IPv4 address by accessing an external web site.
	/// </summary>

	static void ResolveExternalIPv4Address ()
	{
#if !UNITY_WEBPLAYER
		if (ResolveExternalIP(ipCheckerUrl)) return;
		if (ResolveExternalIP("http://ipv4.icanhazip.com")) return;
		if (ResolveExternalIP("http://ipinfo.io/ip")) return;
		if (ResolveExternalIP("http://bot.whatismyipaddress.com")) return;

 #if UNITY_EDITOR
		UnityEngine.Debug.LogWarning("Unable to resolve the external IP address via " + mChecker);
 #endif
#endif
		mExternalAddress = localAddress;
	}

	/// <summary>
	/// Determine the external IPv6 address from the list of local interfaces.
	/// </summary>

	static void ResolveExternalIPv6Address ()
	{
		var locals = localAddresses;

		for (int i = 0; i < locals.size; ++i)
		{
			var local = locals[i];

			if (local.AddressFamily != AddressFamily.InterNetworkV6) continue;
			if (local.IsIPv6LinkLocal) continue; // LAN address

			mExternalAddress = local;
			isExternalIPReliable = true;
			return;
		}

#if !UNITY_WEBPLAYER
		if (ResolveExternalIP(ipCheckerUrl)) return;
		if (ResolveExternalIP("http://ipv6.icanhazip.com")) return;
		if (ResolveExternalIP("http://ipinfo.io/ip")) return;
		if (ResolveExternalIP("http://bot.whatismyipaddress.com")) return;
#endif
		mExternalAddress = localAddress;
	}

	/// <summary>
	/// Resolve the external IP using the specified URL.
	/// </summary>

	static bool ResolveExternalIP (string url)
	{
#if !UNITY_WINRT
		if (string.IsNullOrEmpty(url)) return false;

		try
		{
			WebClient web = new WebClient();
			string text = web.DownloadString(url).Trim();
			mExternalAddress = ResolveAddress(text);

			if (mExternalAddress != null && localAddress.AddressFamily == mExternalAddress.AddressFamily)
			{
				isExternalIPReliable = true;
				return true;
			}
		}
		catch (System.Exception) { }
#endif
		return false;
	}

	/// <summary>
	/// Helper function that determines if this is a valid address.
	/// </summary>

	static public bool IsValidAddress (IPAddress address)
	{
		if (address.IsIPv6SiteLocal) return false;
		if (address.IsIPv6Multicast) return false;
		if (address.Equals(IPAddress.Loopback) || address.Equals(IPAddress.IPv6Loopback)) return false;
		if (address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None)) return false;
		if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;
		if (address.AddressFamily == AddressFamily.InterNetwork && address.ToString().StartsWith("169.")) return false;
		return true;
	}

	/// <summary>
	/// Helper function that resolves the remote address.
	/// </summary>

	static public IPAddress ResolveAddress (string address)
	{
		address = address.Trim();
		if (string.IsNullOrEmpty(address)) return null;
		if (address == "localhost") return IPAddress.Loopback;

		IPAddress ip;

		if (address.Contains(":"))
		{
			string[] parts = address.Split(':');
			
			if (parts.Length == 2)
			{
				if (IPAddress.TryParse(parts[0], out ip))
					return ip;
			}
		}

		if (IPAddress.TryParse(address, out ip))
			return ip;

#if !UNITY_WINRT
		try
		{
			IPAddress[] ips = Dns.GetHostAddresses(address);

			for (int i = 0; i < ips.Length; ++i)
				if (!IPAddress.IsLoopback(ips[i]))
					return ips[i];
		}
 #if UNITY_EDITOR
		catch (System.Exception ex)
		{
			UnityEngine.Debug.LogWarning(ex.Message + " (" + address + ")");
		}
 #else
		catch (System.Exception) {}
 #endif
#endif
		return null;
	}

	/// <summary>
	/// Given the specified address and port, get the end point class.
	/// </summary>

	static public IPEndPoint ResolveEndPoint (string address, int port)
	{
		IPEndPoint ip = ResolveEndPoint(address);
		if (ip != null) ip.Port = port;
		return ip;
	}

	/// <summary>
	/// Given the specified address, get the end point class.
	/// </summary>

	static public IPEndPoint ResolveEndPoint (string address)
	{
		int port = 0;
		string[] split = address.Split(':');

		// Automatically try to parse the port
		if (split.Length == 2)
		{
			address = split[0];
			int.TryParse(split[1], out port);
		}

		IPAddress ad = ResolveAddress(address);
		return (ad != null) ? new IPEndPoint(ad, port) : null;
	}

	/// <summary>
	/// Converts 192.168.1.1 to 192.168.1.
	/// </summary>

	static public string GetSubnet (IPAddress ip)
	{
		if (ip == null) return null;
		string addr = ip.ToString();
		int last = addr.LastIndexOf('.');
		if (last == -1) return null;
		return addr.Substring(0, last);
	}

	/// <summary>
	/// Helper function that returns the response of the specified web request.
	/// </summary>

	static public string GetResponse (WebRequest request)
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
	/// Serialize the IP end point.
	/// </summary>

	static public void Serialize (BinaryWriter writer, IPEndPoint ip)
	{
		byte[] bytes = ip.Address.GetAddressBytes();
		writer.Write((byte)bytes.Length);
		writer.Write(bytes);
		writer.Write((ushort)ip.Port);
	}

	/// <summary>
	/// Deserialize the IP end point.
	/// </summary>

	static public void Serialize (BinaryReader reader, out IPEndPoint ip)
	{
		byte[] bytes = reader.ReadBytes(reader.ReadByte());
		int port = reader.ReadUInt16();
		ip = new IPEndPoint(new IPAddress(bytes), port);
	}

	/// <summary>
	/// Write the channel's data into the specified writer.
	/// </summary>

	/*static public void Serialize (BinaryWriter writer, byte[] data)
	{
		int count = (data != null) ? data.Length : 0;

		if (count < 255)
		{
			writer.Write((byte)count);
		}
		else
		{
			writer.Write((byte)255);
			writer.Write(count);
		}
		if (count > 0) writer.Write(data);
	}

	/// <summary>
	/// Read the channel's data from the specified reader.
	/// </summary>

	static public void Serialize (BinaryReader reader, out byte[] data)
	{
		int count = reader.ReadByte();
		if (count == 255) count = reader.ReadInt32();
		data = (count > 0) ? reader.ReadBytes(count) : null;
	}*/

	/// <summary>
	/// Given the full path, extract the path minus the file's extension.
	/// </summary>

	static public string GetFilePathWithoutExtension (string path)
	{
#if !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		if (!string.IsNullOrEmpty(path))
		{
			try
			{
				return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
			}
			catch (System.Exception) { }
		}
#endif
		return null;
	}

	/// <summary>
	/// Retrieve the list of filenames from the specified directory.
	/// </summary>

	static public string[] GetFiles (string directory, bool inMyDocuments = false)
	{
#if !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		if (!IsAllowedToAccess(directory)) return null;

		try
		{
			if (inMyDocuments) directory = GetDocumentsPath(directory);
			if (!Directory.Exists(directory)) return null;
			return Directory.GetFiles(directory);
		}
		catch (System.Exception) { }
#endif
		return null;
	}

	/// <summary>
	/// Find the specified file in the chosen directory or one of its subfolders.
	/// </summary>

	static public string FindFile (string directory, string fileName)
	{
#if !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		if (!IsAllowedToAccess(directory)) return null;
		string[] files = Directory.GetFiles(directory, fileName);
		return (files.Length == 0) ? null : files[0];
#else
		return null;
#endif
	}

	/// <summary>
	/// Find all files matching the specified pattern, such as "*.txt".
	/// </summary>

	static public string[] FindFiles (string directory, string pattern)
	{
#if !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		if (!IsAllowedToAccess(directory)) return null;
		string[] files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
		return (files.Length == 0) ? null : files;
#else
		return null;
#endif
	}

	/// <summary>
	/// Whether the application should be allowed to access the specified path.
	/// The path must be inside the same folder or in the Documents folder.
	/// </summary>

	static public bool IsAllowedToAccess (string path, bool allowConfigAccess = false)
	{
#if UNITY_EDITOR
		return true;
#elif !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		// Relative paths are not allowed
		if (path.Contains("../") || path.Contains("..\\")) return false;

		// Config folder is restricted by default
		if (!allowConfigAccess && path.IndexOf("ServerConfig", System.StringComparison.CurrentCultureIgnoreCase) != -1)
			return false;

		// Get the full path
		string fullPath = null;

		try
		{
			fullPath = System.IO.Path.GetFullPath(path).Replace("\\", "/");
		}
		catch (System.Exception ex)
		{
			LogError("Path: " + path + "\n" + ex.Message, ex.StackTrace);
			return false;
		}

		// Steam workshop access
		if (fullPath.Contains("/workshop/content/")) return true;

		// Path is inside the current folder
		string current = System.Environment.CurrentDirectory.Replace("\\", "/");
		if (fullPath.Contains(current)) return true;

		// Path is inside My Documents
		string docs = persistentDataPath;

		if (!string.IsNullOrEmpty(applicationDirectory))
		{
			docs = Path.Combine(docs, applicationDirectory);
			docs = docs.Replace("\\", "/");
		}
		if (fullPath.Contains(docs)) return true;

		// No other paths are allowed
		return false;
#else
		return false;
#endif
	}

	/// <summary>
	/// Gets the path to a file in My Documents or OSX equivalent.
	/// </summary>

	static public string GetDocumentsPath (string path = null)
	{
#if !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		try
		{
			if (!string.IsNullOrEmpty(applicationDirectory))
			{
				string docs = Path.Combine(persistentDataPath, applicationDirectory).Replace("\\", "/");
				path = string.IsNullOrEmpty(path) ? docs : Path.Combine(docs, path);
			}

			path = path.Replace("\\", "/");
		}
		catch (System.Exception ex)
		{
 #if UNITY_EDITOR
			UnityEngine.Debug.LogError(ex.Message.Trim() + "\n" + path + "\n" + ex.StackTrace.Replace("\r\n", "\n"));
 #else
			LogError(ex.Message + " (" + path + ")", ex.StackTrace.Replace("\r\n", "\n"), false);
 #endif
			return null;
		}
#endif
		return path;
	}

	/// <summary>
	/// Application directory to use in My Documents. Generally should be the name of your game.
	/// If not set, current directory will be used instead.
	/// </summary>

	static public string applicationDirectory = null;

	/// <summary>
	/// Write the specified file, creating all the subdirectories in the process.
	/// </summary>

	static public bool WriteFile (string path, byte[] data, bool inMyDocuments = false, bool allowConfigAccess = false)
	{
#if !DEMO && !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		if (inMyDocuments) path = GetDocumentsPath(path);

		if (data == null || data.Length == 0)
		{
			DeleteFile(path);
			return true;
		}
		else
		{
			path = path.Replace("\\", "/");

			if (!IsAllowedToAccess(path, allowConfigAccess))
			{
 #if !STANDALONE
				UnityEngine.Debug.LogWarning("Unable to write to " + path);
 #endif
				return false;
			}

			try
			{
				string dir = Path.GetDirectoryName(path);

				if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
					Directory.CreateDirectory(dir);

				if (File.Exists(path))
				{
					FileAttributes att = File.GetAttributes(path);

					if ((att & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
					{
						att = (att & ~FileAttributes.ReadOnly);
						File.SetAttributes(path, att);
					}
				}

				File.WriteAllBytes(path, data);
				if (File.Exists(path)) return true;
 #if !STANDALONE
				UnityEngine.Debug.LogWarning("Unable to write to " + path);
 #endif
			}
 #if STANDALONE
			catch (System.Exception) { }
 #else
			catch (System.Exception ex) { UnityEngine.Debug.LogError(ex.Message); }
 #endif
		}
#endif
		return false;
	}

	/// <summary>
	/// Write the specified file, creating all the subdirectories in the process.
	/// </summary>

	static public bool WriteFile (string path, MemoryStream data, bool inMyDocuments = false, bool allowConfigAccess = false)
	{
#if !DEMO && !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		if (inMyDocuments) path = GetDocumentsPath(path);

		if (!IsAllowedToAccess(path, allowConfigAccess))
		{
#if !STANDALONE
			UnityEngine.Debug.LogWarning("Unable to write to " + path);
#endif
			return false;
		}

		if (data == null || data.Length == 0)
		{
			return DeleteFile(path);
		}
		else
		{
			path = path.Replace("\\", "/");

			try
			{
				string dir = Path.GetDirectoryName(path);

				if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
					Directory.CreateDirectory(dir);

				if (File.Exists(path))
				{
					FileAttributes att = File.GetAttributes(path);

					if ((att & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
					{
						att = (att & ~FileAttributes.ReadOnly);
						File.SetAttributes(path, att);
					}
				}

				FileStream fs = new FileStream(path, FileMode.Create);
				data.Seek(0, SeekOrigin.Begin);
				data.WriteTo(fs);
				fs.Close();
				if (File.Exists(path)) return true;
 #if !STANDALONE
				UnityEngine.Debug.LogWarning("Unable to write to " + path);
 #endif
			}
 #if STANDALONE
			catch (System.Exception) { }
 #else
			catch (System.Exception ex) { UnityEngine.Debug.LogError(ex.Message); }
 #endif
		}
#endif
		return false;
	}

	/// <summary>
	/// Read the following file as text and return the contents.
	/// </summary>

	static public string ReadTextFile (string path)
	{
		byte[] bytes = Tools.ReadFile(path);

		if (bytes != null)
		{
			MemoryStream ms = new MemoryStream(bytes);
			StreamReader sr = new StreamReader(ms);
			string s = sr.ReadToEnd();
			sr.Dispose();
			return s;
		}
		return null;
	}

	/// <summary>
	/// Read the specified file, returning all bytes read.
	/// </summary>

	static public byte[] ReadFile (string path, bool allowConfigAccess = false)
	{
#if !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_WINRT
		path = FindFile(path, allowConfigAccess);

		try
		{
			if (!string.IsNullOrEmpty(path))
				return File.ReadAllBytes(path);
		}
 #if STANDALONE
		catch (System.Exception) { }
 #else
		catch (System.Exception ex) { UnityEngine.Debug.LogError(ex.Message); }
 #endif
#endif
		return null;
	}

	/// <summary>
	/// Delete the specified file, if it exists.
	/// </summary>

	static public bool DeleteFile (string path)
	{
#if !DEMO && !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		try
		{
			path = FindFile(path);

			if (!string.IsNullOrEmpty(path) && File.Exists(path))
			{
				File.Delete(path);
				return true;
			}
		}
		catch (System.Exception) { }
#endif
		return false;
	}

	/// <summary>
	/// Find a directory, given the specified path.
	/// </summary>

	static public string FindDirectory (string path, bool allowConfigAccess = false)
	{
		if (string.IsNullOrEmpty(path)) return null;
#if !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		try
		{
			path = path.Replace("\\", "/");
			if (!path.EndsWith("/")) path += "/";
			if (!IsAllowedToAccess(path, allowConfigAccess)) return null;
			if (Directory.Exists(path)) return path;
			path = GetDocumentsPath(path);
			if (Directory.Exists(path)) return path;
		}
		catch (System.Exception) {}
#endif
		return null;
	}

	/// <summary>
	/// Tries to find the specified file, checking the raw path, My Documents folder, and the application folder.
	/// Returns the path if found, null if not found.
	/// </summary>

	static public string FindFile (string path, bool allowConfigAccess = false)
	{
		if (string.IsNullOrEmpty(path)) return null;
#if !UNITY_WEBPLAYER && !UNITY_FLASH && !UNITY_METRO && !UNITY_WP8 && !UNITY_WP_8_1
		try
		{
			if (!IsAllowedToAccess(path, allowConfigAccess)) return null;
			if (File.Exists(path)) return path.Replace("\\", "/");
			path = GetDocumentsPath(path);
			if (File.Exists(path)) return path;
		}
		catch (System.Exception) { }
#endif
		return null;
	}

	/// <summary>
	/// Convenience function -- prints the specified message, prefixed with a timestamp.
	/// </summary>

	static public void Print (string text, bool appendTime = true)
	{
#if STANDALONE
		if (string.IsNullOrEmpty(text)) System.Console.WriteLine("");
		else if (!appendTime) System.Console.WriteLine(text);
		else System.Console.WriteLine("[" + System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] " + text);
#elif UNITY_EDITOR
		if (!string.IsNullOrEmpty(text)) UnityEngine.Debug.Log(text);
#endif
	}

	/// <summary>
	/// Write a new log entry.
	/// </summary>

	static public void Log (string msg, bool logInFile = true)
	{
#if UNITY_EDITOR
		UnityEngine.Debug.Log(msg);
#else
		msg = "[" + System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] " + msg;
		Tools.Print(msg, false);
 #if STANDALONE
		if (logInFile) LogFile(msg);
 #endif
#endif
	}

	/// <summary>
	/// Log the specified string to the log file.
	/// </summary>

	static public void LogFile (string msg)
	{
		try
		{
			string path = Tools.GetDocumentsPath("Debug/TNetLog.txt");
			string dir = Path.GetDirectoryName(path);
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			StreamWriter sw = new StreamWriter(path, true);
			sw.WriteLine(msg);
			sw.Close();
		}
		catch (System.Exception) { }
	}

	/// <summary>
	/// Log an error message.
	/// </summary>

	static public void LogError (string msg, string stack = null, bool logInFile = true)
	{
		if (msg.Contains("forcibly closed")) return;
#if UNITY_EDITOR
		UnityEngine.Debug.LogError(msg);
#else
		msg = "ERROR: " + msg;
		Tools.Print(msg);

		if (logInFile)
		{
			try
			{
				msg = "[" + System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] " + msg;
				string path = Tools.GetDocumentsPath("Debug/TNetLog.txt");
				string dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
				StreamWriter sw = new StreamWriter(path, true);
				sw.WriteLine(msg);
				if (!string.IsNullOrEmpty(stack)) sw.WriteLine(stack);
				sw.Close();

				path = Tools.GetDocumentsPath("Debug/TNetErrors.txt");
				dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
				sw = new StreamWriter(path, true);
				sw.WriteLine(msg);
				if (!string.IsNullOrEmpty(stack)) sw.WriteLine(stack);
				sw.Close();
			}
			catch (System.Exception) { }
		}
#endif
	}

	/// <summary>
	/// Save this configuration list.
	/// </summary>

	static internal void SaveList (string path, List<string> list)
	{
		if (list.size > 0)
		{
			if (!File.Exists(path) && !string.IsNullOrEmpty(Tools.applicationDirectory))
				path = Tools.GetDocumentsPath(path);

			string dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			StreamWriter sw = new StreamWriter(path, false);
			for (int i = 0; i < list.size; ++i) sw.WriteLine(list[i]);
			sw.Close();
		}
		else Tools.DeleteFile(path);
	}

	/// <summary>
	/// Helper function that loads a list from within specified file.
	/// </summary>

	static internal void LoadList (string path, List<string> list)
	{
		if (path == null) return;

		list.Clear();

		bool exists = false;
		
		if (File.Exists(path))
		{
			exists = true;
		}
		else if (!string.IsNullOrEmpty(Tools.applicationDirectory))
		{
			path = Tools.GetDocumentsPath(path);
			if (File.Exists(path)) exists = true;
		}

		if (exists)
		{
			StreamReader reader = new StreamReader(path);

			while (!reader.EndOfStream)
			{
				string s = reader.ReadLine();
				if (!string.IsNullOrEmpty(s)) list.Add(s);
				else break;
			}
			reader.Close();
		}
	}

	/// <summary>
	/// Format the version number like 1122334 into 2011-22-33.4
	/// </summary>

	static public string FormatVersion (int version)
	{
		string stringVersion = version.ToString();
		int len = stringVersion.Length;

		if (len == 7)
		{
			string text = "20" + stringVersion.Substring(0, 2);
			text += "-" + stringVersion.Substring(2, 2);
			text += "-" + stringVersion.Substring(4, 2);
			text += "." + stringVersion[6];
			return text;
		}
		else if (len == 8)
		{
			string text = stringVersion.Substring(0, 4);
			text += "-" + stringVersion.Substring(4, 2);
			text += "-" + stringVersion.Substring(6, 2);
			return text;
		}
		else if (len == 9)
		{
			string text = stringVersion.Substring(0, 4);
			text += "-" + stringVersion.Substring(4, 2);
			text += "-" + stringVersion.Substring(6, 2);
			text += "." + stringVersion[8];
			return text;
		}
		return version.ToString();
	}

	/// <summary>
	/// Handy function that checks to see if the two float values have actually changed.
	/// </summary>

	static public bool IsNotEqual (float before, float after, float threshold)
	{
		if (before == after) return false;
		if (after == 0f || after == 1f) return true;
		float diff = before - after;
		if (diff < 0f) diff = -diff;
		if (diff > threshold) return true;
		return false;
	}

	/// <summary>
	/// Culture info that forces North American syntax for floating point numbers that using dot as a decimal separator.
	/// </summary>

	static public System.Globalization.CultureInfo englishUSCulture = GetEnUsCulture();

	static System.Globalization.CultureInfo GetEnUsCulture ()
	{
		var thread = Thread.CurrentThread;
		var culture = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
		culture.NumberFormat.CurrencyDecimalSeparator = ".";
		if (thread != null) thread.CurrentCulture = culture;
		return culture;
	}

	/// <summary>
	/// Parse the string as a float using North American syntax for floating point numbers that uses dot as a decimal separator.
	/// </summary>

	static public bool TryParseFloat (string s, out float f)
	{
		return float.TryParse(s, System.Globalization.NumberStyles.Float, Tools.englishUSCulture, out f);
	}
}
}
