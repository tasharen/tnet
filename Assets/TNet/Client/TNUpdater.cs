//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2025 Tasharen Entertainment Inc
//-------------------------------------------------

//#define PROFILE_PACKETS
#define THREAD_SAFE_UPDATER

using UnityEngine;
using Generic = System.Collections.Generic;
using IEnumerator = System.Collections.IEnumerator;

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

		struct InvokeEntry
		{
			public float invokeTime;
			public System.Action callback;
		}

		[System.NonSerialized] static TNUpdater mInst;
		[System.NonSerialized] static bool mShuttingDown = false;
		[System.NonSerialized] static Generic.Queue<IStartable> mStartable = new Generic.Queue<IStartable>();
		[System.NonSerialized] static Generic.HashSet<IUpdateable> mUpdateable = new Generic.HashSet<IUpdateable>();
		[System.NonSerialized] static Generic.HashSet<ILateUpdateable> mLateUpdateable = new Generic.HashSet<ILateUpdateable>();
		[System.NonSerialized] static Generic.List<IUpdateable> mRemoveUpdateable = new Generic.List<IUpdateable>();
		[System.NonSerialized] static Generic.List<ILateUpdateable> mRemoveLate = new Generic.List<ILateUpdateable>();
		[System.NonSerialized] static Generic.List<InfrequentEntry> mInfrequent = new Generic.List<InfrequentEntry>();
		[System.NonSerialized] static Generic.List<IInfrequentUpdateable> mRemoveInfrequent = new Generic.List<IInfrequentUpdateable>();
		[System.NonSerialized] static Generic.List<InvokeEntry> mInvoke = new Generic.List<InvokeEntry>();
		[System.NonSerialized] static Generic.List<WorkerThread.EnumFunc> mCoroutines = new Generic.List<WorkerThread.EnumFunc>();
		[System.NonSerialized] static System.Action mOnNextUpdate0;
		[System.NonSerialized] static System.Action mOnNextUpdate1;
		[System.NonSerialized] static bool mOnNextUpdateIndex0 = true;
		[System.NonSerialized] static bool mUpdating = false;
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
					var time = Time.unscaledTime;

					for (int i = 0; i < mInfrequent.Count; ++i)
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
						for (int i = 0; i < mInfrequent.Count; ++i)
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

			if (mInvoke.Count != 0)
			{
				var time = Time.unscaledTime;

				for (int i = 0, imax = mInvoke.Count; i < imax; ++i)
				{
					var inv = mInvoke[i];
					if (inv.invokeTime > time) continue;
					inv.callback();
					mInvoke.RemoveAt(i--);
					--imax;
				}
			}

			if (mCoroutines.Count != 0)
			{
				for (int i = 0; i < mCoroutines.Count; ++i)
				{
					var e = mCoroutines[i]().MoveNext();
					if (!e) mCoroutines.RemoveAt(i--);
				}
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
			mStartable.Enqueue(obj);
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
			mUpdateable.Add(obj);
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
				ent.nextTime = Time.unscaledTime + interval * Random.value;
				ent.interval = interval;
				ent.obj = obj;
				mInfrequent.Add(ent);
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
			mLateUpdateable.Add(obj);
		}

		static public void RemoveUpdate (IUpdateable obj)
		{
			if (mInst)
			{
#if THREAD_SAFE_UPDATER
				lock (mInst)
#endif
				{
					if (mUpdating) mRemoveUpdateable.Add(obj);
					else mUpdateable.Remove(obj);
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
					if (mUpdating) mRemoveLate.Add(obj);
					else mLateUpdateable.Remove(obj);
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
					if (mUpdating)
					{
						mRemoveInfrequent.Add(obj);
					}
					else
					{
						for (int i = 0; i < mInfrequent.Count; ++i)
						{
							if (mInfrequent[i].obj == obj)
							{
								mInfrequent.RemoveAt(i);
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
				if (!Application.isPlaying) { callback(); return; }
				Create();
			}
#if THREAD_SAFE_UPDATER
			lock (mInst)
#endif
			{
				if (mOnNextUpdateIndex0) mOnNextUpdate0 += callback;
				else mOnNextUpdate1 += callback;
			}
		}

		/// <summary>
		/// Remove the specified callback from the one-time execution list.
		/// </summary>

		static public void RemoveOneShot (System.Action callback)
		{
			if (mShuttingDown) return;

#if THREAD_SAFE_UPDATER
			if (mInst != null)
			{
				lock (mInst)
				{
					mOnNextUpdate0 -= callback;
					mOnNextUpdate1 -= callback;
				}
			}
#else
			mOnNextUpdate0 -= callback;
			mOnNextUpdate1 -= callback;
#endif
		}

		/// <summary>
		/// Adds a coroutine that will start to be executed. This works both in Play and Edit modes.
		/// </summary>

		static public void AddCoroutine (WorkerThread.EnumFunc e)
		{
			mCoroutines.Add(e);

			if (mInst == null)
			{
				if (!Application.isPlaying)
				{
#if UNITY_EDITOR
					if (mCoroutines.Count == 1) UnityEditor.EditorApplication.update += EditorUpdate;
#endif
					return;
				}

				Create();
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Edit mode coroutine processing.
		/// </summary>

		static void EditorUpdate ()
		{
			for (int i = 0; i < mCoroutines.Count; ++i)
			{
				var e = mCoroutines[i]().MoveNext();
				if (!e) mCoroutines.RemoveAt(i--);
			}

			if (mCoroutines.Count == 0) UnityEditor.EditorApplication.update -= EditorUpdate;
		}
#endif

		/// <summary>
		/// Invoke the specified callback after a delay.
		/// </summary>

		static public void Invoke (System.Action callback, float delay)
		{
			if (mShuttingDown) return;

			if (mInst == null)
			{
				if (!Application.isPlaying) return;
				Create();
			}

			var inv = new InvokeEntry();
			inv.invokeTime = Time.unscaledTime + delay;
			inv.callback = callback;

#if THREAD_SAFE_UPDATER
			lock (mInst) mInvoke.Add(inv);
#else
			mInvoke.Add(inv);
#endif
		}
	}
}