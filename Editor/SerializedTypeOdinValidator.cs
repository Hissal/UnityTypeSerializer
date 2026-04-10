#if ODIN_VALIDATOR
using Sirenix.OdinInspector.Editor.Validation;

[assembly: RegisterValidator(typeof(Hissal.UnityTypeSerializer.Editor.SerializedTypeValueValidator))]
[assembly: RegisterValidator(typeof(Hissal.UnityTypeSerializer.Editor.SerializedTypeValueValidator<>))]

namespace Hissal.UnityTypeSerializer.Editor {
    internal sealed class SerializedTypeValueValidator : ValueValidator<SerializedType> {
        protected override void Validate(ValidationResult result) {
            var value = ValueEntry.SmartValue;
            var selectedType = value?.Type;
            var serializedAqn = value?.SerializedAqn ?? string.Empty;
            var options = Property.GetAttribute<SerializedTypeOptionsAttribute>();

            if (!SerializedTypeDrawerCore.TryValidateSelectedType(
                    selectedType,
                    serializedAqn,
                    typeof(object),
                    options,
                    Property,
                    out var errorMessage)
                && !string.IsNullOrEmpty(errorMessage)) {
                result.AddError(errorMessage);
            }
        }
    }

    internal sealed class SerializedTypeValueValidator<TBase> : ValueValidator<SerializedType<TBase>> where TBase : class {
        protected override void Validate(ValidationResult result) {
            var value = ValueEntry.SmartValue;
            var selectedType = value?.Type;
            var serializedAqn = value?.SerializedAqn ?? string.Empty;
            var options = Property.GetAttribute<SerializedTypeOptionsAttribute>();

            if (!SerializedTypeDrawerCore.TryValidateSelectedType(
                    selectedType,
                    serializedAqn,
                    typeof(TBase),
                    options,
                    Property,
                    out var errorMessage)
                && !string.IsNullOrEmpty(errorMessage)) {
                result.AddError(errorMessage);
            }
        }
    }
}
#endif


