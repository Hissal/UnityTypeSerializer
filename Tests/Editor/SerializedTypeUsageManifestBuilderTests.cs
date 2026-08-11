using NUnit.Framework;

namespace Hissal.UnityTypeSerializer.Editor.Tests {
    internal sealed class SerializedTypeUsageManifestBuilderTests {
        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        public void AutomaticRebuildDecisionRequiresMissingManifestOrInvalidation(
            bool manifestExists,
            bool isRebuildPending,
            bool expectedResult) {

            Assert.That(
                SerializedTypeUsageManifestBuilder.ShouldRebuildAutomatically(
                    manifestExists,
                    isRebuildPending),
                Is.EqualTo(expectedResult));
        }

        [Test]
        public void RepeatedUnrelatedDomainReloadsSkipFullRebuild() {
            var requestedRebuilds = 0;

            for (var i = 0; i < 1000; i++) {
                if (SerializedTypeUsageManifestBuilder.ShouldRebuildAutomatically(
                        manifestExists: true,
                        isRebuildPending: false)) {
                    requestedRebuilds++;
                }
            }

            Assert.That(requestedRebuilds, Is.Zero);
        }

        [TestCase("Assets/Scripts/Changed.cs", false, true)]
        [TestCase("Assets/Scripts/Runtime.asmdef", false, true)]
        [TestCase("Assets/Scripts/Runtime.asmref", false, true)]
        [TestCase("Assets/Plugins/Runtime.dll", false, true)]
        [TestCase("Assets/Scenes/Test.unity", false, false)]
        [TestCase("Assets/Data/Config.asset", false, false)]
        [TestCase("Assets/DeletedFolder", true, true)]
        [TestCase("Assets/ImportedFolder", false, false)]
        public void AssetInvalidationOnlyTracksCodeAssembliesAndRemovedFolders(
            string assetPath,
            bool includeExtensionlessPaths,
            bool expectedResult) {

            Assert.That(
                SerializedTypeUsageManifestAssetPostprocessor.IsRelevantAssetPath(
                    assetPath,
                    includeExtensionlessPaths),
                Is.EqualTo(expectedResult));
        }
    }
}
