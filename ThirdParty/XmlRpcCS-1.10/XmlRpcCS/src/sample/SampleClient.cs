namespace Sample
{
  using System;
  using System.Collections;
  using System.Diagnostics;
  using Nwc.XmlRpc;

  /// <summary>A very basic XML-RPC client example.</summary>
  /// <remarks>This is a basic client that calls several methods of the server including one
  /// intentionally bad one. One of the methods called works on the server.exe but is
  /// not exposed in the expose.exe, this gives you an idea of the error generated.</remarks>
  class SampleClient
  {
    private static String URL = "http://127.0.0.1:5050";

    /// <summary><c>LoggerDelegate</c> compliant method that does logging to Console.
    /// This method filters out the <c>LogLevel.Information</c> chatter.</summary>
    static public void WriteEntry(String msg, LogLevel level)
      {
	if (level > LogLevel.Information) // ignore debug msgs
	  Console.WriteLine("{0}: {1}", level, msg);
      }

    /// <summary>Main application method.</summary>
    /// <remarks> Simply sets up logging and then an <c>XmlRpcRequest</c> instance. Then
    /// Calls <c>sample.Ping</c>, <c>sample.Echo</c> and <c>sample.Broken</c> in sequence.
    /// The <c>XmlRpcResponse</c> from each call is then displayed. Faults are checked for.
    /// </remarks>
    public static void Main(String[] args) 
      {
	if (args.Length == 1)
	  {
	    URL = args[0];
	  }

	Console.WriteLine("Server: " + URL);

	// Use the console logger above.
	Logger.Delegate = new Logger.LoggerDelegate(WriteEntry);

	// Send the sample.Ping RPC using the Send method which gives you a little more control...
	XmlRpcRequest client = new XmlRpcRequest();
	client.MethodName = "sample.Ping";
	try
	  {
	    Console.WriteLine("Request: " + client);
	    XmlRpcResponse response = client.Send(URL, 10000);
	    Console.WriteLine("Response: " + response);

	    if (response.IsFault)
	      {
		Console.WriteLine("Fault {0}: {1}", response.FaultCode, response.FaultString);
	      }
	    else
	      {
		Console.WriteLine("Returned: " + response.Value);
	      }
	  }
	catch (Exception e)
	  {
	    Console.WriteLine("Exception " + e);
	  }

	// Invoke the sample.Echo RPC - Invoke more closely parallels a method invocation
	client.MethodName = "sample.Echo";
	client.Params.Clear();
	client.Params.Add("Hello");
	try
	  {
	    String echo = (String)client.Invoke(URL);
	    Console.WriteLine("Returned: " + echo);
	  }
	catch (XmlRpcException serverException)
	  {
	    Console.WriteLine("Fault {0}: {1}", serverException.FaultCode, serverException.FaultString);
	  }
	catch (Exception e)
	  {
	    Console.WriteLine("Exception " + e);
	  }


	// Invoke sample.Broken RPC - method that is not present on server.
	client.MethodName = "sample.Broken";
	client.Params.Clear();
	try
	  {
	    Object response = client.Invoke(URL);
	    Console.WriteLine("Response: " + response);
	  }
	catch (XmlRpcException serverException)
	  {
	    Console.WriteLine("Fault {0}: {1}", serverException.FaultCode, serverException.FaultString);
	  }
	catch (Exception e)
	  {
	    Console.WriteLine("Exception " + e);
	  }

      }
  }
}
