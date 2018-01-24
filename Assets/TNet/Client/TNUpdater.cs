//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

#define PROFILE_PACKETS

using UnityEngine;
using System.Collections.Generic;

namespace TNet
{
	public interface IStartable { void OnStart (); }
	public interface IUpdateable { void OnUpdate (); }
	public interface ILateUpdateable { void OnLateUpdate (); }

	/// <summary>
	/// Unity seems to have a horrible bug: if a Start() function is used, disabling the component takes an absurd amount of time.
	/// This class makes it possible to bypass this issue by adding support for a remotely executed Start() function.
	/// Just to make it more useful it also adds support for Update and LateUpdate functions, since reducing the number of those
	/// is a good way to improve application performance. Simply add your scripts in OnEnable and remove in OnDisable.
	/// </summary>

	public class TNUpdater : MonoBehaviour
	{
		static TNUpdater mInst;

		Queue<IStartable> mStartable = new Queue<IStartable>();
		HashSet<IUpdateable> mUpdateable = new HashSet<IUpdateable>();
		HashSet<ILateUpdateable> mLateUpdateable = new HashSet<ILateUpdateable>();

		void Update ()
		{
			while (mStartable.Count != 0)
			{
				var q = mStartable.Dequeue();
				var obj = q as MonoBehaviour;
				if (obj && obj.enabled) q.OnStart();
			}

			if (mUpdateable.Count != 0) foreach (var inst in mUpdateable) inst.OnUpdate();
		}

#if UNITY_EDITOR && PROFILE_PACKETS
		static System.Collections.Generic.Dictionary<System.Type, string> mTypeNames = new Dictionary<System.Type, string>();
#endif

		void LateUpdate ()
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

			if (mLateUpdateable.Count != 0) foreach (var inst in mLateUpdateable) inst.OnLateUpdate();
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
			mInst.mStartable.Enqueue(obj);
		}

		static public void AddUpdate (IUpdateable obj)
		{
			if (mInst == null)
			{
				if (!Application.isPlaying) return;
				Create();
			}
			mInst.mUpdateable.Add(obj);
		}

		static public void AddLateUpdate (ILateUpdateable obj)
		{
			if (mInst == null)
			{
				if (!Application.isPlaying) return;
				Create();
			}
			mInst.mLateUpdateable.Add(obj);
		}

		static public void RemoveUpdate (IUpdateable obj) { if (mInst) mInst.mUpdateable.Remove(obj); }
		static public void RemoveaLateUpdate (ILateUpdateable obj) { if (mInst) mInst.mLateUpdateable.Remove(obj); }
	}
}