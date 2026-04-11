using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hissal.UnityTypeSerializer {
	/// <summary>
	/// Runtime registry for stable SerializedType identifiers.
	/// Generated entries map id -> assembly qualified name.
	/// </summary>
	public static class SerializedTypeIdRegistry {
		static readonly Dictionary<string, string> s_idToAqn = CreateIdToAqnMap();
		static readonly Dictionary<string, Type?> s_resolvedTypeCache = new(StringComparer.Ordinal);
		static readonly Dictionary<Type, string> s_typeToIdCache = new();

		static Dictionary<string, string> CreateIdToAqnMap() {
			var map = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var provider in DiscoverRegistrationProviders()) {
				try {
					var snapshot = new Dictionary<string, string>(map, StringComparer.Ordinal);
					provider.Register(map);
					RestoreFirstWinsForConflicts(snapshot, map, provider.GetType());
				}
				catch (Exception exception) {
					LogRegistryError($"[SerializedType] Failed to run provider '{provider.GetType().FullName}': {exception}");
				}
			}

			return map;
		}

		static IEnumerable<ISerializedTypeIdRegistrationProvider> DiscoverRegistrationProviders() {
			var providerTypes = new List<Type>();

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName, StringComparer.Ordinal)) {
				foreach (var type in GetLoadableTypes(assembly)) {
					if (type == null || type.IsAbstract || type.IsInterface)
						continue;

					if (!typeof(ISerializedTypeIdRegistrationProvider).IsAssignableFrom(type))
						continue;

					providerTypes.Add(type);
				}
			}

			foreach (var providerType in providerTypes
				 .OrderBy(type => type.Assembly.FullName, StringComparer.Ordinal)
				 .ThenBy(type => type.FullName, StringComparer.Ordinal)) {
				ISerializedTypeIdRegistrationProvider? provider = null;

				try {
					provider = Activator.CreateInstance(providerType, nonPublic: true) as ISerializedTypeIdRegistrationProvider;
				}
				catch (Exception exception) {
					LogRegistryError($"[SerializedType] Failed to create provider '{providerType.FullName}': {exception}");
				}

				if (provider != null)
					yield return provider;
			}
		}

		static IEnumerable<Type?> GetLoadableTypes(Assembly assembly) {
			try {
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException reflectionTypeLoadException) {
				return reflectionTypeLoadException.Types;
			}
			catch {
				return Array.Empty<Type>();
			}
		}

		static void RestoreFirstWinsForConflicts(
			Dictionary<string, string> snapshot,
			Dictionary<string, string> map,
			Type providerType) {

			foreach (var existing in snapshot) {
				if (!map.TryGetValue(existing.Key, out var currentValue))
					continue;

				if (string.Equals(existing.Value, currentValue, StringComparison.Ordinal))
					continue;

				map[existing.Key] = existing.Value;
				LogRegistryError(
					$"[SerializedType] Duplicate SerializedTypeId '{existing.Key}' detected in provider '{providerType.FullName}'. " +
					$"Keeping first mapping '{existing.Value}' and ignoring '{currentValue}'.");
			}
		}

		static void LogRegistryError(string message) {
			#if UNITY_5_3_OR_NEWER
			UnityEngine.Debug.LogError(message);
			#else
			Console.Error.WriteLine(message);
			#endif
		}

		public static bool TryResolveType(string? serializedTypeId, out Type? type) {
			type = null;
			if (string.IsNullOrEmpty(serializedTypeId))
				return false;

			if (s_resolvedTypeCache.TryGetValue(serializedTypeId, out var cachedType)) {
				type = cachedType;
				return cachedType != null;
			}

			if (!s_idToAqn.TryGetValue(serializedTypeId, out var aqn) || string.IsNullOrEmpty(aqn)) {
				s_resolvedTypeCache[serializedTypeId] = null;
				return false;
			}

			type = Type.GetType(aqn);
			s_resolvedTypeCache[serializedTypeId] = type;
			return type != null;
		}

		public static bool TryGetAqn(string? serializedTypeId, out string aqn) {
			aqn = string.Empty;
			if (string.IsNullOrEmpty(serializedTypeId))
				return false;

			if (!s_idToAqn.TryGetValue(serializedTypeId, out var resolvedAqn) || string.IsNullOrEmpty(resolvedAqn))
				return false;

			aqn = resolvedAqn;
			return true;
		}

		public static bool TryGetTypeId(Type? type, out string serializedTypeId) {
			serializedTypeId = string.Empty;
			if (type == null)
				return false;

			if (s_typeToIdCache.TryGetValue(type, out var cachedId)) {
				serializedTypeId = cachedId;
				return !string.IsNullOrEmpty(cachedId);
			}

			var attribute = type.GetCustomAttribute<SerializedTypeIdAttribute>(false);
			if (attribute == null || string.IsNullOrWhiteSpace(attribute.Id)) {
				s_typeToIdCache[type] = string.Empty;
				return false;
			}

			serializedTypeId = attribute.Id.Trim();
			s_typeToIdCache[type] = serializedTypeId;
			return true;
		}
	}
}

