//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace TNet
{
/// <summary>
/// Simple datagram container -- contains a data buffer and the address of where it came from (or where it's going).
/// </summary>

public struct Datagram
{
	public Buffer buffer;
	public IPEndPoint ip;
}
}