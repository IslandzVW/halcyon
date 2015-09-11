namespace Sample
{
  using System;
  using System.Diagnostics;
  using Nwc.XmlRpc;

  ///<summary>
  /// An XML-RPC server that utilizes XmlRpcExposed attribute to restrict access.
  ///</summary>
  ///<remarks>
  ///This server registers itself as the "sample" object. This example differs from
  /// SampleServer in that it uses the XmlRpcExposed attribute to indicate which
  /// public methods are exposed via XML-RPC. Normally ALL public methods are
  /// considered to be exposed, but if the XmlRpcExposed attribute is present at the
  ///class level then ONLY public methods having the XmlRpcExposed attribute will be
  ///exposed.
  ///<para>
  ///Run the soc client against the two servers to see the difference. Running
  ///client.exe against this server should generate an error as well since it
  ///attempts to run a method that is not exposed.</para>
  ///</remarks>
  [XmlRpcExposed]		// Indicate this class uses XmlRpcExposed to restrict method access
  class SampleServerWithExpose
  {
    const int PORT = 5050;

    /// <summary>The application starts here.</summary>
    /// <remarks>This method instantiates an <c>XmlRpcServer</c> as an embedded XML-RPC server,
    /// then add this object to the server as an XML-RPC handler, and finally starts the server.</remarks>
    public static void Main() 
      {
	XmlRpcServer server = new XmlRpcServer(PORT);
	server.Add("sample", new SampleServerWithExpose());
	Console.WriteLine("Web Server Running on port {0} ... Press ^C to Stop...", PORT);
	server.Start();
      }

    /// <summary>A method that returns the current time.</summary>
    /// <return>The current <c>DateTime</c> of the server is returned.</return>
    [XmlRpcExposed]		// Make this method XML-RPC exposed
    public DateTime Ping()
      {
	return DateTime.Now;
      }

    /// <summary>A method that echos back it's arguement.
    /// Note this is public BUT NOT XML-RPC exposed! 
    /// </summary>
    /// <param name="arg">A <c>String</c> to echo back to the caller.</param>
    /// <return>Return, as a <c>String</c>, the <paramref>arg</paramref> that was passed in.</return>
    public String Echo(String arg)
      {
	return arg;
      }
  }
}
