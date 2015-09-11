namespace Sample
{
  using System;
  using System.Diagnostics;
  using System.IO;
  using System.Xml;
  using Nwc.XmlRpc;

  ///<remarks>
  /// This example simple deserializes and request from a file and the reserializes
  /// it to display.  Tests the request deserializer and serializer. </remarks>
  class RequestXmlTest
  {

    /// <summary><c>LoggerDelegate</c> compliant method that does logging to Console.
    /// This method filters out the <c>LogLevel.Information</c> chatter.</summary>
    static public void WriteEntry(String msg, LogLevel level)
      {
	if (level > LogLevel.Information) // ignore debug msgs
	  Console.WriteLine("{0}: {1}", level, msg);
      }

    /// <summary>Classes Main method.</summary>
    /// <remarks>This method opens an XML file as a <c>StreamReader</c> and then asks
    /// <c>XmlRpcRequestDeserializer.Parse</c> to deserialize it into an <c>XmlRpcRequest</c>. The 
    /// resultant request is now displayed.
    /// </remarks>
    public static void Main(String[] args) 
      {
	Logger.Delegate = new Logger.LoggerDelegate(WriteEntry);
	XmlRpcRequestDeserializer deserializer = new XmlRpcRequestDeserializer();

	Console.WriteLine("Attempting to deserialize " + args[0]);
	StreamReader input = new StreamReader(args[0]);
	XmlRpcRequest req = (XmlRpcRequest)deserializer.Deserialize(input);
	Console.WriteLine(req);
      }
  }
}
