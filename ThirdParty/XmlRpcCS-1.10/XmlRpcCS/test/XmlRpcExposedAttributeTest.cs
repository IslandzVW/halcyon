namespace Nwc.XmlRpc.Tests
{
  using System;
  using NUnit.Framework;
  using Nwc.XmlRpc;

  [XmlRpcExposed]
  public class Exposed
  {
    [XmlRpcExposed]
      public void Open() {}
    public void Closed() {}
  }

  [TestFixture]
  public class XmlRpcExposedAttributeTest
  {
    private Exposed _e = new Exposed();

    [Test]
    [ExpectedException(typeof(MissingMethodException))]
      public void InvalidMethod()
      {
	XmlRpcExposedAttribute.ExposedMethod(this,"fobar");
      }

    [Test]
      public void ValidMethod()
      {
	XmlRpcExposedAttribute.ExposedMethod(this,"ValidMethod");
      }

    [Test]
      public void MethodAccess()
      {
	Assertion.Assert("Method on normal object", XmlRpcExposedAttribute.ExposedMethod(this,"MethodAccess"));
	Assertion.Assert("Exposed method on exposed object", XmlRpcExposedAttribute.ExposedMethod(_e,"Open"));
	Assertion.Assert("Closed method on exposed object", !XmlRpcExposedAttribute.ExposedMethod(_e,"Closed"));
      }
  }
}
