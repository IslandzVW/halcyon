namespace Nwc.XmlRpc.Tests
{
  using System;
  using System.Collections;
  using NUnit.Framework;
  using Nwc.XmlRpc;

  [TestFixture]
  public class XmlRpcSystemObjectTest
  {
    private Exposed _e = new Exposed();

    [Test]
    [ExpectedException(typeof(XmlRpcException))]
      public void InvalidArgCount()
      {
	ArrayList parms = new ArrayList();
	parms.Add(this);
	XmlRpcSystemObject.Invoke(this,"InvalidMethod", parms);
      }

    [Test]
    [ExpectedException(typeof(XmlRpcException))]
      public void InvalidMethodName()
      {
	ArrayList parms = new ArrayList();
	parms.Add(this);
	XmlRpcSystemObject.Invoke(this, "InvalidWombat", parms);
      }

    [Test]
    [ExpectedException(typeof(XmlRpcException))]
      public void InvalidObject()
      {
	ArrayList parms = new ArrayList();
	parms.Add(this);
	XmlRpcSystemObject.Invoke(null, "InvalidWombat", parms);
      }


    [Test]
      public void ValidCalls()
      {
	ArrayList parms = new ArrayList();
	Assertion.AssertEquals("Basic call",XmlRpcSystemObject.Invoke(this, "CallMe", parms),true);
	parms.Add("Hello");
	Assertion.AssertEquals("Return Value",XmlRpcSystemObject.Invoke(this, "Echo", parms),"Hello");
      }

    public Boolean CallMe()
      {
	return true;
      }

    public String Echo(String str)
      {
	return str;
      }
  }
}
