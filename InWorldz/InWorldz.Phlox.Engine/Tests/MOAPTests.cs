using NUnit.Framework;
using Nini.Config;
using InWorldz.Testing;
using InWorldz.Phlox.Types;
using Nini.Ini;
using OpenMetaverse;
using System.Collections.Generic;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.FrameworkTests;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Interfaces;

namespace InWorldz.Phlox.Engine.Tests
{
    class MockApi : LSLSystemAPI
    {
        public MockApi(IScriptEngine ScriptEngine, SceneObjectPart host, uint localID, UUID itemID) : base(ScriptEngine, host, localID, itemID)
        {
        }

        protected override void ScriptSleep(int delay)
        {
            return;
        }
    }

    [TestFixture]
    public class MOAPTests
    {
        Scene world;
        MockScriptEngine engine;
        LSLSystemAPI api;

        [SetUp]
        public void Setup()
        {
            var iniDoc = new IniDocument();
            var configSource = new IniConfigSource(iniDoc);
            configSource.AddConfig("InWorldz.Phlox");
            world = SceneHelper.CreateScene(9000, 1000, 1000);
            var module = new OpenSim.Region.CoreModules.World.Media.Moap.MoapModule();
            module.Initialize(configSource);
            module.AddRegion(world);

            engine = new MockScriptEngine(world, configSource);
            var sop = SceneUtil.RandomSOP("Root", 1);
            var group = new SceneObjectGroup(sop);
            api = new MockApi(engine, sop, 0, UUID.Zero);
        }

        [TearDown]
        public void Teardown()
        {
            SceneHelper.TearDownScene(world);
        }

        [Test]
        public void TestPrimMediaClear()
        {

            api.llClearPrimMedia(0);
/*
            var expectedResult = new LSLList(new List<object> { UUID.Zero.ToString(), new Vector3(1, 1, 0), new Vector3(0, 0, 0), 0 });
            var rules = new LSLList(new List<object> { ScriptBaseClass.PRIM_NORMAL, 0 });
            LSLList asList = lslSystemApi.llGetPrimitiveParams(rules);
            Assert.That(asList.ToString(), Is.EqualTo(expectedResult.ToString()));
*/
        }

    }
}
