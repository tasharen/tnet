//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2013 Tasharen Entertainment
//---------------------------------------------

namespace TNet
{
/// <summary>
/// This improved version of the System.Collections.Generic.List that doesn't release the buffer on Clear(),
/// resulting in better performance and less garbage collection.
/// </summary>

public class List<T>
{
	/// <summary>
	/// Direct access to the buffer. Note that you should not use its 'Length' parameter, but instead use List.size.
	/// </summary>

	public T[] buffer;

	/// <summary>
	/// Direct access to the buffer's size. Note that it's only public for speed and efficiency. You shouldn't modify it.
	/// </summary>

	public int size = 0;

	/// <summary>
	/// For 'foreach' functionality.
	/// </summary>

	public System.Collections.Generic.IEnumerator<T> GetEnumerator ()
	{
		if (buffer != null)
		{
			for (int i = 0; i < size; ++i)
			{
				yield return buffer[i];
			}
		}
	}

	/// <summary>
	/// Convenience function. I recommend using .buffer instead.
	/// </summary>

	public T this[int i]
	{
		get { return buffer[i]; }
		set { buffer[i] = value; }
	}

	/// <summary>
	/// Helper function that expands the size of the array, maintaining the content.
	/// </summary>

	void AllocateMore ()
	{
		int max = (buffer == null) ? 0 : (buffer.Length << 1);
		if (max < 32) max = 32;
		T[] newList = new T[max];
		if (buffer != null && size > 0) buffer.CopyTo(newList, 0);
		buffer = newList;
	}

	/// <summary>
	/// Trim the unnecessary memory, resizing the buffer to be of 'Length' size.
	/// Call this function only if you are sure that the buffer won't need to resize anytime soon.
	/// </summary>

	void Trim ()
	{
		if (size > 0)
		{
			if (size < buffer.Length)
			{
				T[] newList = new T[size];
				for (int i = 0; i < size; ++i) newList[i] = buffer[i];
				buffer = newList;
			}
		}
		else buffer = new T[0];
	}

	/// <summary>
	/// Clear the array by resetting its size to zero. Note that the memory is not actually released.
	/// </summary>

	public void Clear () { size = 0; }

	/// <summary>
	/// Clear the array and release the used memory.
	/// </summary>

	public void Release () { size = 0; buffer = null; }

	/// <summary>
	/// Add the specified item to the end of the list.
	/// </summary>

	public void Add (T item)
	{
		if (buffer == null || size == buffer.Length) AllocateMore();
		buffer[size++] = item;
	}

	/// <summary>
	/// Insert an item at the specified index, pushing the entries back.
	/// </summary>

	public void Insert (int index, T item)
	{
		if (buffer == null || size == buffer.Length) AllocateMore();

		if (index < size)
		{
			for (int i = size; i > index; --i) buffer[i] = buffer[i - 1];
			buffer[index] = item;
			++size;
		}
		else Add(item);
	}

	/// <summary>
	/// Returns 'true' if the specified item is within the list.
	/// </summary>

	public bool Contains (T item)
	{
		if (buffer == null) return false;
		for (int i = 0; i < size; ++i) if (buffer[i].Equals(item)) return true;
		return false;
	}

	/// <summary>
	/// Remove the specified item from the list. Note that RemoveAt() is faster and is advisable if you already know the index.
	/// </summary>

	public bool Remove (T item)
	{
		if (buffer != null)
		{
			System.Collections.Generic.EqualityComparer<T> comp =
				System.Collections.Generic.EqualityComparer<T>.Default;

			for (int i = 0; i < size; ++i)
			{
				if (comp.Equals(buffer[i], item))
				{
					--size;
					buffer[i] = default(T);
					for (int b = i; b < size; ++b) buffer[b] = buffer[b + 1];
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Remove an item at the specified index.
	/// </summary>

	public void RemoveAt (int index)
	{
		if (buffer != null && index < size)
		{
			--size;
			buffer[index] = default(T);
			for (int b = index; b < size; ++b) buffer[b] = buffer[b + 1];
		}
	}

	/// <summary>
	/// Remove an item from the end.
	/// </summary>

	public T Pop ()
	{
		if (buffer != null && size != 0)
		{
			T val = buffer[--size];
			buffer[size] = default(T);
			return val;
		}
		return default(T);
	}

	/// <summary>
	/// Mimic List's ToArray() functionality, except that in this case the list is resized to match the current size.
	/// </summary>

	public T[] ToArray () { Trim(); return buffer; }

	/// <summary>
	/// List.Sort equivalent.
	/// </summary>

	public void Sort (System.Comparison<T> comparer)
	{
		bool changed = true;

		while (changed)
		{
			changed = false;

			for (int i = 1; i < size; ++i)
			{
				if (comparer.Invoke(buffer[i - 1], buffer[i]) > 0)
				{
					T temp = buffer[i];
					buffer[i] = buffer[i - 1];
					buffer[i - 1] = temp;
					changed = true;
				}
			}
		}
	}
}
}