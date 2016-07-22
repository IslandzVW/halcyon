using NUnit.Framework;
using Nini.Config;
using InWorldz.Testing;
using InWorldz.Phlox.Types;
using Nini.Ini;
using OpenMetaverse;
using System.Collections.Generic;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;

namespace InWorldz.Phlox.Engine.Tests
{

    [TestFixture]
    public class JsonTests
    {
        LSLSystemAPI lslSystemApi;

        [TestFixtureSetUp]
        public void Setup()
        {
            var iniDoc = new IniDocument();
            var configSource = new IniConfigSource(iniDoc);
            configSource.AddConfig("InWorldz.Phlox");
            var world = SceneHelper.CreateScene(9000, 1000, 1000);
            var engine = new MockScriptEngine(world, configSource);
            lslSystemApi = new LSLSystemAPI(engine, null, 0, UUID.Zero);
        }

        [Test]
        public void TestJsonSimpleObject()
        {
            var expectedResult = new LSLList(new List<object> { "dummy", "data" });
            LSLList asList = lslSystemApi.llJson2List("{ \"dummy\" : \"data\" }");
            Assert.IsTrue(expectedResult.Equals(asList));
        }

        [Test]
        public void TestJsonSimpleObjectWithNullInValue()
        {
            var expectedResult = new LSLList(new List<object>{ "dummy", ScriptBaseClass.JSON_NULL });
            var asList = lslSystemApi.llJson2List("{ \"dummy\" : null }");
            Assert.IsTrue(expectedResult.Equals(asList));
        }

        [Test]
        public void TestJsonArrayWithNull()
        {
            var expectedResult = new LSLList(new List<object> { ScriptBaseClass.JSON_NULL });
            var asList = lslSystemApi.llJson2List("[ null ]");
            Assert.IsTrue(expectedResult.Equals(asList));
        }
    }
}
