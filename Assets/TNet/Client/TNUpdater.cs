//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

#define PROFILE_PACKETS
//#define THREAD_SAFE_UPDATER

using UnityEngine;
using System.Collections.Generic;

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
		[System.NonSerialized] Queue<IStartable> mStartable = new Queue<IStartable>();
		[System.NonSerialized] HashSet<IUpdateable> mUpdateable = new HashSet<IUpdateable>();
		[System.NonSerialized] HashSet<ILateUpdateable> mLateUpdateable = new HashSet<ILateUpdateable>();
		[System.NonSerialized] List<IUpdateable> mRemoveUpdateable = new List<IUpdateable>();
		[System.NonSerialized] List<ILateUpdateable> mRemoveLate = new List<ILateUpdateable>();
		[System.NonSerialized] List<InfrequentEntry> mInfrequent = new List<InfrequentEntry>();
		[System.NonSerialized] List<IInfrequentUpdateable> mRemoveInfrequent = new List<IInfrequentUpdateable>();
		[System.NonSerialized] bool mUpdating = false;
		[System.NonSerialized] static public System.Action onQuit;

		void OnApplicationQuit () { if (onQuit != null) onQuit(); }

		void Update ()
		{
#if THREAD_SAFE_UPDATER
			lock (this)
#endif
			{
				while (mStartable.Count != 0)
				{
					var q = mStartable.Dequeue();
					var obj = q as MonoBehaviour;
					if (obj && obj.enabled) q.OnStart();
				}

				if (mRemoveUpdateable.size != 0)
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

				if (mInfrequent.size != 0)
				{
					mUpdating = true;
					var time = Time.time;

					for (int i = 0; i < mInst.mInfrequent.size; ++i)
					{
						if (mInfrequent.buffer[i].nextTime < time)
						{
							var ent = mInfrequent.buffer[i];
							ent.nextTime = time + ent.interval;
							ent.obj.InfrequentUpdate();
							mInfrequent.buffer[i] = ent;
						}
					}

					mUpdating = false;
				}

				if (mRemoveInfrequent.size != 0)
				{
					foreach (var e in mRemoveInfrequent)
					{
						for (int i = 0; i < mInst.mInfrequent.size; ++i)
						{
							if (mInfrequent.buffer[i].obj == e)
							{
								mInfrequent.RemoveAt(i);
								break;
							}
						}
					}
					mRemoveInfrequent.Clear();
				}
			}
		}

#if UNITY_EDITOR && PROFILE_PACKETS
		static System.Collections.Generic.Dictionary<System.Type, string> mTypeNames = new Dictionary<System.Type, string>();
#endif

		void LateUpdate ()
		{
#if THREAD_SAFE_UPDATER
			lock (this)
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

				if (mRemoveLate.size != 0)
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
			var go = new GameObject();
			go.name = "CustomUpdater";
			DontDestroyOnLoad(go);
			mInst = go.AddComponent<TNUpdater>();
		}

		static public void AddStart (IStartable obj)
		{
			if (mInst == null)
			{
				if (!Application.isPlaying) return;
				Create();
			}

#if THREAD_SAFE_UPDATER
			lock (this)
# endif
			mInst.mStartable.Enqueue(obj);
		}

		static public void AddUpdate (IUpdateable obj)
		{
			if (mInst == null)
			{
				if (WorkerThread.isShuttingDown || !Application.isPlaying) return;
				Create();
			}

#if THREAD_SAFE_UPDATER
			lock (this)
#endif
			mInst.mUpdateable.Add(obj);
		}

		static public void AddInfrequentUpdate (IInfrequentUpdateable obj, float interval)
		{
			if (mInst == null)
			{
				if (WorkerThread.isShuttingDown || !Application.isPlaying) return;
				Create();
			}

#if THREAD_SAFE_UPDATER
			lock (this)
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
			if (mInst == null)
			{
				if (WorkerThread.isShuttingDown || !Application.isPlaying) return;
				Create();
			}

#if THREAD_SAFE_UPDATER
			lock (this)
#endif
			mInst.mLateUpdateable.Add(obj);
		}

		static public void RemoveUpdate (IUpdateable obj)
		{
			if (mInst)
			{
#if THREAD_SAFE_UPDATER
				lock (this)
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
				lock (this)
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
				lock (this)
#endif
				{
					if (mInst.mUpdating)
					{
						mInst.mRemoveInfrequent.Add(obj);
					}
					else
					{
						for (int i = 0; i < mInst.mInfrequent.size; ++i)
						{
							if (mInst.mInfrequent.buffer[i].obj == obj)
							{
								mInst.mInfrequent.RemoveAt(i);
								break;
							}
						}
					}
				}
			}
		}

		//mRemoveInfrequent

		[System.Obsolete("Use RemoveLateUpdate (fixed the typo)")]
		static public void RemoveaLateUpdate (ILateUpdateable obj) { RemoveLateUpdate(obj); }
	}
}