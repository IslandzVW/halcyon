namespace Sample
{
  using System;
  using System.Collections;
  using System.Diagnostics;
  using Nwc.XmlRpc;

  /// <summary>The interface describing the remote object.</summary>
  interface Server
  {
    /// <summary>Server's Ping method</summary>
    DateTime Ping();
    /// <summary>Server's Echo method</summary>
    String Echo(String arg);
    /// <summary>This method <b>does not</b> exist on the server, to demonstrate an error.</summary>
    Boolean BadMethod(); 
  }

  /// <summary>An XML-RPC client that employs a local proxy.</summary>
  /// <remarks></remarks>
  class ProxyClient
  {
    private static String URL = "http://127.0.0.1:5050";

    /// <summary>Main application method.</summary>
    /// <remarks>Simply sets up logging and then a proxy server instance. Then
    /// uses that proxy.
    /// </remarks>
    public static void Main(String[] args) 
      {
	if (args.Length == 1)
	  {
	    URL = args[0];
	  }

	// Create a proxy.
	Server server = (Server)XmlRpcClientProxy.createProxy("sample", URL, typeof(Server));

	// Excercise the proxy.
	Console.WriteLine("server.Echo(\"Foobar\") --> " + server.Echo("foobar"));
	Console.WriteLine("server.Ping() --> " + server.Ping());
	try
	  {
	    Console.WriteLine("server.BadMethod() --> " + server.BadMethod());
	  }
	catch (Exception e)
	  {
	    Console.WriteLine("Exception: " + e);
	  }
      }
  }
}
