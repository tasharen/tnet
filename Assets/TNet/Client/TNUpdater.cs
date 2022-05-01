//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2020 Tasharen Entertainment Inc
//-------------------------------------------------

//#define PROFILE_PACKETS
#define THREAD_SAFE_UPDATER

using UnityEngine;
using Generic = System.Collections.Generic;

namespace TNet
{
	public interface IStartable { void OnStart (); }
	public interface IUpdateable { void OnUpdate (); }
	public interface ILateUpdateable { void OnLateUpdate (); }
	public interface IInfrequentUpdateable { void InfrequentUpdate (); }

	/// <summary>
	/// Unity seems to have a horrible bug: if a Start() function is used, disabling the component takes an absurd amount of time.
	/// This class makes it possible to bypass this issue by adding support for a remotely executed Start() function.
	/// Just to make it more useful it also adds support for Update and LateUpdate functions, since reducing the number of those
	/// is a good way to improve application performance. Simply add your scripts in OnEnable and remove in OnDisable.
	/// </summary>

	public class TNUpdater : MonoBehaviour
	{
		struct InfrequentEntry
		{
			public float nextTime;
			public float interval;
			public IInfrequentUpdateable obj;
		}

		[System.NonSerialized] static TNUpdater mInst;
		[System.NonSerialized] static bool mShuttingDown = false;
		[System.NonSerialized] Generic.Queue<IStartable> mStartable = new Generic.Queue<IStartable>();
		[System.NonSerialized] Generic.HashSet<IUpdateable> mUpdateable = new Generic.HashSet<IUpdateable>();
		[System.NonSerialized] Generic.HashSet<ILateUpdateable> mLateUpdateable = new Generic.HashSet<ILateUpdateable>();
		[System.NonSerialized] Generic.List<IUpdateable> mRemoveUpdateable = new Generic.List<IUpdateable>();
		[System.NonSerialized] Generic.List<ILateUpdateable> mRemoveLate = new Generic.List<ILateUpdateable>();
		[System.NonSerialized] Generic.List<InfrequentEntry> mInfrequent = new Generic.List<InfrequentEntry>();
		[System.NonSerialized] Generic.List<IInfrequentUpdateable> mRemoveInfrequent = new Generic.List<IInfrequentUpdateable>();
		[System.NonSerialized] System.Action mOnNextUpdate0;
		[System.NonSerialized] System.Action mOnNextUpdate1;
		[System.NonSerialized] bool mOnNextUpdateIndex0 = true;
		[System.NonSerialized] bool mUpdating = false;
		[System.NonSerialized] static public System.Action onQuit;

		void OnDestroy ()
		{
			mShuttingDown = true;

			if (mInst != null)
			{
#if THREAD_SAFE_UPDATER
				lock (mInst)
#endif
				{
					if (onQuit != null)
					{
						onQuit();
						onQuit = null;
					}
				}
			}
		}

		void Update ()
		{
#if THREAD_SAFE_UPDATER
			lock (mInst)
#endif
			{
				while (mStartable.Count != 0)
				{
					var q = mStartable.Dequeue();
					var obj = q as MonoBehaviour;
					if (obj && obj.enabled) q.OnStart();
				}

				if (mRemoveUpdateable.Count != 0)
				{
					foreach (var e in mRemoveUpdateable) mUpdateable.Remove(e);
					mRemoveUpdateable.Clear();
				}

				if (mUpdateable.Count != 0)
				{
					mUpdating = true;
					foreach (var inst in mUpdateable) inst.OnUpdate();
					mUpdating = false;
				}

				if (mInfrequent.Count != 0)
				{
					mUpdating = true;
					var time = Time.time;

					for (int i = 0; i < mInst.mInfrequent.Count; ++i)
					{
						if (mInfrequent[i].nextTime < time)
						{
							var ent = mInfrequent[i];
							ent.nextTime = time + ent.interval;
							ent.obj.InfrequentUpdate();
							mInfrequent[i] = ent;
						}
					}

					mUpdating = false;
				}

				if (mRemoveInfrequent.Count != 0)
				{
					foreach (var e in mRemoveInfrequent)
					{
						for (int i = 0; i < mInst.mInfrequent.Count; ++i)
						{
							if (mInfrequent[i].obj == e)
							{
								mInfrequent.RemoveAt(i);
								break;
							}
						}
					}
					mRemoveInfrequent.Clear();
				}

				if (mOnNextUpdateIndex0)
				{
					if (mOnNextUpdate1 != null)
					{
						mOnNextUpdate1();
						mOnNextUpdate1 = null;
					}
				}
				else if (mOnNextUpdate0 != null)
				{
					mOnNextUpdate0();
					mOnNextUpdate0 = null;
				}

				mOnNextUpdateIndex0 = !mOnNextUpdateIndex0;
			}
		}

#if UNITY_EDITOR && PROFILE_PACKETS
		static Generic.Dictionary<System.Type, string> mTypeNames = new Generic.Dictionary<System.Type, string>();
#endif

