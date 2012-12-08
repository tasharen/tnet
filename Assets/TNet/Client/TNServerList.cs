//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;
using System.IO;

/// <summary>
/// Example script showing how to use the Broadcast feature to share a list of servers.
/// </summary>

//[RequireComponent(typeof(TNObject))]
//[AddComponentMenu("TNet/Server List")]
//public class TNServerList : MonoBehaviour
//{
//    public class Entry
//    {
//        public string name;
//        public float elapsed = 0f;
//    }

//    static public List<Entry> list = new List<Entry>();

//    /// <summary>
//    /// Port used for broadcasts.
//    /// </summary>

//    public int broadcastPort = 5128;

//    /// <summary>
//    /// Client-triggered broadcast message.
//    /// TODO: Would be easier to use RFCs...
//    /// </summary>

//    void OnBroadcastReceive (string address, BinaryReader reader)
//    {
//    }
//}