//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using System.Threading;
using System.Collections.Generic;

namespace TNet
{
/// <summary>
/// Worker thread is a convenience class that can execute specified code on a separate thread.
/// The worker thread class takes care of creating multiple threads for concurrent code execution.
/// </summary>

public class WorkerThread : MonoBehaviour
{
	// Recommended setting is 2 threads per CPU, but I am setting it to 1 by default since I don't want to assume 100% CPU usage
	const int threadsPerCore = 1;

	static WorkerThread mInstance = null;

	// Callback function prototype -- should return whether to keep it in the thread pool
	public delegate bool BoolFunc ();
	public delegate void VoidFunc ();

	// Actual worker thread
	Thread[] mThreads = null;
	int[] mLoad = null;

	class Entry
	{
		public VoidFunc main;
		public BoolFunc conditional;
		public VoidFunc finished;
	}

	// List of callbacks executed in order by the worker thread
	Queue<Entry> mNew = new Queue<Entry>();
	Queue<Entry> mFinished = new Queue<Entry>();
	List<Entry> mUnused = new List<Entry>();

	/// <summary>
	/// Create the worker thread.
	/// </summary>

	void OnEnable ()
	{
		if (mInstance == null)
		{
			mInstance = this;
			StartThreads();
		}
		else Destroy(this);
	}

	/// <summary>
	/// Release the mutex and destroy the worker thread.
	/// </summary>

	void OnDisable ()
	{
		if (mInstance == this)
		{
			StopThreads();
			mInstance = null;
		}
	}

	/// <summary>
	/// Start worker threads.
	/// </summary>

	void StartThreads ()
	{
		if (mThreads != null) return;

		int maxThreads = System.Environment.ProcessorCount * threadsPerCore;
		if (maxThreads < 1) maxThreads = 1;
		if (maxThreads > 32) maxThreads = 32;

		mThreads = new Thread[maxThreads];
		mLoad = new int[maxThreads];

		// Create the threads
		for (int i = 0; i < maxThreads; ++i)
		{
			int threadID = i;

			mThreads[threadID] = new Thread(delegate()
			{
				List<Entry> active = new List<Entry>();

				for (; ; )
				{
					// Check without locking first as it's faster
					if (mNew.Count > 0)
					{
						bool grab = true;

						// If this thread is not idling, check to see if others are
						if (active.size != 0)
						{
							for (int b = 0; b < maxThreads; ++b)
							{
								if (b != threadID && mLoad[b] == 0)
								{
									grab = false;
									break;
								}
							}
						}

						// No threads are idling -- grab the first queued item
						if (grab)
						{
							lock (mNew)
							{
								if (mNew.Count > 0)
								{
									active.Add(mNew.Dequeue());
									mLoad[threadID] = active.size;
								}
							}
						}
					}

					// If we are working on something, run another update
					if (active.Count > 0)
					{
						for (int b = active.size; b > 0; )
						{
							var ent = active[--b];

							try
							{
								if (ent.main != null)
								{
									ent.main();
									active.RemoveAt(b);
									if (ent.finished != null) lock (mFinished) mFinished.Enqueue(ent);
									else lock (mUnused) mUnused.Add(ent);
									mLoad[threadID] = active.size;
								}
								else if (!ent.conditional())
								{
									active.RemoveAt(b);
									if (ent.finished != null) lock (mFinished) mFinished.Enqueue(ent);
									else lock (mUnused) mUnused.Add(ent);
									mLoad[threadID] = active.size;
								}
							}
							catch (System.Exception ex)
							{
								Debug.LogError(ex.Message + "\n" + ex.StackTrace);
								active.RemoveAt(b);
							}
						}
					}
					
					// Sleep for a short amount
					try { Thread.Sleep(1); }
					catch (System.Threading.ThreadInterruptedException) { return; }
				}
			});
		}
		
		// Now that all threads have been created, start them all at once
		for (int i = 0; i < maxThreads; ++i) mThreads[i].Start();
	}

	/// <summary>
	/// Stop all active threads.
	/// </summary>

	void StopThreads ()
	{
		if (mThreads != null)
		{
			for (int i = 0; i < mThreads.Length; ++i)
			{
				Thread thread = mThreads[i];

				if (thread != null)
				{
					thread.Interrupt();
					thread.Join();
				}
			}
			mThreads = null;
		}
	}

	/// <summary>
	/// Abort the worker thread on application quit.
	/// </summary>

	void OnApplicationQuit ()
	{
		StopThreads();
		mNew.Clear();
	}

	/// <summary>
	/// Call finished delegates on the main thread.
	/// </summary>

	void Update ()
	{
		if (mFinished.Count > 0)
		{
			lock (mFinished)
			{
				while (mFinished.Count > 0)
				{
					var ent = mFinished.Dequeue();
					if (ent.finished != null) ent.finished();
					ent.main = null;
					ent.finished = null;
					ent.conditional = null;
					lock (mUnused) mUnused.Add(ent);
				}
			}
		}
	}

	/// <summary>
	/// Add a new callback function to the worker thread.
	/// </summary>

	static public void Create (VoidFunc main, VoidFunc finished = null)
	{
		if (mInstance == null)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				main();
				if (finished != null) finished();
				return;
			}
#endif
			GameObject go = new GameObject("Worker Thread");
			mInstance = go.AddComponent<WorkerThread>();
		}

		Entry ent;

		if (mInstance.mUnused.size != 0)
		{
			lock (mInstance.mUnused) { ent = (mInstance.mUnused.size != 0) ? mInstance.mUnused.Pop() : new Entry(); }
		}
		else ent = new Entry();

		ent.main = main;
		ent.finished = finished;
		lock (mInstance.mNew) mInstance.mNew.Enqueue(ent);
	}

	/// <summary>
	/// Add a new callback function to the worker thread.
	/// The 'main' delegate will run on a secondary thread, while the 'finished' delegate will run in Update().
	/// </summary>

	static public void CreateConditional (BoolFunc main, VoidFunc finished = null)
	{
		if (mInstance == null)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				if (main() && finished != null) finished();
				return;
			}
#endif
			GameObject go = new GameObject("Worker Thread");
			mInstance = go.AddComponent<WorkerThread>();
		}

		Entry ent;

		if (mInstance.mUnused.Count != 0)
		{
			lock (mInstance.mUnused) { ent = (mInstance.mUnused.size != 0) ? mInstance.mUnused.Pop() : new Entry(); }
		}
		else ent = new Entry();

		ent.conditional = main;
		ent.finished = finished;
		lock (mInstance.mNew) mInstance.mNew.Enqueue(ent);
	}
}
}
