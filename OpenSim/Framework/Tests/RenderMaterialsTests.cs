using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using OpenSim.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class RenderMaterialsTests
    {
        [TestFixtureSetUp]
        public void Init()
        {
        }

        [Test]
        public void T000_OSDFromToTest()
        {
            RenderMaterial mat = new RenderMaterial ();
            OSD map = mat.GetOSD ();
            RenderMaterial matFromOSD = RenderMaterial.FromOSD (map);
            Assert.That (mat, Is.EqualTo (matFromOSD));
            Assert.That (matFromOSD.NormalID, Is.EqualTo (UUID.Zero));
            Assert.That (matFromOSD.NormalOffsetX, Is.EqualTo (0.0f));
            Assert.That (matFromOSD.NormalOffsetY, Is.EqualTo(0.0f));
            Assert.That (matFromOSD.NormalRepeatX, Is.EqualTo(1.0f));
            Assert.That (matFromOSD.NormalRepeatY, Is.EqualTo(1.0f));
            Assert.That (matFromOSD.NormalRotation, Is.EqualTo(0.0f));
            Assert.That (matFromOSD.SpecularOffsetX, Is.EqualTo(0.0f));
            Assert.That (matFromOSD.SpecularOffsetY, Is.EqualTo(0.0f));
            Assert.That (matFromOSD.SpecularRepeatX, Is.EqualTo(1.0f));
            Assert.That (matFromOSD.SpecularRepeatY, Is.EqualTo(1.0f));
            Assert.That (matFromOSD.SpecularRotation, Is.EqualTo(0.0f));
            Assert.That (matFromOSD.SpecularLightColor, Is.EqualTo(RenderMaterial.DEFAULT_SPECULAR_LIGHT_COLOR));
            Assert.That (matFromOSD.SpecularLightExponent, Is.EqualTo(RenderMaterial.DEFAULT_SPECULAR_LIGHT_EXPONENT));
            Assert.That (matFromOSD.EnvironmentIntensity, Is.EqualTo(RenderMaterial.DEFAULT_ENV_INTENSITY));
            Assert.That (matFromOSD.DiffuseAlphaMode, Is.EqualTo((byte)RenderMaterial.eDiffuseAlphaMode.DIFFUSE_ALPHA_MODE_BLEND));
            Assert.That (matFromOSD.AlphaMaskCutoff, Is.EqualTo(0));
        }

		[Test]
		public void T001_ToFromBinaryTest()
		{
			RenderMaterial mat = new RenderMaterial ();
			RenderMaterials mats = new RenderMaterials ();
            String key = UUID.Random().ToString();
            mats.Materials.Add(key, mat);

			byte[] bytes = mats.ToBytes ();
            RenderMaterials newmats = RenderMaterials.FromBytes(bytes, 0);
            RenderMaterial newmat = newmats.Materials[key];
			Assert.That (mat, Is.EqualTo(newmat));
		}
    }
}

