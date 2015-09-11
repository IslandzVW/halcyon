namespace Nwc.XmlRpc.Tests
{
  using System;
  using System.Collections;
  using NUnit.Framework;
  using Nwc.XmlRpc;

  [TestFixture]
  public class SerializeDeserializeTest
  {
    [Test]
      public void Request()
      {
	ArrayList parms = new ArrayList();

	parms.Add(1);
	parms.Add("two");
	parms.Add(3.0);

	XmlRpcRequest reqIn = new XmlRpcRequest("object.method", parms);
	String str = (new XmlRpcRequestSerializer()).Serialize(reqIn);
	XmlRpcRequest reqOut = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(str);
	
	Assertion.AssertEquals("method name", reqIn.MethodName, reqOut.MethodName);
	Assertion.AssertEquals("Param count", reqIn.Params.Count, reqOut.Params.Count);
	
	for (int x = 0; x < 3; x++)
	  Assertion.AssertEquals("Param " + x, reqIn.Params[x], reqOut.Params[x]);
      }

    [Test]
      public void Response()
      {
	XmlRpcResponse respIn = new XmlRpcResponse(22,"Help Me");
	String str = (new XmlRpcResponseSerializer()).Serialize(respIn);
	XmlRpcResponse respOut = (XmlRpcResponse)(new XmlRpcResponseDeserializer()).Deserialize(str);

	Assertion.AssertEquals("faultcode", respIn.FaultCode, respOut.FaultCode);
	Assertion.AssertEquals("faultstring", respIn.FaultString, respOut.FaultString);
      }
  }
}
