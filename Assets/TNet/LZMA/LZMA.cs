//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System.IO;
using SevenZip.Compression.LZMA;

#if !STANDALONE
using UnityEngine;
#endif

/// <summary>
/// Convenience wrapper for the LZMA library by 7-Zip.
/// LZMA library is distributed under the public domain license: http://www.7-zip.org/sdk.html
/// </summary>

public class LZMA
{
	MemoryStream mIn;
	BinaryWriter mWriter;

	/// <summary>
	/// Begin the writing operation into the to-be-compressed buffer.
	/// </summary>

	public BinaryWriter BeginWriting()
	{
		mIn = new MemoryStream();
		mWriter = new BinaryWriter(mIn);
		return mWriter;
	}

	/// <summary>
	/// Compress the previously written data.
	/// </summary>

	public byte[] Compress ()
	{
		try
		{
			mIn.Seek(0, SeekOrigin.Begin);
			MemoryStream compressed = Compress(mIn);
			mIn.Seek(0, SeekOrigin.End);

			// Get the byte array
			BinaryReader reader = new BinaryReader(compressed);
			byte[] bytes = reader.ReadBytes((int)compressed.Length);
			reader.Close();
			return bytes;
		}
#if STANDALONE
		catch (System.Exception ex) { TNet.Tools.LogError(ex.Message, ex.StackTrace); }
#else
		catch (System.Exception ex) { Debug.LogError(ex); }
#endif
		return null;
	}

	/// <summary>
	/// LZMA compression helper function.
	/// </summary>

	static public MemoryStream Compress (Stream input, byte[] prefix = null)
	{
		try
		{
			MemoryStream output = new MemoryStream();
			long length = input.Length - input.Position;
			Encoder enc = new Encoder();

			// Write the prefix
			if (prefix != null) output.Write(prefix, 0, prefix.Length);

			// Write the header
			enc.WriteCoderProperties(output);

			// Write the buffer length
			output.Write(System.BitConverter.GetBytes(length), 0, 8);

			// Write the buffer
			enc.Code(input, output, length, -1, null);
			enc = null;

			output.Flush();
			output.Position = 0;
			return output;
		}
#if STANDALONE
		catch (System.Exception ex)
		{
			TNet.Tools.LogError(ex.Message, ex.StackTrace);
			return null;
		}
#else
		catch (System.Exception ex)
		{
			Debug.LogError(ex.Message + "\n" + ex.StackTrace);
			return null;
		}
#endif
	}

	/// <summary>
	/// LZMA compression helper function.
	/// </summary>

	static public byte[] Compress (byte[] data, byte[] prefix = null)
	{
		MemoryStream stream = new MemoryStream();
		stream.Write(data, 0, data.Length);
		stream.Position = 0;
		MemoryStream outStream = LZMA.Compress(stream, prefix);
		stream.Close();
		if (outStream != null) data = outStream.ToArray();
		outStream.Close();
		return null;
	}

	/// <summary>
	/// LZMA decompression helper function.
	/// </summary>

	static public MemoryStream Decompress (Stream input)
	{
		try
		{
			byte[] properties = new byte[5];
			if (5 != input.Read(properties, 0, 5)) return null;

			MemoryStream output = new MemoryStream();
			Decoder dec = new Decoder();

			// Read the coder properties
			dec.SetDecoderProperties(properties);

			// Read the buffer length
			byte[] lengthBytes = new byte[8];
			input.Read(lengthBytes, 0, 8);
			long length = System.BitConverter.ToInt64(lengthBytes, 0);

			// Read the data
			dec.Code(input, output, input.Length - input.Position, length, null);
			dec = null;

			// Reset the position to the beginning of the stream
			output.Flush();
			output.Position = 0;
			return output;
		}
#if STANDALONE
		catch (System.Exception ex)
		{
			TNet.Tools.LogError(ex.Message, ex.StackTrace);
			return null;
		}
#else
		catch (System.Exception ex)
		{
			Debug.LogError(ex.Message + "\n" + ex.StackTrace);
			return null;
		}
#endif
	}

	/// <summary>
	/// LZMA decompression helper function.
	/// </summary>

	static public byte[] Decompress (byte[] data, int offset = 0)
	{
		MemoryStream stream = new MemoryStream(data);
		if (offset > 0) stream.Position = offset;
		MemoryStream uncomp = Decompress(stream);
		byte[] retVal = (uncomp != null) ? uncomp.ToArray() : null;
		stream.Close();
		return retVal;
	}
}
