#if ODIN_VALIDATOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector.Editor.Validation;

[assembly: RegisterValidator(typeof(Hissal.UnityTypeSerializer.Editor.SerializedTypeIdUniquenessValidator))]

namespace Hissal.UnityTypeSerializer.Editor {
    internal sealed class SerializedTypeIdUniquenessValidator : GlobalValidator {
        public override IEnumerable RunValidation(ValidationResult result) {
            var typeIdToTypes = CollectTypeIds();
            ReportDuplicateIds(result, typeIdToTypes);

            yield break;
        }

        static Dictionary<string, List<Type>> CollectTypeIds() {
            var typeIdToTypes = new Dictionary<string, List<Type>>(StringComparer.Ordinal);

            foreach (var type in SerializedTypeEditorTypeCache.GetTypesWithAttribute<SerializedTypeIdAttribute>()) {
                TryAddTypeId(typeIdToTypes, type);
            }

            return typeIdToTypes;
        }

        static void TryAddTypeId(Dictionary<string, List<Type>> typeIdToTypes, Type? type) {
            if (type == null)
                return;

            var typeIdAttribute = type.GetCustomAttribute<SerializedTypeIdAttribute>(false);
            if (typeIdAttribute == null)
                return;

            var typeId = typeIdAttribute.Id.Trim();
            if (string.IsNullOrEmpty(typeId))
                return;

            if (!typeIdToTypes.TryGetValue(typeId, out var typesWithId)) {
                typesWithId = new List<Type>();
                typeIdToTypes[typeId] = typesWithId;
            }

            typesWithId.Add(type);
        }

        static void ReportDuplicateIds(ValidationResult result, Dictionary<string, List<Type>> typeIdToTypes) {
            foreach (var entry in typeIdToTypes.OrderBy(pair => pair.Key, StringComparer.Ordinal)) {
                if (entry.Value.Count <= 1)
                    continue;

                var conflictingTypes = entry.Value
                    .OrderBy(type => type.Assembly.FullName, StringComparer.Ordinal)
                    .ThenBy(type => type.FullName, StringComparer.Ordinal)
                    .Select(type => $"{type.FullName} ({type.Assembly.GetName().Name})")
                    .ToArray();

                result.AddError(
                    $"Duplicate SerializedTypeId '{entry.Key}' found on multiple types: {string.Join(", ", conflictingTypes)}");
            }
        }
    }
}
#endif
