using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.Tests
{
    public class BroadcastArtTests
    {
        private const string Folder = "Assets/BikiniWipeout/Art/Broadcast/";

        [TestCase("LaunchGantry", "SplashCrestFace", "StartBoard")]
        [TestCase("FinishMarquee", "FinishMarqueeFace", "FinishBoard")]
        [TestCase("SpectatorDeck", "FrontFasciaFace", "GalleryBoard")]
        [TestCase("JumbotronFaces", "SouthFace", "StartBoard")]
        public void SignFaceUsesItsPrintedArtwork(string prop, string face, string print)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + prop + ".prefab");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + print + ".mat");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + print + ".png");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(material);
            Assert.IsNotNull(texture);
            Assert.AreSame(material, prefab.transform.Find(face).GetComponent<MeshRenderer>().sharedMaterial);
            Assert.AreSame(texture, material.GetTexture("_BaseMap"));
        }

        [Test]
        public void InflatablePadImportsWithUnitBounds()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "PaddedBlock.asset");
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 24);
            Assert.AreEqual(1, mesh.subMeshCount);
            Assert.Less((mesh.bounds.size - Vector3.one).sqrMagnitude, 0.00001f);
        }
    }
}
