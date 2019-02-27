//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

// Use this for debugging purposes
//#define SINGLE_THREADED
//#define DEBUG_THREAD_TIMING

using UnityEngine;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;

namespace TNet
{
	/// <summary>
	/// Worker thread is a convenience class that can execute specified code on a separate thread.
	/// The worker thread class takes care of creating multiple threads for concurrent code execution.
	/// </summary>

	public class WorkerThread : MonoBehaviour
	{
		/// <summary>
		/// Current thread's ID. Can be called from anywhere to determine if executing on the main thread (threadID == 0) or a worker thread (threadID > 0).
		/// Don't try to edit this value.
		/// </summary>

		[System.ThreadStatic]
		static public int threadID = 0;

		// Recommended setting is 2 threads per CPU, but I am setting it to 1 by default since I don't want to assume 100% CPU usage
		const int threadsPerCore = 1;

		// How much time the worker thread's update function is allowed to take per frame.
		// Note that this value simply means that if it's exceeded, no more functions will be executed on this frame, not that it will pause mid-execution.
		static public double maxMillisecondsPerFrame = 4d;
		static WorkerThread mInstance = null;

		/// <summary>
		/// Set to 'true' if the application is shutting down.
		/// </summary>

		[System.NonSerialized]
		static public bool isShuttingDown = false;

		public delegate bool BoolFunc ();
		public delegate void VoidFunc ();
		public delegate System.Collections.IEnumerator EnumFunc ();

		// Actual worker thread
		Thread[] mThreads = null;
		int[] mLoad = null;

		struct Entry
		{
			public VoidFunc main;
			public VoidFunc finished;
			public BoolFunc mainBool;       // Return 'true' when done, 'false' to execute again next update
			public BoolFunc finishedBool;   // Return 'true' when done, 'false' to execute again next update
			public EnumFunc finishedEnum;
			public double threadTime;		// Time spent in the worker thread (in milliseconds)
			public double mainTime;         // Time spent in the main thread (in milliseconds)
#if DEBUG_THREAD_TIMING
			public string trace;
#endif
			public System.Collections.IEnumerator en;
		}

		// List of callbacks executed in order by the worker thread
		Queue<Entry> mPriority = new Queue<Entry>();
		Queue<Entry> mRegular = new Queue<Entry>();
		Queue<Entry> mFinished = new Queue<Entry>();
		static Stopwatch mStopwatch = new Stopwatch();

		/// <summary>
		/// Count how many callbacks are still remaining in the worker thread's queues.
		/// </summary>

		static public int remainingCallbackCount
		{
			get
			{
				if (mInstance == null) return 0;

				var count = 0;
				lock (mInstance.mPriority) { lock (mInstance.mRegular) { lock (mInstance.mFinished) { count = mInstance.mPriority.Count + mInstance.mRegular.Count + mInstance.mFinished.Count; } } }

				if (mInstance.mLoad != null)
				{
					for (int i = 0, imax = mInstance.mLoad.Length; i < imax; ++i)
						count += mInstance.mLoad[i];
				}
				return count;
			}
		}

		/// <summary>
		/// Immediately abort all active threads.
		/// </summary>

		static public void Abort ()
		{
			if (mInstance != null)
			{
				mInstance.StopThreads();
				mInstance.mRegular.Clear();
				mInstance.mPriority.Clear();
				mInstance.mFinished.Clear();
				mInstance.StartThreads();
			}
		}

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

			isShuttingDown = false;

			int maxThreads = System.Environment.ProcessorCount * threadsPerCore;
			if (maxThreads < 1) maxThreads = 1;
			if (maxThreads > 32) maxThreads = 32;

			mThreads = new Thread[maxThreads];
			mLoad = new int[maxThreads];

