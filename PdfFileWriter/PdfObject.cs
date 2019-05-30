/////////////////////////////////////////////////////////////////////
//
//	PdfFileWriter
//	PDF File Write C# Class Library.
//
//	PdfObject
//	Base class for all PDF indirect object classes.
//
//	Uzi Granot
//	Version: 1.0
//	Date: April 1, 2013
//	Copyright (C) 2013-2019 Uzi Granot. All Rights Reserved
//
//	PdfFileWriter C# class library and TestPdfFileWriter test/demo
//  application are free software.
//	They is distributed under the Code Project Open License (CPOL).
//	The document PdfFileWriterReadmeAndLicense.pdf contained within
//	the distribution specify the license agreement and other
//	conditions and notes. You must read this document and agree
//	with the conditions specified in order to use this software.
//
//	For version history please refer to PdfDocument.cs
//
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PdfFileWriter
{
/////////////////////////////////////////////////////////////////////
// Resource code enumeration
/////////////////////////////////////////////////////////////////////

internal enum ResCode
	{
	// must be in this order
	Font,
	Pattern,
	Shading,
	XObject,
	ExtGState,
	OpContent,
	Length
	}

internal enum ObjectType
	{
	Other,
	Dictionary,
	Stream,
	}

////////////////////////////////////////////////////////////////////
/// <summary>
/// PDF indirect object base class
/// </summary>
/// <remarks>
/// PDF indirect object base class.
/// User program cannot call it directly.
/// </remarks>
////////////////////////////////////////////////////////////////////
public class PdfObject : IComparable<PdfObject>
	{
	/// <summary>
	/// PDF document object
	/// </summary>
	public PdfDocument Document {get; internal set;}

	/// <summary>
	/// Scale factor
	/// </summary>
	/// <remarks>Convert from user unit of measure to points.</remarks>
	public double ScaleFactor {get; internal set;}

	internal	int				ObjectNumber;		// PDF indirect object number
	internal	string				ResourceCode;		// resource code automatically generated by the program
	internal	long				FilePosition;		// PDF file position for this indirect object
	internal	ObjectType			ObjectType;			// object type
	internal	List<byte>			ObjectValueList;
	internal	byte[]				ObjectValueArray;
	internal	PdfDictionary		Dictionary;			// indirect objects dictionary or stream dictionary
	internal	bool				NoCompression;

	private  static string[]		ResCodeStr = {"/Font <<", "/Pattern <<", "/Shading <<", "/XObject <<", "/ExtGState <<", "/Properties <<"};
	internal static string			ResCodeLetter = "FPSXGO";

	internal PdfObject() {}

	////////////////////////////////////////////////////////////////////
	// Constructor for objects with /Type in their dictionary
	// Note: access is internal. Used by derived classes only
	////////////////////////////////////////////////////////////////////

	internal PdfObject
			(
			PdfDocument	Document,
			ObjectType	Type = ObjectType.Dictionary,
			string		PdfDictType = null	// object type (i.e. /Catalog, /Pages, /Font, /XObject, /OCG)
			)
		{
		// save link to main document object
		this.Document = Document;

		// save scale factor
		ScaleFactor = Document.ScaleFactor;

		// save type
		this.ObjectType = Type;

		// no compression
		NoCompression = Document.Debug;

		// if object is stream or a dictionary define empty dictionary
		if(Type != ObjectType.Other) Dictionary = new PdfDictionary(this);

		// if object is not a dictionary define empty contents stream
		if(Type != ObjectType.Dictionary) ObjectValueList = new List<byte>();

		// if object name is specified, create a dictionary and add /Type Name entry
		if(!string.IsNullOrEmpty(PdfDictType)) Dictionary.Add("/Type", PdfDictType);

		// set PDF indirect object number to next available number
		this.ObjectNumber = Document.ObjectArray.Count + 1;

		// add the new object to object array
		Document.ObjectArray.Add(this);
		return;
		}

	////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Compare the resource codes of two PDF objects.
	/// </summary>
	/// <param name="Other">Other PdfObject</param>
	/// <returns>Compare result</returns>
	/// <remarks>
	/// Used by PdfContents to maintain resource objects in sorted order.
	/// </remarks>
	////////////////////////////////////////////////////////////////////
	public int CompareTo
			(
			PdfObject Other		// the second object
			)
		{
		return(string.Compare(this.ResourceCode, Other.ResourceCode));
		}

	////////////////////////////////////////////////////////////////////
	// Convert user coordinates or line width to points.
	// The result is rounded to 6 decimal places and converted to Single.
	////////////////////////////////////////////////////////////////////

	internal Single ToPt
			(
			double	Value		// coordinate value in user unit of measure
			)
		{
		double ReturnValue = ScaleFactor * Value;
		if(Math.Abs(ReturnValue) < 0.0001) ReturnValue = 0;
		return((Single) ReturnValue);
		}

	////////////////////////////////////////////////////////////////////
	// Round unscaled numbers.
	// The value is rounded to 6 decimal places and converted to Single
	////////////////////////////////////////////////////////////////////

	internal Single Round
			(
			double	Value		// a number to be saved in contents
			)
		{
		if(Math.Abs(Value) < 0.0001) Value = 0;
		return((Single) Value);
		}

	internal void ObjectValueAppend
			(
			string				Str
			)
		{
		// convert content from string to binary
		foreach(char Chr in Str) ObjectValueList.Add((byte) Chr);
		return;
		}

	internal void ObjectValueFormat
			(
			string				FormatStr,
			params	object[]	List
			)
		{
		// format input arguments
		string Str = string.Format(NFI.PeriodDecSep, FormatStr, List);

		// convert content from string to binary
		foreach(char Chr in Str) ObjectValueList.Add((byte) Chr);
		return;
		}

	////////////////////////////////////////////////////////////////////
	// Convert resource dictionary to one String.
	// This method is called at the last step of document creation
	// from within PdfDocument.CreateFile(FileName).
	// it is relevant to page contents, X objects and tiled pattern
	// Return value is resource dictionary string.
	////////////////////////////////////////////////////////////////////
	
	internal string BuildResourcesDictionary
			(
			List<PdfObject>		ResObjects,		// list of resource objects for this contents
			bool				AddProcSet		// for page contents we need /ProcSet 
			)
		{
		// resource object list is empty
		// if there are no resources an empty dictionary must be returned
		if(ResObjects == null || ResObjects.Count == 0)
			{
			return(AddProcSet ? "<</ProcSet [/PDF/Text]>>" : "<<>>");
			}

		// resources dictionary content initialization
		StringBuilder Resources = new StringBuilder("<<");

		// for page object
		if(AddProcSet) Resources.Append("/ProcSet [/PDF/Text/ImageB/ImageC/ImageI]\n");

		// add all resources
		char ResCodeType = ' ';
		foreach(PdfObject Resource in ResObjects)
			{
			// resource code is /Xnnn
			if(Resource.ResourceCode[1] != ResCodeType)
				{
				// terminate last type
				if(ResCodeType != ' ') Resources.Append(">>\n");

				// start new type
				ResCodeType = Resource.ResourceCode[1];
				Resources.Append(ResCodeStr[ResCodeLetter.IndexOf(ResCodeType)]);
				}

			// append resource code
			if(Resource.GetType() != typeof(PdfFont))
				{
				Resources.Append(string.Format("{0} {1} 0 R", Resource.ResourceCode, Resource.ObjectNumber));
				}
			else
				{
				PdfFont Font = (PdfFont) Resource;
				if(Font.FontResCodeUsed) Resources.Append(string.Format("{0} {1} 0 R", Font.ResourceCode, Font.ObjectNumber));
				if(Font.FontResGlyphUsed) Resources.Append(string.Format("{0} {1} 0 R", Font.ResourceCodeGlyph, Font.GlyphIndexFont.ObjectNumber));
				}
			}

		// terminate last type and resource dictionary
		Resources.Append(">>\n>>");

		// exit
		return(Resources.ToString());
		}

	////////////////////////////////////////////////////////////////////
	// Write object to PDF file
	// Called by PdfDocument.CreateFile(FileName) method
	// to output one indirect PDF object.
	// It is a virtual method. Derived classes can overwrite it.
	////////////////////////////////////////////////////////////////////

	internal virtual void WriteObjectToPdfFile()
		{
		// save file position for this object
		FilePosition = Document.PdfFile.BaseStream.Position;

		// write object header
		Document.PdfFile.WriteFormat("{0} 0 obj\n", ObjectNumber);

		// switch based on the type of PDF indirect object
		switch(ObjectType)
			{
			case ObjectType.Stream:
				// convert byte list to array
				if(ObjectValueList.Count > 0) ObjectValueArray = ObjectValueList.ToArray();

				// application test
				if(ObjectValueArray == null) ObjectValueArray = new byte[0];

				// compression is disabled
				if(!NoCompression) ObjectValueArray = CompressStream(ObjectValueArray);

				// encryption
				if(Document.Encryption != null) ObjectValueArray = Document.Encryption.EncryptByteArray(ObjectNumber, ObjectValueArray);

				// stream length
				Dictionary.AddInteger("/Length", ObjectValueArray.Length);

				// write dictionary
				Dictionary.WriteToPdfFile();

				// write stream reserved word
				Document.PdfFile.WriteString("stream\n");

				// write content to pdf file
				Document.PdfFile.Write(ObjectValueArray);

				// write end of stream
				Document.PdfFile.WriteString("\nendstream\nendobj\n");
				break;

			case ObjectType.Dictionary:
				// write dictionary
				Dictionary.WriteToPdfFile();

				// output object trailer
				Document.PdfFile.WriteString("endobj\n");
				break;

			case ObjectType.Other:
				// convert byte list to array
				if(ObjectValueList.Count > 0) ObjectValueArray = ObjectValueList.ToArray();

				// we have contents but no dictionary
				// write content to pdf file
				Document.PdfFile.Write(ObjectValueArray);
	
				// output object trailer
				Document.PdfFile.WriteString("\nendobj\n");
				break;
			}

		// resources not used
		Dictionary = null;
		ObjectValueList = null;
		ObjectValueArray = null;
		return;
		}

	////////////////////////////////////////////////////////////////////
	// Compress byte array
	////////////////////////////////////////////////////////////////////
	
	internal byte[] CompressStream
			(
			byte[]			InputBuf
			)
		{
		// input length
		int InputLen = InputBuf.Length;

		// input buffer too small to compress
		if(InputLen < 16) return(InputBuf);

		// create output memory stream to receive the compressed buffer
		MemoryStream OutputStream = new MemoryStream();

		// deflate compression object
		DeflateStream Deflate = new DeflateStream(OutputStream, CompressionMode.Compress, true);

		// load input buffer into the compression class
		Deflate.Write(InputBuf, 0, InputBuf.Length);

		// compress, flush and close
		Deflate.Close();

		// compressed file length
		int OutputLen = (int) OutputStream.Length;

		// make sure compressed stream is shorter than input stream
		if(OutputLen + 6 >= InputLen) return(InputBuf);

		// create output buffer
		byte[] OutputBuf = new byte[OutputLen + 6];

		// write two bytes in most significant byte first
		OutputBuf[0] = (byte) 0x78;
		OutputBuf[1] = (byte) 0x9c;

		// copy the compressed result
		OutputStream.Seek(0, SeekOrigin.Begin);
		OutputStream.Read(OutputBuf, 2, OutputLen);
		OutputStream.Close();

		// reset adler32 checksum
		uint ReadAdler32 = Adler32Checksum(InputBuf);

		// ZLib checksum is Adler32 write it big endian order, high byte first
		OutputLen += 2;
		OutputBuf[OutputLen++] = (byte) (ReadAdler32 >> 24);
		OutputBuf[OutputLen++] = (byte) (ReadAdler32 >> 16);
		OutputBuf[OutputLen++] = (byte) (ReadAdler32 >> 8);
		OutputBuf[OutputLen] = (byte) ReadAdler32;

		// update dictionary
		Dictionary.Add("/Filter", "/FlateDecode");
		
		// successful exit
		return(OutputBuf);
		}

	/////////////////////////////////////////////////////////////////////
	// Accumulate Adler Checksum
	/////////////////////////////////////////////////////////////////////

	private uint Adler32Checksum
			(
			byte[]		Buffer
			)
		{
		const uint Adler32Base = 65521;

		// split current Adler checksum into two 
		uint AdlerLow = 1; // AdlerValue & 0xFFFF;
		uint AdlerHigh = 0; // AdlerValue >> 16;

		int Len = Buffer.Length;
		int Pos = 0;
		while(Len > 0) 
			{
			// We can defer the modulo operation:
			// Under worst case the starting value of the two halves is 65520 = (AdlerBase - 1)
			// each new byte is maximum 255
			// The low half grows AdlerLow(n) = AdlerBase - 1 + n * 255
			// The high half grows AdlerHigh(n) = (n + 1)*(AdlerBase - 1) + n * (n + 1) * 255 / 2
			// The maximum n before overflow of 32 bit unsigned integer is 5552
			// it is the solution of the following quadratic equation
			// 255 * n * n + (2 * (AdlerBase - 1) + 255) * n + 2 * (AdlerBase - 1 - uint.MaxValue) = 0
			int n = Len < 5552 ? Len : 5552;
			Len -= n;
			while(--n >= 0) 
				{
				AdlerLow += (uint) Buffer[Pos++];
				AdlerHigh += AdlerLow;
				}
			AdlerLow %= Adler32Base;
			AdlerHigh %= Adler32Base;
			}
		return((AdlerHigh << 16) | AdlerLow);
		}
	}
}
