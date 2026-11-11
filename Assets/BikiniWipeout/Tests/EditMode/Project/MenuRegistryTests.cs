using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    public class MenuRegistryTests
    {
        [Test]
        public void CourseRequiresExplicitReadinessAndScene()
        {
            var entry = new CourseEntry { ready = false, sceneName = SceneNames.CourseGauntlet };
            Assert.IsFalse(entry.Available);
            entry.ready = true;
            entry.sceneName = "";
            Assert.IsFalse(entry.Available);
            entry.sceneName = "__missing_splashdown_scene__";
            Assert.IsFalse(entry.Available);
        }
    }
}