			// Create the threads
			for (int i = 0; i < maxThreads; ++i)
			{
				int threadID = i;

				mThreads[threadID] = Tools.CreateThread(delegate ()
				{
					var active = new List<Entry>();
					var sw = new Stopwatch();
					WorkerThread.threadID = threadID + 1;

					for (;;)
					{
						bool handled = false;

						// Check without locking first as it's faster
						if (mPriority.Count > 0)
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
								lock (mPriority)
								{
									if (mPriority.Count > 0)
									{
										handled = true;
										active.Add(mPriority.Dequeue());
										mLoad[threadID] = active.size;
									}
								}
							}
						}

						if (!handled && mRegular.Count > 0)
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
								lock (mRegular)
								{
									if (mRegular.Count > 0)
									{
										active.Add(mRegular.Dequeue());
										mLoad[threadID] = active.size;
									}
								}
							}
						}

						// If we are working on something, run another update
						if (active.size > 0)
						{
							for (int b = active.size; b > 0;)
							{
								var ent = active.buffer[--b];
#if DEBUG_THREAD_TIMING
								FastLog.Log("Worker Thread\n   " + ent.trace + "\n");
#endif
								sw.Reset();
								sw.Start();

								try
								{
									if (ent.main != null)
									{
										ent.main();
										ent.threadTime += sw.GetElapsedMilliseconds();

										active.RemoveAt(b);
										if (ent.finished != null || ent.finishedBool != null || ent.finishedEnum != null) lock (mFinished) mFinished.Enqueue(ent);
										mLoad[threadID] = active.size;
									}
									else if (ent.mainBool != null)
									{
										var result = ent.mainBool();
										ent.threadTime += sw.GetElapsedMilliseconds();

										if (result)
										{
											active.RemoveAt(b);
											if (ent.finished != null || ent.finishedBool != null || ent.finishedEnum != null) lock (mFinished) mFinished.Enqueue(ent);
											mLoad[threadID] = active.size;
										}
										else active.buffer[b] = ent;
									}
								}
								catch (System.Exception ex)
								{
									UnityEngine.Debug.LogError(ex.Message + "\n" + ex.StackTrace);
									active.RemoveAt(b);
								}
							}
						}

						// Sleep for a short amount
						try { Thread.Sleep(1); }
						catch (ThreadInterruptedException) { return; }
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
			isShuttingDown = true;

