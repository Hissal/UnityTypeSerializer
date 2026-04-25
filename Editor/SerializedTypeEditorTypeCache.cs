using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Hissal.UnityTypeSerializer.Editor {
    /// <summary>
    /// Centralized editor type discovery wrapper. Prefer Unity's <see cref="TypeCache"/> when it can answer
    /// the query directly, and use cached AppDomain scans for field-type and assembly relationship queries.
    /// </summary>
    internal static class SerializedTypeEditorTypeCache {
        static Assembly[]? s_loadableDomainAssemblies;
        static Type[]? s_loadableDomainTypes;
        static Assembly[]? s_runtimeDependentAssemblies;
        static Type[]? s_runtimeDependentTypes;

        public static IEnumerable<Type> GetTypesDerivedFrom(Type baseType) {
            return TypeCache.GetTypesDerivedFrom(baseType);
        }

        public static IReadOnlyList<Type> GetTypesWithAttribute<TAttribute>() where TAttribute : Attribute {
            return TypeCache.GetTypesWithAttribute<TAttribute>().ToArray();
        }

        public static IReadOnlyList<FieldInfo> GetFieldsWithAttribute<TAttribute>() where TAttribute : Attribute {
            return TypeCache.GetFieldsWithAttribute<TAttribute>().ToArray();
        }

        public static IReadOnlyList<Type> GetLoadableDomainTypes() {
            return s_loadableDomainTypes ??= GetLoadableDomainAssemblies()
                .SelectMany(GetLoadableTypes)
                .OrderBy(type => type.Assembly.FullName, StringComparer.Ordinal)
                .ThenBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<Assembly> GetRuntimeDependentAssemblies() {
            return s_runtimeDependentAssemblies ??= BuildRuntimeDependentAssemblies();
        }

        public static IReadOnlyList<Type> GetRuntimeDependentTypes() {
            return s_runtimeDependentTypes ??= GetRuntimeDependentAssemblies()
                .SelectMany(GetLoadableTypes)
                .OrderBy(type => type.Assembly.FullName, StringComparer.Ordinal)
                .ThenBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        public static Type? FindTypeByName(string typeName) {
            return GetLoadableDomainTypes()
                .FirstOrDefault(type => type.Name == typeName || type.FullName == typeName);
        }

        static Assembly[] GetLoadableDomainAssemblies() {
            return s_loadableDomainAssemblies ??= AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        static Assembly[] BuildRuntimeDependentAssemblies() {
            var assemblies = GetLoadableDomainAssemblies();
            var includedAssemblyNames = new HashSet<string>(StringComparer.Ordinal);
            var includedAssemblies = new List<Assembly>();
            var runtimeAssemblyName = typeof(SerializedType).Assembly.GetName().Name;

            if (runtimeAssemblyName == null)
                return Array.Empty<Assembly>();

            var changed = true;
            while (changed) {
                changed = false;

                foreach (var assembly in assemblies) {
                    var assemblyName = assembly.GetName().Name;
                    if (string.IsNullOrEmpty(assemblyName) || includedAssemblyNames.Contains(assemblyName))
                        continue;

                    if (!IsRuntimeAssemblyOrReferencesIncludedAssembly(assembly, runtimeAssemblyName, includedAssemblyNames))
                        continue;

                    includedAssemblyNames.Add(assemblyName);
                    includedAssemblies.Add(assembly);
                    changed = true;
                }
            }

            return includedAssemblies
                .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        static bool IsRuntimeAssemblyOrReferencesIncludedAssembly(
            Assembly assembly,
            string runtimeAssemblyName,
            HashSet<string> includedAssemblyNames) {

            var assemblyName = assembly.GetName().Name;
            if (string.Equals(assemblyName, runtimeAssemblyName, StringComparison.Ordinal))
                return true;

            AssemblyName[] referencedAssemblies;
            try {
                referencedAssemblies = assembly.GetReferencedAssemblies();
            }
            catch {
                return false;
            }

            foreach (var referencedAssembly in referencedAssemblies) {
                if (string.Equals(referencedAssembly.Name, runtimeAssemblyName, StringComparison.Ordinal))
                    return true;

                if (!string.IsNullOrEmpty(referencedAssembly.Name) &&
                    includedAssemblyNames.Contains(referencedAssembly.Name)) {
                    return true;
                }
            }

            return false;
        }

        static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
            try {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException reflectionTypeLoadException) {
                return reflectionTypeLoadException.Types
                    .Where(type => type != null)
                    .Cast<Type>();
            }
            catch {
                return Array.Empty<Type>();
            }
        }
    }
}
