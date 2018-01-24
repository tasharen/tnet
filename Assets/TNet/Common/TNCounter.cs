//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.IO;

namespace TNet
{
/// <summary>
/// Counter is a value that automatically changes with time. It's ideal for resources.
/// </summary>

public class Counter : IBinarySerializable, IDataNodeSerializable
{
	public double min;		// Minimum value
	public double max;		// Maximum value
	public double rate;		// Value's rate of change per second

	double mValue;
	long mTimestamp;

	/// <summary>
	/// Actual stored value. In most cases you will want to use 'value' instead.
	/// </summary>

	public double storedValue { get { return mValue; } }

	/// <summary>
	/// Current value.
	/// </summary>

	public double value
	{
		get
		{
#if STANDALONE
			var time = System.DateTime.UtcNow.Ticks / 10000;
#else
			var time = TNManager.serverTime;
#endif
			if (mTimestamp != time)
			{
				var delta = (time - mTimestamp);
				if (delta < 0) delta = 0;
				mTimestamp = time;
				mValue += delta * 0.001 * rate;
				if (mValue < min) mValue = min;
				else if (mValue > max) mValue = max;
			}
			return mValue;
		}
		set
		{
			mValue = value;
#if STANDALONE
			mTimestamp = System.DateTime.UtcNow.Ticks / 10000;
#else
			mTimestamp = TNManager.serverTime;
#endif
		}
	}

	public Counter () { max = double.MaxValue; }

	public Counter (double value, double rate = 0.0, double min = 0.0, double max = double.MaxValue)
	{
		this.value = value;
		this.rate = rate;
		this.min = min;
		this.max = max;
	}

	public virtual void Serialize (BinaryWriter writer)
	{
		writer.Write(min);
		writer.Write(max);
		writer.Write(rate);
		writer.Write(value);
		writer.Write(mTimestamp);
	}

	public virtual void Deserialize (BinaryReader reader)
	{
		min = reader.ReadDouble();
		max = reader.ReadDouble();
		rate = reader.ReadDouble();
		mValue = reader.ReadDouble();
		mTimestamp = reader.ReadInt64();
	}

	public virtual void Serialize (DataNode node)
	{
		node.AddChild("min", min);
		node.AddChild("max", max);
		node.AddChild("rate", rate);
		node.AddChild("value", value);
		node.AddChild("time", mTimestamp);
	}

	public virtual void Deserialize (DataNode node)
	{
		min = node.GetChild<double>("min");
		max = node.GetChild<double>("max");
		rate = node.GetChild<double>("rate");
		mValue = node.GetChild<double>("value");
		mTimestamp = node.GetChild<long>("time");
	}

	/// <summary>
	/// Copy the counter's values over to another.
	/// </summary>

	public virtual void CopyTo (Counter c)
	{
		c.min = min;
		c.max = max;
		c.rate = rate;
		c.mValue = mValue;
		c.mTimestamp = mTimestamp;
	}

	/// <summary>
	/// Create a copy of this counter.
	/// </summary>

	public virtual Counter Clone () { var c = new Counter(); CopyTo(c); return c; }

	/// <summary>
	/// Given the time delta, calculate how long the resource will last relative to that delta.
	/// </summary>

	public double GetPercent (double delta = 1.0)
	{
		var change = this.rate * delta;

		if (change != 0.0)
		{
			var next = value + change;

			if (change < min)
			{
				if (next < min) return 1.0 - (next - min) / change;
			}
			else if (change > 0.0)
			{
				if (next > max) return 1.0 - (next - max) / change;
			}
		}
		return 1.0;
	}

	public override string ToString () { return value.ToString(); }
}
}
