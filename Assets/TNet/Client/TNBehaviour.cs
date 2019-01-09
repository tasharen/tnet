//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using System.Reflection;

namespace TNet
{
	/// <summary>
	/// If your MonoBehaviour will need to use a TNObject, deriving from this class will make it easier.
	/// </summary>

	public abstract class TNBehaviour : MonoBehaviour, IStartable
	{
		[System.NonSerialized] TNObject mTNO;
		[System.NonSerialized] public bool ignoreMissingTNO = false;

		public TNObject tno { get { if (mTNO == null) CreateTNObject(); return mTNO; } }

		/// <summary>
		/// Cache the TNObject if it exists.
		/// </summary>

		protected virtual void Awake ()
		{
			mTNO = GetComponentInParent<TNObject>();
			if (mTNO != null) mTNO.rebuildMethodList = true;
			TNUpdater.AddStart(this);
		}

		/// <summary>
		/// Create the TNObject if it hasn't been found already.
		/// </summary>

		public virtual void OnStart () { if (mTNO == null) CreateTNObject(); }

		/// <summary>
		/// Create the TNObject.
		/// </summary>

		protected void CreateTNObject ()
		{
			mTNO = GetComponentInParent<TNObject>();

#if UNITY_EDITOR
			if (!Application.isPlaying) return;
#endif
			if (mTNO != null)
			{
				mTNO.rebuildMethodList = true;
			}
			else if (!ignoreMissingTNO)
			{
				if (!isActiveAndEnabled) return;

				Debug.LogWarning("Your game object is missing a TNObject script needed for network communication.\n" +
					"Simply attach a TNObject script to this game object to fix this problem. If instantiating a prefab, " +
					"attach it to your prefab instead.", this);

				// Add a TNObject manually to make scripts work properly. Doing so won't make network communication
				// work properly however, so beware! Make sure that a TNObject is present on the same object or any
				// parent of an object containing your TNBehaviour-derived scripts.
				mTNO = gameObject.AddComponent<TNObject>();
				mTNO.rebuildMethodList = true;
			}
		}

		/// <summary>
		/// Get the object-specific child data node.
		/// </summary>

		public DataNode Get (string name) { return tno != null ? mTNO.Get(name) : null; }

		/// <summary>
		/// Get the object-specific data.
		/// </summary>

		public T Get<T> (string name) { return tno != null ? mTNO.Get<T>(name) : default(T); }

		/// <summary>
		/// Get the object-specific data.
		/// </summary>

		public T Get<T> (string name, T defVal) { return tno != null ? mTNO.Get<T>(name, defVal) : defVal; }

		/// <summary>
		/// Set the object-specific data.
		/// </summary>

		public void Set (string name, object val) { if (tno != null) mTNO.Set(name, val); }

		/// <summary>
		/// Convenience function to set the data using a single string notation such as "key = value".
		/// </summary>

		public void Set (string text)
		{
			if (!string.IsNullOrEmpty(text))
			{
				var parts = text.Split(new char[] { '=' }, 2);

				if (parts.Length == 2)
				{
					var key = parts[0].Trim();
					var val = parts[1].Trim();
					var node = new DataNode(key, val);
					if (node.ResolveValue()) Set(node.name, node.value);
				}
				else Debug.LogWarning("Invalid syntax [" + text + "]. Expected [key = value].");
			}
		}

		/// <summary>
		/// Destroy this game object.
		/// </summary>

		public virtual void DestroySelf () { if (tno != null) mTNO.DestroySelf(); }

		/// <summary>
		/// Destroy this game object on all connected clients and remove it from the server.
		/// </summary>

		public void DestroySelf (float delay, bool onlyIfOwner = true) { if (tno != null) mTNO.DestroySelf(delay, onlyIfOwner); }

		/// <summary>
		/// Convenience method mirroring TNManager.Instantiate.
		/// Instantiate a new game object in the behaviour's channel on all connected players.
		/// </summary>

		public void Instantiate (int rccID, string path, bool persistent, params object[] objs)
		{
			TNManager.Instantiate(tno.channelID, rccID, null, path, persistent, objs);
		}

		/// <summary>
		/// Convenience method mirroring TNManager.Instantiate.
		/// Instantiate a new game object in the behaviour's channel on all connected players.
		/// </summary>

		public void Instantiate (string funcName, string path, bool persistent, params object[] objs)
		{
			TNManager.Instantiate(tno.channelID, 0, funcName, path, persistent, objs);
		}

		/// <summary>
		/// Immediately remove all saved RFCs. Use this if you want to clear all saved RFCs for any reason.
		/// </summary>

		public void RemoveAllSavedRFCs ()
		{
			var methods = GetType().GetCache().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			for (int b = 0, bmax = methods.Count; b < bmax; ++b)
			{
				var cm = methods.buffer[b];

				if (cm.method.IsDefined(typeof(RFC), true))
				{
					var rfc = (RFC)cm.method.GetCustomAttributes(typeof(RFC), true)[0];

					if (rfc.id > 0)
					{
						tno.RemoveSavedRFC((byte)rfc.id);
					}
					else
					{
						var name = cm.name;
						if (rfc.property != null) name = name + "/" + rfc.GetUniqueID(this);
						tno.RemoveSavedRFC(name);
					}
				}
			}
		}
	}
}
