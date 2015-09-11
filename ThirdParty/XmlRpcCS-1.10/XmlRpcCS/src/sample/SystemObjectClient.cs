namespace Sample
{
  using System;
  using System.Collections;
  using Nwc.XmlRpc;

  /// <summary>Example of boxcarring and using the system object.</summary>
  /// <remarks>
  /// This example class calls the system object to get a list of methods available, and
  /// then boxcars a collection of calls to get the signature of each of the methods
  /// returned in the original list.
  /// </remarks>
  class SystemObjectClient
  {
    /// <summary>The URL of the XML-RPC server.</summary>
    const String SERVER_URL = "http://127.0.0.1:5050";

    /// <summary>
    /// The Main method, the entire class is embodied in this single method. This method simply
    /// creates an XmlRpcRequest to system.listMethods which is sent. If all goes well a list
    /// of methods is returned. That list is then used to make a collection of XmlRpcRequests
    /// each a call to system.methodSignature. This collection of requests is not sent
    /// individually but instead is "boxcarred" together and sent all at once.
    /// <para>
    /// The results of the second call is a collection of results to each individual request
    /// bundled into an array. The array of results is then formatted for output.</para>
    /// </summary>
    public static void Main() 
      {
	XmlRpcResponse response;
	IList methods = null;
	XmlRpcRequest client = new XmlRpcRequest();

	/*
	 * Get the list of methods.
	 */
	client.MethodName = "system.listMethods";
	client.Params.Clear();
	response = client.Send(SERVER_URL, 10000);
	if (response.IsFault)
	  {
	    Console.WriteLine("Fault {0}: {1}", response.FaultCode, response.FaultString);
	    return;
	  }
	else
	  {
	    methods = (IList)response.Value;
	  }

	/*
	 * Create a boxcar and add an XmlRpcRequest for each call to be included.
	 */
	XmlRpcBoxcarRequest br = new XmlRpcBoxcarRequest();
	foreach (String method in methods)
	  {
	    client = new XmlRpcRequest();
	    client.MethodName = "system.methodSignature";
	    client.Params.Add(method);
	    br.Requests.Add(client);

	    client = new XmlRpcRequest();
	    client.MethodName = "system.methodHelp";
	    client.Params.Add(method);
	    br.Requests.Add(client);
	  }

	// Submit the boxcarred request
	response = br.Send(SERVER_URL, 10000);

	if (response.IsFault)
	  Console.WriteLine("Fault {0}: {1}", response.FaultCode, response.FaultString);
	else
	  {
	    int subResponseCount = -1;
	    // travers the list of responses
	    foreach (Object subResponse in (ArrayList)response.Value)
	      {
		subResponseCount++;

		if (subResponse is IDictionary)
		  {		// Response is a Hashtable - indecates a fault
		    Console.WriteLine("Fault {0}: {1}", ((Hashtable)subResponse)[XmlRpcXmlTokens.FAULT_CODE], 
				      ((Hashtable)subResponse)[XmlRpcXmlTokens.FAULT_STRING]);		    
		    continue;
		  }
		
		if (subResponse is IList)
		  {
		    Object returnValue = ((ArrayList)subResponse)[0];

		    if (returnValue is String)
		      {
			Console.WriteLine(returnValue);
			Console.WriteLine("\n");
		      }

		    if (returnValue is IList)
		      {
			foreach (IList signature in ((IList)returnValue))
			  {
			    Console.Write(signature[0]);
			    Console.Write(" " + methods[subResponseCount / 2] + "(");		
			    for (int x = 1; x < signature.Count; x++)
			      {
				if (x > 1)
				  Console.Write(", ");
				Console.Write(signature[x]);
			      }
			    Console.WriteLine(")");
			  }
		      }
		  }
	      }
	  }
      }
  }
}
