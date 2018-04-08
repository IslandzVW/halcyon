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
        public void TestMoapPrimMediaClear()
        {

            int clearResult = api.llClearPrimMedia(0);
            Assert.AreEqual(clearResult, ScriptBaseClass.LSL_STATUS_OK);
        }

        [Test]
        public void TestMoapLinkMediaClearWithLinkThis()
        {
            // https://bugs.inworldz.com/mantis/view.php?id=3374
            int clearResult = api.llClearLinkMedia(ScriptBaseClass.LINK_THIS, 0);
            Assert.AreEqual(clearResult, ScriptBaseClass.LSL_STATUS_OK);
        }

        [Test]
        public void TestMoapGetPrimMediaParams()
        {
            // https://bugs.inworldz.com/mantis/view.php?id=3375
            LSLList details = new LSLList(new List<object> {
                ScriptBaseClass.PRIM_MEDIA_CONTROLS,
                ScriptBaseClass.PRIM_MEDIA_CURRENT_URL,
                ScriptBaseClass.PRIM_MEDIA_HOME_URL,
                ScriptBaseClass.PRIM_MEDIA_AUTO_LOOP,
                ScriptBaseClass.PRIM_MEDIA_AUTO_PLAY,
                ScriptBaseClass.PRIM_MEDIA_AUTO_SCALE,
                ScriptBaseClass.PRIM_MEDIA_AUTO_ZOOM,
                ScriptBaseClass.PRIM_MEDIA_WIDTH_PIXELS,
                ScriptBaseClass.PRIM_MEDIA_HEIGHT_PIXELS,
                ScriptBaseClass.PRIM_MEDIA_WHITELIST_ENABLE,
                ScriptBaseClass.PRIM_MEDIA_WHITELIST,
                ScriptBaseClass.PRIM_MEDIA_PERMS_INTERACT,
                ScriptBaseClass.PRIM_MEDIA_PERMS_CONTROL
            });

            int clearResult = api.llClearPrimMedia(0);
            Assert.AreEqual(clearResult, ScriptBaseClass.LSL_STATUS_OK);

            var setResult = api.llSetPrimMediaParams(0, new LSLList(
                new List<object> { ScriptBaseClass.PRIM_MEDIA_CURRENT_URL, "https://example.com" }));
            Assert.AreEqual(setResult, ScriptBaseClass.LSL_STATUS_OK);

            string expectedDefaults = "[0,https://example.com,,0,0,0,0,0,0,0,,7,0]";
            LSLList defaults = api.llGetPrimMediaParams(0, details);
            Assert.That(defaults.ToString(), Is.EqualTo(expectedDefaults));
        }

        [Test]
        public void TestMoapGetLinkMedia()
        {
            // https://bugs.inworldz.com/mantis/view.php?id=3375
            LSLList details = new LSLList(new List<object> {
                ScriptBaseClass.PRIM_MEDIA_CONTROLS,
                ScriptBaseClass.PRIM_MEDIA_CURRENT_URL,
                ScriptBaseClass.PRIM_MEDIA_HOME_URL,
                ScriptBaseClass.PRIM_MEDIA_AUTO_LOOP,
                ScriptBaseClass.PRIM_MEDIA_AUTO_PLAY,
                ScriptBaseClass.PRIM_MEDIA_AUTO_SCALE,
                ScriptBaseClass.PRIM_MEDIA_AUTO_ZOOM,
                ScriptBaseClass.PRIM_MEDIA_WIDTH_PIXELS,
                ScriptBaseClass.PRIM_MEDIA_HEIGHT_PIXELS,
                ScriptBaseClass.PRIM_MEDIA_WHITELIST_ENABLE,
                ScriptBaseClass.PRIM_MEDIA_WHITELIST,
                ScriptBaseClass.PRIM_MEDIA_PERMS_INTERACT,
                ScriptBaseClass.PRIM_MEDIA_PERMS_CONTROL
            });


            int clearResult = api.llClearPrimMedia(0);
            Assert.AreEqual(clearResult, ScriptBaseClass.LSL_STATUS_OK);

            var setResult = api.llSetPrimMediaParams(0, new LSLList(
                new List<object> { ScriptBaseClass.PRIM_MEDIA_CURRENT_URL, "https://example.com" }));
            Assert.AreEqual(setResult, ScriptBaseClass.LSL_STATUS_OK);

            string expectedDefaults = "[0,https://example.com,,0,0,0,0,0,0,0,,7,0]";
            LSLList defaults = api.llGetLinkMedia(ScriptBaseClass.LINK_THIS, 0, details);
            Assert.That(defaults.ToString(), Is.EqualTo(expectedDefaults));
        }
    }
}
