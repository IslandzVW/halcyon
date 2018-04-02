using NUnit.Framework;
using Nini.Config;
using InWorldz.Testing;
using InWorldz.Phlox.Types;
using Nini.Ini;
using OpenMetaverse;
using System.Collections.Generic;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.FrameworkTests;

namespace InWorldz.Phlox.Engine.Tests
{

    [TestFixture]
    public class MaterialsTests
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
            var sop = SceneUtil.RandomSOP("Root", 1);

            lslSystemApi = new LSLSystemAPI(engine, sop, 0, UUID.Zero);
        }

        [Test]
        public void TestGetMaterialsForFace()
        {
            var expectedResult = new LSLList(new List<object> { "dummy", "data" });
            var rules = new LSLList(new List<object> { ScriptBaseClass.PRIM_NORMAL, 0 });
            LSLList asList = lslSystemApi.llGetPrimitiveParams(rules);
            Assert.IsTrue(expectedResult.Equals(asList));
        }

    }
}