		void LateUpdate ()
		{
#if THREAD_SAFE_UPDATER
			lock (mInst)
#endif
			{
				while (mStartable.Count != 0)
				{
					var q = mStartable.Dequeue();
					var obj = q as MonoBehaviour;

					if (obj && obj.enabled && obj.gameObject.activeInHierarchy)
					{
#if UNITY_EDITOR && PROFILE_PACKETS
						var type = obj.GetType();

						string packetName;

						if (!mTypeNames.TryGetValue(type, out packetName))
						{
							packetName = type.ToString() + ".OnStart()";
							mTypeNames.Add(type, packetName);
						}

						UnityEngine.Profiling.Profiler.BeginSample(packetName);
						q.OnStart();
						UnityEngine.Profiling.Profiler.EndSample();
#else
						q.OnStart();
#endif
					}
				}

				if (mRemoveLate.Count != 0)
				{
					foreach (var e in mRemoveLate) mLateUpdateable.Remove(e);
					mRemoveLate.Clear();
				}

				if (mLateUpdateable.Count != 0)
				{
					mUpdating = true;
					foreach (var inst in mLateUpdateable) inst.OnLateUpdate();
					mUpdating = false;
				}
			}
		}

		static void Create ()
		{
			if (mShuttingDown) return;
			var go = new GameObject();
			go.name = "TNUpdater";
			DontDestroyOnLoad(go);
			mInst = go.AddComponent<TNUpdater>();
		}

		static public void AddStart (IStartable obj)
		{
			if (mShuttingDown) return;

			if (mInst == null)
			{
				if (!Application.isPlaying) return;
				Create();
			}

#if THREAD_SAFE_UPDATER
			lock (mInst)
#endif
			mInst.mStartable.Enqueue(obj);
		}

		static public void AddUpdate (IUpdateable obj)
		{
			if (mShuttingDown) return;

			if (mInst == null)
			{
				if (!Application.isPlaying) return;
				Create();
			}

#if THREAD_SAFE_UPDATER
			lock (mInst)
#endif
			mInst.mUpdateable.Add(obj);
		}

		static public void AddInfrequentUpdate (IInfrequentUpdateable obj, float interval)
		{
			if (mShuttingDown) return;

			if (mInst == null)
			{
				if (!Application.isPlaying) return;
				Create();
			}

#if THREAD_SAFE_UPDATER
			lock (mInst)
#endif
			{
				var ent = new InfrequentEntry();
				ent.nextTime = Time.time + interval * Random.value;
				ent.interval = interval;
				ent.obj = obj;
				mInst.mInfrequent.Add(ent);
			}
		}

		static public void AddLateUpdate (ILateUpdateable obj)
		{
			if (mShuttingDown) return;

			if (mInst == null)
			{
				if (!Application.isPlaying) return;
				Create();
			}

#if THREAD_SAFE_UPDATER
			lock (mInst)
#endif
			mInst.mLateUpdateable.Add(obj);
		}

		static public void RemoveUpdate (IUpdateable obj)
		{
			if (mInst)
			{
#if THREAD_SAFE_UPDATER
				lock (mInst)
#endif
				{
					if (mInst.mUpdating) mInst.mRemoveUpdateable.Add(obj);
					else mInst.mUpdateable.Remove(obj);
				}
			}
		}

		static public void RemoveLateUpdate (ILateUpdateable obj)
		{
			if (mInst)
			{
#if THREAD_SAFE_UPDATER
				lock (mInst)
#endif
				{
					if (mInst.mUpdating) mInst.mRemoveLate.Add(obj);
					else mInst.mLateUpdateable.Remove(obj);
				}
			}
		}

		static public void RemoveInfrequentUpdate (IInfrequentUpdateable obj)
		{
			if (mInst)
			{
#if THREAD_SAFE_UPDATER
				lock (mInst)
#endif
				{
					if (mInst.mUpdating)
					{
						mInst.mRemoveInfrequent.Add(obj);
					}
					else
					{
						for (int i = 0; i < mInst.mInfrequent.Count; ++i)
						{
							if (mInst.mInfrequent[i].obj == obj)
							{
								mInst.mInfrequent.RemoveAt(i);
								break;
							}
						}
					}
				}
			}
		}

		[System.Obsolete("Use RemoveLateUpdate (fixed the typo)")]
		static public void RemoveaLateUpdate (ILateUpdateable obj) { RemoveLateUpdate(obj); }

		/// <summary>
		/// Add a callback to be executed on the next update, and only once.
		/// </summary>

		static public void AddOneShot (System.Action callback)
		{
			if (mShuttingDown) return;

			if (mInst == null)
			{
				if (!Application.isPlaying) return;
				Create();
			}
#if THREAD_SAFE_UPDATER
			lock (mInst)
#endif
			{
				if (mInst.mOnNextUpdateIndex0) mInst.mOnNextUpdate0 += callback;
				else mInst.mOnNextUpdate1 += callback;
			}
		}

		/// <summary>
		/// Remove the specified callback from the one-time execution list.
		/// </summary>

		static public void RemoveOneShot (System.Action callback)
		{
			if (mShuttingDown) return;

			if (mInst != null)
			{
#if THREAD_SAFE_UPDATER
				lock (mInst)
#endif
				{
					mInst.mOnNextUpdate0 -= callback;
					mInst.mOnNextUpdate1 -= callback;
				}
			}
		}
	}
}