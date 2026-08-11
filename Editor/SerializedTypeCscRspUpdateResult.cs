using System.Collections.Generic;

namespace Hissal.UnityTypeSerializer.Editor {
    internal sealed class SerializedTypeCscRspUpdateResult {
        readonly List<string> failures = new();

        public int CreatedCount { get; private set; }
        public int UpdatedCount { get; private set; }
        public int UnchangedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int FailedCount => failures.Count;
        public bool HasChanges => CreatedCount > 0 || UpdatedCount > 0;
        public IReadOnlyList<string> Failures => failures;

        public string Summary =>
            $"Created: {CreatedCount}, updated: {UpdatedCount}, unchanged: {UnchangedCount}, " +
            $"skipped: {SkippedCount}, failed: {FailedCount}.";

        internal void AddCreated() {
            CreatedCount++;
        }

        internal void AddUpdated() {
            UpdatedCount++;
        }

        internal void AddUnchanged() {
            UnchangedCount++;
        }

        internal void AddSkipped() {
            SkippedCount++;
        }

        internal void AddFailure(string failure) {
            failures.Add(failure);
        }
    }
}
