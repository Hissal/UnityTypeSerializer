using System;
using System.IO;
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

        [Test]
        public void AnalyzerManifestIsNotCreatedBeforeCscSetupOptsIn() {
            var temporaryDirectory = CreateTemporaryDirectory();
            try {
                var sourceManifestPath = Path.Combine(temporaryDirectory, "LibraryManifest.xml");
                var analyzerManifestPath = Path.Combine(temporaryDirectory, "AnalyzerManifest.xml");
                File.WriteAllText(sourceManifestPath, "canonical");

                var hasChanges = SerializedTypeUsageManifestBuilder.SynchronizeAnalyzerManifestIfPresent(
                    sourceManifestPath,
                    analyzerManifestPath);

                Assert.That(hasChanges, Is.False);
                Assert.That(File.Exists(analyzerManifestPath), Is.False);
            }
            finally {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void ExistingAnalyzerManifestTracksCanonicalManifest() {
            var temporaryDirectory = CreateTemporaryDirectory();
            try {
                var sourceManifestPath = Path.Combine(temporaryDirectory, "LibraryManifest.xml");
                var analyzerManifestPath = Path.Combine(temporaryDirectory, "AnalyzerManifest.xml");
                File.WriteAllText(sourceManifestPath, "canonical");
                File.WriteAllText(analyzerManifestPath, "stale");

                var firstHasChanges = SerializedTypeUsageManifestBuilder.SynchronizeAnalyzerManifestIfPresent(
                    sourceManifestPath,
                    analyzerManifestPath);
                var secondHasChanges = SerializedTypeUsageManifestBuilder.SynchronizeAnalyzerManifestIfPresent(
                    sourceManifestPath,
                    analyzerManifestPath);

                Assert.That(firstHasChanges, Is.True);
                Assert.That(secondHasChanges, Is.False);
                Assert.That(File.ReadAllText(analyzerManifestPath), Is.EqualTo("canonical"));
            }
            finally {
                Directory.Delete(temporaryDirectory, true);
            }
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

        static string CreateTemporaryDirectory() {
            var path = Path.Combine(
                Path.GetTempPath(),
                "UnityTypeSerializerTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
