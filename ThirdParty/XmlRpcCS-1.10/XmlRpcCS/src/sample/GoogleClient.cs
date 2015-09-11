namespace Sample
{
  using System;
  using System.Collections;
  using Nwc.XmlRpc;

  /// <summary>An XML-RPC client using the Google APIs.</summary>
  /// <remarks>Google provides basic XML-RPC APIs.  See http://www.xmlrpc.com/googleGateway and
  /// http://www.google.com/apis/ for more info on them. This client simply asks 
  /// for the alternate spelling of a phrase and performs a basic search.
  /// <p/>Please note you need a key and keys are limited to 1000 queries a day so if you really
  /// plan to play with this example get you own key and replace mine.
  /// </remarks>
  class GoogleClient
  {
    static private readonly String KEY = "ze35KTVsYUdn39S6TLLyOeelInY1kADa";
    static private readonly String PHRASE = "This iz a teste prase.";
    static private readonly String URL = "http://google.xmlrpc.com/RPC2";
    static private readonly String SPELL_CHECK = "googleGateway.spellingSuggestion";
    static private readonly String RESULTS = "googleGateway.search";

    /// <summary><c>LoggerDelegate</c> compliant method that does logging to Console.
    /// This method filters out the <c>LogLevel.Information</c> chatter.</summary>
    static public void WriteEntry(String msg, LogLevel level)
      {
	if (level > LogLevel.Information) // ignore debug msgs
	  Console.WriteLine("{0}: {1}", level, msg);
      }

    /// <summary>Submit two XML-RPC calls to Google. One to suggest spelling on a phrase and
    /// the next to do a basic search.</summary>
    public static void Main() 
      {
	// Use the console logger above.
	Logger.Delegate = new Logger.LoggerDelegate(WriteEntry);

	// Send the sample.Ping RPC using the Send method which gives you a little more control...
	XmlRpcRequest client = new XmlRpcRequest();
	client.MethodName = SPELL_CHECK;
	client.Params.Add(PHRASE);
	client.Params.Add(KEY);
	Console.WriteLine("Looking up: " + PHRASE);
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
	catch (XmlRpcException serverException)
	  {
	    Console.WriteLine("Fault {0}: {1}", serverException.FaultCode, serverException.FaultString);
	  }
	catch (Exception e)
	  {
	    Console.WriteLine("Exception " + e);
	  }


	client.MethodName = RESULTS;
	client.Params.Clear();
	client.Params.Add("C# XML-RPC .Net");
	client.Params.Add(0);
	client.Params.Add(5);
	client.Params.Add("");
	client.Params.Add("");
	client.Params.Add(false);
	client.Params.Add("");
	client.Params.Add("");
	client.Params.Add(KEY);

	try
	  {
	    Hashtable results = (Hashtable)client.Invoke(URL);
	    foreach (Hashtable result in ((Hashtable)results["resultElements"]).Values)
	      {
		Console.WriteLine("Title: {0}",result["title"]);
		Console.WriteLine("URL: {0}",result["URL"]);
		Console.WriteLine("Context: {0}",result["snippet"]);
		Console.WriteLine("------------------------------------------------------------");
	      }
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
