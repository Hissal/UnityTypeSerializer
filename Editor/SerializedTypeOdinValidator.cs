#if ODIN_VALIDATOR
using Sirenix.OdinInspector.Editor.Validation;

[assembly: RegisterValidator(typeof(Hissal.UnityTypeSerializer.Editor.SerializedTypeValueValidator))]
[assembly: RegisterValidator(typeof(Hissal.UnityTypeSerializer.Editor.SerializedTypeValueValidator<>))]

namespace Hissal.UnityTypeSerializer.Editor {
    internal sealed class SerializedTypeValueValidator : ValueValidator<SerializedType> {
        protected override void Validate(ValidationResult result) {
            var value = ValueEntry.SmartValue;
            var selectedType = value?.Type;
            var serializedTypeId = value?.SerializedTypeId ?? string.Empty;
            var serializedAqn = value?.SerializedAqn ?? string.Empty;
            var options = Property.GetAttribute<SerializedTypeOptionsAttribute>();

            if (!SerializedTypeDrawerCore.TryValidateSelectedType(
                    selectedType,
                    serializedTypeId,
                    serializedAqn,
                    typeof(object),
                    options,
                    Property,
                    out var errorMessage)
                && !string.IsNullOrEmpty(errorMessage)) {
                result.AddError(errorMessage);
                return;
            }

            if (SerializedTypeDrawerCore.TryGetObsoleteTypeWarning(selectedType, out var warningMessage)) {
                result.AddWarning(warningMessage);
            }

            var mismatches = value?.GetTypeTreeMismatches() ?? System.Array.Empty<SerializedTypeTreeMismatch>();
            for (int i = 0; i < mismatches.Length; i++) {
                AddMismatchWarningWithFixes(result, mismatches[i], () => ValueEntry.SmartValue);
            }
        }

        void AddMismatchWarningWithFixes(
            ValidationResult result,
            SerializedTypeTreeMismatch mismatch,
            System.Func<SerializedType?> getValue) {

            ref var warning = ref result.AddWarning(mismatch.WarningMessage);

            if (mismatch.CanMatchTypeId) {
                warning.WithFix($"Match TypeId ({mismatch.MatchTypeIdPreview})", () => ApplyFix(SerializedTypeTreeFixMode.MatchTypeId));

                if (mismatch.CanMatchAqn) {
                    warning.WithButton($"Match AQN ({mismatch.MatchAqnPreview})", () => ApplyFix(SerializedTypeTreeFixMode.MatchAqn));
                }

                return;
            }

            if (mismatch.CanMatchAqn) {
                warning.WithFix($"Match AQN ({mismatch.MatchAqnPreview})", () => ApplyFix(SerializedTypeTreeFixMode.MatchAqn));
            }

            return;

            void ApplyFix(SerializedTypeTreeFixMode fixMode) {
                var current = getValue();
                if (current == null)
                    return;

                if (current.TryApplyTypeTreeMismatchFix(mismatch.Path, fixMode, out _)) {
                    ValueEntry.ApplyChanges();
                }
            }
        }
    }

    internal sealed class SerializedTypeValueValidator<TBase> : ValueValidator<SerializedType<TBase>> where TBase : class {
        protected override void Validate(ValidationResult result) {
            var value = ValueEntry.SmartValue;
            var selectedType = value?.Type;
            var serializedTypeId = value?.SerializedTypeId ?? string.Empty;
            var serializedAqn = value?.SerializedAqn ?? string.Empty;
            var options = Property.GetAttribute<SerializedTypeOptionsAttribute>();

            if (!SerializedTypeDrawerCore.TryValidateSelectedType(
                    selectedType,
                    serializedTypeId,
                    serializedAqn,
                    typeof(TBase),
                    options,
                    Property,
                    out var errorMessage)
                && !string.IsNullOrEmpty(errorMessage)) {
                result.AddError(errorMessage);
                return;
            }

            if (SerializedTypeDrawerCore.TryGetObsoleteTypeWarning(selectedType, out var warningMessage)) {
                result.AddWarning(warningMessage);
            }

            var mismatches = value?.GetTypeTreeMismatches() ?? System.Array.Empty<SerializedTypeTreeMismatch>();
            for (int i = 0; i < mismatches.Length; i++) {
                AddMismatchWarningWithFixes(result, mismatches[i], () => ValueEntry.SmartValue);
            }
        }

        void AddMismatchWarningWithFixes(
            ValidationResult result,
            SerializedTypeTreeMismatch mismatch,
            System.Func<SerializedType<TBase>?> getValue) {

            ref var warning = ref result.AddWarning(mismatch.WarningMessage);

            if (mismatch.CanMatchTypeId) {
                warning.WithFix($"Match TypeId ({mismatch.MatchTypeIdPreview})", () => ApplyFix(SerializedTypeTreeFixMode.MatchTypeId));

                if (mismatch.CanMatchAqn) {
                    warning.WithButton($"Match AQN ({mismatch.MatchAqnPreview})", () => ApplyFix(SerializedTypeTreeFixMode.MatchAqn));
                }

                return;
            }

            if (mismatch.CanMatchAqn) {
                warning.WithFix($"Match AQN ({mismatch.MatchAqnPreview})", () => ApplyFix(SerializedTypeTreeFixMode.MatchAqn));
            }

            return;

            void ApplyFix(SerializedTypeTreeFixMode fixMode) {
                var current = getValue();
                if (current == null)
                    return;

                if (current.TryApplyTypeTreeMismatchFix(mismatch.Path, fixMode, out _)) {
                    ValueEntry.ApplyChanges();
                }
            }
        }
    }
}
#endif