			if (mThreads != null)
			{
				for (int i = 0; i < mThreads.Length; ++i)
				{
					var thread = mThreads[i];

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
			isShuttingDown = true;

			if (mThreads != null)
			{
				for (int i = 0; i < mThreads.Length; ++i)
				{
					var thread = mThreads[i];
					if (thread != null) thread.Interrupt();
				}
				mThreads = null;
			}

			mRegular.Clear();
		}

		List<Entry> mTemp = new List<Entry>();

		/// <summary>
		/// Number of elapsed milliseconds since the function started its current execution iteration.
		/// Only valid inside the OnFinished stage functions.
		/// </summary>

		static public double currentExecutionTime { get { return mStopwatch.GetElapsedMilliseconds() - mLoopStart; } }

		/// <summary>
		/// Total execution time for the current callback, including secondary thread execution times. Execution of multi-stage callbacks are cumulative.
		/// Only valid inside the OnFinished stage functions.
		/// </summary>

		static public double totalExecutionTime { get { return mExecStart + currentExecutionTime; } }

		/// <summary>
		/// Check from inside your multi-stage completion functions to check whether the main frame's max allowed time has been exceeded.
		/// Only valid inside the OnFinished stage functions.
		/// </summary>

		static public bool frameTimeExceeded { get { return currentExecutionTime > maxMillisecondsPerFrame; } }

		[System.Obsolete("Use 'frameTimeExceeded instead'")]
		static public bool mainFrameTimeExceeded { get { return frameTimeExceeded; } }

		static double mLoopStart = 0d, mExecStart = 0d;

		/// <summary>
		/// Call finished delegates on the main thread.
		/// </summary>

		void Update ()
		{
			if (mFinished.Count > 0)
			{
				mStopwatch.Reset();
				mStopwatch.Start();

				Entry ent;

				while (mFinished.Count > 0)
				{
					lock (mFinished) ent = mFinished.Dequeue();

					mLoopStart = mStopwatch.GetElapsedMilliseconds();
					mExecStart = ent.mainTime;

					if (ent.finished != null)
					{
						ent.finished();
						ent.mainTime += mStopwatch.GetElapsedMilliseconds() - mLoopStart;
					}
					else if (ent.finishedEnum != null)
					{
						if (ent.en == null) ent.en = ent.finishedEnum();

						var keepGoing = false;

						while (ent.en.MoveNext())
						{
							if (mStopwatch.GetElapsedMilliseconds() > maxMillisecondsPerFrame)
							{
								keepGoing = true;
								break;
							}
						}

						ent.mainTime += mStopwatch.GetElapsedMilliseconds() - mLoopStart;

						if (keepGoing)
						{
							mTemp.Add(ent);
							continue;
						}
					}
					else if (ent.finishedBool != null)
					{
						var result = ent.finishedBool();
						var elapsed = mStopwatch.GetElapsedMilliseconds();
						ent.mainTime += elapsed - mLoopStart;

						if (!result)
						{
							mTemp.Add(ent);
							if (elapsed > maxMillisecondsPerFrame) break;
							continue;
						}
					}
#if DEBUG_THREAD_TIMING
					FastLog.Log("Time: " + ent.threadTime.ToString("N3") + " ms (thread) + " + ent.mainTime.ToString("N3") + " ms (main)\n   " + ent.trace + "\n");
#endif
					if (mStopwatch.GetElapsedMilliseconds() > maxMillisecondsPerFrame) break;
				}
			}

			// Re-queue the conditionals
			if (mTemp.size > 0)
			{
				lock (mFinished)
				{
					for (int i = 0; i < mTemp.size; ++i) mFinished.Enqueue(mTemp.buffer[i]);
					mTemp.Clear();
				}
			}
		}

		/// <summary>
		/// Add a new callback function to the worker thread.
		/// </summary>

		static public void Create (VoidFunc main, VoidFunc finished = null, bool highPriority = false)
		{
#if SINGLE_THREADED
			if (main != null) main();
			if (finished != null) finished();
#else
			if (mInstance == null)
			{
#if UNITY_EDITOR
				if (!Application.isPlaying)
				{
					if (main != null) main();
					if (finished != null) finished();
					return;
				}
#endif
				var go = new GameObject("Worker Thread");
				mInstance = go.AddComponent<WorkerThread>();
			}

			var ent = new Entry();
			ent.main = main;
			ent.finished = finished;
#if DEBUG_THREAD_TIMING
			ent.trace = Tools.stackTrace2;
#endif
			if (main != null)
			{
				if (highPriority) lock (mInstance.mPriority) mInstance.mPriority.Enqueue(ent);
				else lock (mInstance.mRegular) mInstance.mRegular.Enqueue(ent);
			}
			else lock (mInstance.mFinished) mInstance.mFinished.Enqueue(ent);
#endif
		}

		/// <summary>
		/// Add a new callback function to the worker thread.
		/// </summary>

		static public void Create (VoidFunc main, EnumFunc finished, bool highPriority = false)
		{
#if SINGLE_THREADED
			if (main != null) main();
			while (finished().MoveNext()) {}
#else
			if (mInstance == null)
			{
#if UNITY_EDITOR
				if (!Application.isPlaying)
				{
					if (main != null) main();
					if (finished != null) finished();
					return;
				}
#endif
				var go = new GameObject("Worker Thread");
				mInstance = go.AddComponent<WorkerThread>();
			}

			var ent = new Entry();
			ent.main = main;
			ent.finishedEnum = finished;
#if DEBUG_THREAD_TIMING
			ent.trace = Tools.stackTrace2;
#endif
			if (main != null)
			{
				if (highPriority) lock (mInstance.mPriority) mInstance.mPriority.Enqueue(ent);
				else lock (mInstance.mRegular) mInstance.mRegular.Enqueue(ent);
			}
			else lock (mInstance.mFinished) mInstance.mFinished.Enqueue(ent);
#endif
		}

		/// <summary>
		/// Add a new callback function to the worker thread.
		/// Return 'false' if you want the same delegate to execute again in the next Update(), or 'true' if you're done.
		/// </summary>

		static public void CreateMultiStageCompletion (VoidFunc main, BoolFunc finished = null, bool highPriority = false)
		{
#if SINGLE_THREADED
			if (main != null) main();
			if (finished != null) while (!finished()) { };
#else
			if (mInstance == null)
			{
#if UNITY_EDITOR
				if (!Application.isPlaying)
				{
					if (main != null) main();
					if (finished != null) while (!finished()) { };
					return;
				}
#endif
				GameObject go = new GameObject("Worker Thread");
				mInstance = go.AddComponent<WorkerThread>();
			}

			var ent = new Entry();
			ent.main = main;
			ent.finishedBool = finished;
#if DEBUG_THREAD_TIMING
			ent.trace = Tools.stackTrace2;
#endif
			if (main != null)
			{
				if (highPriority) lock (mInstance.mPriority) mInstance.mPriority.Enqueue(ent);
				else lock (mInstance.mRegular) mInstance.mRegular.Enqueue(ent);
			}
			else lock (mInstance.mFinished) mInstance.mFinished.Enqueue(ent);
#endif
		}

		/// <summary>
		/// Add a new callback function to the worker thread.
		/// The 'main' delegate will run on a secondary thread, while the 'finished' delegate will run in Update().
		/// Return 'false' if you want the same delegate to execute again next time, or 'true' if you're done.
		/// </summary>

		static public void CreateMultiStageExecution (BoolFunc main, VoidFunc finished = null, bool highPriority = false)
		{
#if SINGLE_THREADED
			if (main != null) { while (!main()) { } }
			if (finished != null) finished();
#else
			if (mInstance == null)
			{
#if UNITY_EDITOR
				if (!Application.isPlaying)
				{
					if (main != null) { while (!main()) { } }
					if (finished != null) finished();
					return;
				}
#endif
				GameObject go = new GameObject("Worker Thread");
				mInstance = go.AddComponent<WorkerThread>();
			}

			var ent = new Entry();
			ent.mainBool = main;
			ent.finished = finished;
#if DEBUG_THREAD_TIMING
			ent.trace = Tools.stackTrace2;
#endif
			if (highPriority) lock (mInstance.mPriority) mInstance.mPriority.Enqueue(ent);
			else lock (mInstance.mRegular) mInstance.mRegular.Enqueue(ent);
#endif
		}

		/// <summary>
		/// Add a new callback function to the worker thread.
		/// The 'main' delegate will run on a secondary thread, while the 'finished' delegate will run in Update().
		/// Return 'false' if you want the same delegates to execute again next time, or 'true' if you're done.
		/// </summary>

		static public void CreateMultiStage (BoolFunc main, BoolFunc finished = null, bool highPriority = false)
		{
#if SINGLE_THREADED
			if (main != null) { while (!main()) { } }
			if (finished != null) { while (!finished()) { } }
#else
			if (mInstance == null)
			{
#if UNITY_EDITOR
				if (!Application.isPlaying)
				{
					if (main != null) { while (!main()) { } }
					if (finished != null) { while (!finished()) { } }
					return;
				}
#endif
				GameObject go = new GameObject("Worker Thread");
				mInstance = go.AddComponent<WorkerThread>();
			}

			var ent = new Entry();
			ent.mainBool = main;
			ent.finishedBool = finished;
#if DEBUG_THREAD_TIMING
			ent.trace = Tools.stackTrace2;
#endif
			if (highPriority) lock (mInstance.mPriority) mInstance.mPriority.Enqueue(ent);
			else lock (mInstance.mRegular) mInstance.mRegular.Enqueue(ent);
#endif
		}
	}
}
