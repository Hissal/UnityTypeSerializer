using System;
using System.Collections.Generic;
using System.Linq;
using Hissal.TypeSerializer;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Editor {
    /// <summary>
    /// Inline drawer for TypeRef that displays everything on a single line with multiple dropdowns.
    /// This is the default, simpler mode for TypeRef fields.
    /// </summary>
    internal sealed class InlineTypeRefDrawer<TBase> : TypeRefDrawerBase<TBase>, ITypeRefDrawerImplementation 
        where TBase : class {
        
        readonly List<GenericSelectorItem<Type>> dropdownItems;
        
        // Track construction state for multi-parameter generics
        // Maps from generic definition to array of selected argument types
        readonly Dictionary<Type, Type?[]> constructionState = new Dictionary<Type, Type?[]>();
        
        // Track the last type we rebuilt construction state from
        Type? lastRebuiltType = null;
        
        public InlineTypeRefDrawer(
            InspectorProperty property,
            PropertyValueEntry<TypeRef<TBase>> valueEntry,
            TypeRefOptionsAttribute? options,
            List<Type> availableTypes) 
            : base(property, valueEntry, options, availableTypes) {
            
            // Build dropdown items
            dropdownItems = new List<GenericSelectorItem<Type>>();
            foreach (var type in availableTypes) {
                dropdownItems.Add(new GenericSelectorItem<Type>(GetTypeName(type), type));
            }
        }
        
        public void DrawPropertyLayout(GUIContent label) {
            var currentType = ValueEntry.SmartValue?.Type;
            
            // Only rebuild construction state if the stored type actually changed
            // This preserves partial selections for open generics
            if (currentType != lastRebuiltType) {
                RebuildConstructionState(currentType);
                lastRebuiltType = currentType;
            }
            
            // Validate the current type
            string? errorMessage;
            bool isValid = ValidateType(currentType, out errorMessage);
            
            // Draw error message if invalid
            if (!isValid && !string.IsNullOrEmpty(errorMessage)) {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
            }
            
            // Draw the inline type selector
            EditorGUILayout.BeginHorizontal();
            
            var rect = EditorGUILayout.GetControlRect(true, GUILayout.ExpandWidth(true));
            rect = EditorGUI.PrefixLabel(rect, label);
            
            DrawInlineTypeSelector(rect, currentType);
            
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// Rebuilds construction state from the current type.
        /// Called only when the stored type changes to ensure construction state matches the type.
        /// Preserves partial selections for open generics by not rebuilding unnecessarily.
        /// </summary>
        void RebuildConstructionState(Type? currentType) {
            constructionState.Clear();
            
            if (currentType == null) {
                return;
            }
            
            RebuildConstructionStateRecursive(currentType);
        }
        
        void RebuildConstructionStateRecursive(Type type) {
            // Early exit for non-generic types
            if (!type.IsGenericType && !type.IsGenericTypeDefinition) {
                return;
            }
            
            if (type.IsGenericTypeDefinition) {
                // Open generic - initialize empty state
                var genericParams = type.GetGenericArguments();
                constructionState[type] = new Type?[genericParams.Length];
            } else if (type.IsGenericType) {
                // Constructed generic - extract arguments and store them
                var genericDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();
                
                constructionState[genericDef] = new Type?[genericArgs.Length];
                for (int i = 0; i < genericArgs.Length; i++) {
                    constructionState[genericDef][i] = genericArgs[i];
                    
                    // Recurse into nested generics
                    RebuildConstructionStateRecursive(genericArgs[i]);
                }
            }
        }
        
        void DrawInlineTypeSelector(Rect rect, Type? currentType) {
            bool allowGenericTypeConstruction = Options?.AllowGenericTypeConstruction ?? false;
            bool allowOpenGenerics = Options?.AllowOpenGenerics ?? false;
            
            if (currentType == null) {
                // No type selected - show dropdown for base type
                DrawBaseTypeDropdown(rect, currentType);
                return;
            }
            
            // If it's a non-generic type, just show it
            if (!currentType.IsGenericType && !currentType.IsGenericTypeDefinition) {
                DrawBaseTypeDropdown(rect, currentType);
                return;
            }
            
            // If AllowGenericTypeConstruction is false, just show the type as-is
            if (!allowGenericTypeConstruction) {
                DrawBaseTypeDropdown(rect, currentType);
                return;
            }
            
            // At this point we have a generic type and construction is allowed
            // Draw inline construction UI
            DrawInlineGenericConstruction(rect, currentType);
        }
        
        void DrawBaseTypeDropdown(Rect rect, Type? currentType) {
            var displayName = currentType != null ? GetTypeName(currentType) : "None";
            
            if (EditorGUI.DropdownButton(rect, new GUIContent(displayName), FocusType.Keyboard)) {
                if (dropdownItems == null)
                    return;
                    
                var selector = new GenericSelector<Type>("Select Type", false, dropdownItems);
                selector.SelectionConfirmed += selection => {
                    var selectedType = selection.FirstOrDefault();
                    ValueEntry.SmartValue = new TypeRef<TBase> { Type = selectedType };
                    ValueEntry.ApplyChanges();
                };
                selector.ShowInPopup(rect.position);
            }
        }
        
        void DrawInlineGenericConstruction(Rect rect, Type currentType) {
            // Build a list of all dropdowns to render (recursively)
            var dropdowns = BuildDropdownList(currentType);
            
            if (dropdowns.Count == 0) {
                // Fallback to simple dropdown
                DrawBaseTypeDropdown(rect, currentType);
                return;
            }
            
            // Calculate widths for each dropdown
            float totalWidth = rect.width;
            float dropdownSpacing = 2f;
            float availableWidth = totalWidth - (dropdowns.Count - 1) * dropdownSpacing;
            float dropdownWidth = availableWidth / dropdowns.Count;
            
            float currentX = rect.x;
            
            for (int i = 0; i < dropdowns.Count; i++) {
                var dropdown = dropdowns[i];
                float width = dropdownWidth;
                
                if (i == dropdowns.Count - 1) {
                    // Last dropdown - use remaining width
                    width = rect.x + rect.width - currentX;
                }
                
                var dropdownRect = new Rect(currentX, rect.y, width - dropdownSpacing, rect.height);
                DrawDropdown(dropdownRect, dropdown);
                
                currentX += width;
            }
        }
        
        List<DropdownInfo> BuildDropdownList(Type currentType) {
            var dropdowns = new List<DropdownInfo>();
            var visited = new HashSet<string>(); // Track visited type+path to prevent infinite recursion
            BuildDropdownListRecursive(currentType, dropdowns, new List<int>(), visited, 0);
            return dropdowns;
        }
        
        void BuildDropdownListRecursive(Type type, List<DropdownInfo> dropdowns, List<int> path, HashSet<string> visited, int depth) {
            // Prevent infinite recursion
            const int MAX_DEPTH = 10;
            if (depth > MAX_DEPTH) {
                return;
            }
            
            // Create a unique key for this type+path combination
            var pathKey = string.Join("/", path);
            var visitKey = $"{type.AssemblyQualifiedName}@{pathKey}";
            
            if (visited.Contains(visitKey)) {
                // Already visited this type at this path - stop to prevent cycles
                return;
            }
            visited.Add(visitKey);
            
            if (type.IsGenericTypeDefinition) {
                // Open generic - need to show the base type and placeholder for each arg
                var genericParams = type.GetGenericArguments();
                
                // Add base type dropdown
                dropdowns.Add(new DropdownInfo {
                    Type = type,
                    IsBaseType = true,
                    Path = new List<int>(path),
                    GenericDefinition = type
                });
                
                // Check if we have construction state for this type
                Type?[]? stateArgs = null;
                if (constructionState.TryGetValue(type, out var stored)) {
                    stateArgs = stored;
                }
                
                // Add dropdown for each generic parameter
                for (int i = 0; i < genericParams.Length; i++) {
                    var argPath = new List<int>(path) { i };
                    var selectedType = stateArgs != null && i < stateArgs.Length ? stateArgs[i] : null;
                    
                    if (selectedType != null) {
                        // We have a selection for this parameter
                        if (selectedType.IsGenericType || selectedType.IsGenericTypeDefinition) {
                            // Nested generic - recurse
                            BuildDropdownListRecursive(selectedType, dropdowns, argPath, visited, depth + 1);
                        } else {
                            // Concrete type
                            dropdowns.Add(new DropdownInfo {
                                Type = selectedType,
                                IsGenericArgument = true,
                                ArgumentIndex = i,
                                Path = argPath,
                                GenericDefinition = type
                            });
                        }
                    } else {
                        // No selection yet - show placeholder
                        dropdowns.Add(new DropdownInfo {
                            Type = null,
                            IsGenericArgument = true,
                            ArgumentIndex = i,
                            Path = argPath,
                            GenericDefinition = type,
                            GenericParameter = genericParams[i]
                        });
                    }
                }
            } else if (type.IsGenericType) {
                // Constructed generic - show base type and recurse for each argument
                var genericDefinition = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();
                
                // DON'T modify construction state during rendering - only read it
                // Construction state should only be updated during type updates, not during UI rendering
                
                // Add base type dropdown
                dropdowns.Add(new DropdownInfo {
                    Type = genericDefinition,
                    IsBaseType = true,
                    Path = new List<int>(path),
                    GenericDefinition = genericDefinition,
                    ConstructedType = type
                });
                
                // Recursively process each generic argument
                for (int i = 0; i < genericArgs.Length; i++) {
                    var arg = genericArgs[i];
                    var argPath = new List<int>(path) { i };
                    
                    if (arg.IsGenericParameter) {
                        // Unresolved generic parameter - show placeholder
                        dropdowns.Add(new DropdownInfo {
                            Type = null,
                            IsGenericArgument = true,
                            ArgumentIndex = i,
                            Path = argPath,
                            GenericDefinition = genericDefinition,
                            GenericParameter = arg
                        });
                    } else if (arg.IsGenericType || arg.IsGenericTypeDefinition) {
                        // Nested generic - recurse
                        BuildDropdownListRecursive(arg, dropdowns, argPath, visited, depth + 1);
                    } else {
                        // Concrete type argument
                        dropdowns.Add(new DropdownInfo {
                            Type = arg,
                            IsGenericArgument = true,
                            ArgumentIndex = i,
                            Path = argPath,
                            GenericDefinition = genericDefinition
                        });
                    }
                }
            } else {
                // Concrete type
                dropdowns.Add(new DropdownInfo {
                    Type = type,
                    IsBaseType = true,
                    Path = new List<int>(path)
                });
            }
        }
        
        void DrawDropdown(Rect rect, DropdownInfo info) {
            // Visual de-emphasis for generic arguments
            var oldFontSize = EditorStyles.popup.fontSize;
            if (info.IsGenericArgument) {
                EditorStyles.popup.fontSize = Mathf.Max(9, oldFontSize - 1);
            }
            
            string displayName;
            if (info.Type == null && info.GenericParameter != null) {
                displayName = $"<{info.GenericParameter.Name}>";
            } else if (info.Type != null) {
                displayName = GetTypeName(info.Type);
            } else {
                displayName = "?";
            }
            
            if (EditorGUI.DropdownButton(rect, new GUIContent(displayName), FocusType.Keyboard)) {
                if (info.IsBaseType) {
                    ShowBaseTypeSelector(info);
                } else if (info.IsGenericArgument) {
                    ShowGenericArgumentSelector(info);
                }
            }
            
            EditorStyles.popup.fontSize = oldFontSize;
        }
        
        void ShowBaseTypeSelector(DropdownInfo info) {
            List<GenericSelectorItem<Type>> items;

            if (info.Path.Count > 0) {
                var currentType = ValueEntry.SmartValue?.Type;
                var genericParam = currentType != null ? GetGenericParameterAtPath(currentType, info.Path) : null;
                var constraints = genericParam?.GetGenericParameterConstraints() ?? Array.Empty<Type>();
                var validTypes = BuildValidTypesForGenericParameter(constraints, info.Path);
                items = validTypes.Select(t => new GenericSelectorItem<Type>(GetTypeName(t), t)).ToList();
            } else {
                items = dropdownItems;
            }

            var selector = new GenericSelector<Type>("Select Type", false, items);
            selector.SelectionConfirmed += selection => {
                var selectedType = selection.FirstOrDefault();
                if (selectedType != null) {
                    if (info.Path.Count == 0) {
                        // Root level base type - replace entire type
                        constructionState.Clear();
                        ValueEntry.SmartValue = new TypeRef<TBase> { Type = selectedType };
                        ValueEntry.ApplyChanges();
                    } else {
                        // Nested base type - update at path
                        UpdateGenericArgumentAtPath(info.Path, selectedType);
                    }
                }
            };
            selector.ShowInPopup();
        }

        Type? GetGenericParameterAtPath(Type rootType, List<int> path) {
            if (path.Count == 0) {
                return null;
            }

            var currentType = BuildTypeFromConstructionState(rootType);

            for (int i = 0; i < path.Count; i++) {
                if (!currentType.IsGenericType && !currentType.IsGenericTypeDefinition) {
                    return null;
                }

                var genericDef = currentType.IsGenericTypeDefinition
                    ? currentType
                    : currentType.GetGenericTypeDefinition();
                var genericParams = genericDef.GetGenericArguments();
                int argIndex = path[i];

                if (argIndex < 0 || argIndex >= genericParams.Length) {
                    return null;
                }

                if (i == path.Count - 1) {
                    return genericParams[argIndex];
                }

                if (currentType.IsGenericTypeDefinition) {
                    if (!constructionState.TryGetValue(genericDef, out var stateArgs)) {
                        return null;
                    }

                    var nextType = stateArgs[argIndex];
                    if (nextType == null) {
                        return null;
                    }

                    currentType = nextType;
                } else {
                    var args = currentType.GetGenericArguments();
                    currentType = args[argIndex];
                }
            }

            return null;
        }

        void ShowGenericArgumentSelector(DropdownInfo info) {
            if (info.GenericDefinition == null || !info.ArgumentIndex.HasValue)
                return;
            
            var genericParams = info.GenericDefinition.GetGenericArguments();
            if (info.ArgumentIndex.Value >= genericParams.Length)
                return;
            
            var genericParam = info.GenericParameter ?? genericParams[info.ArgumentIndex.Value];
            var constraints = genericParam.GetGenericParameterConstraints();
            
            // Build list of valid types for this generic parameter
            var validTypes = BuildValidTypesForGenericParameter(constraints, info.Path);
            
            var items = validTypes.Select(t => new GenericSelectorItem<Type>(GetTypeName(t), t)).ToList();
            
            var selector = new GenericSelector<Type>($"Select {genericParam.Name}", false, items);
            selector.SelectionConfirmed += selection => {
                var selectedType = selection.FirstOrDefault();
                if (selectedType != null) {
                    UpdateGenericArgumentAtPath(info.Path, selectedType);
                }
            };
            selector.ShowInPopup();
        }
        
        void UpdateGenericArgumentAtPath(List<int> path, Type newArgumentType) {
            var currentType = ValueEntry.SmartValue?.Type;
            if (currentType == null || path.Count == 0)
                return;
            
            // Handle root-level path (direct child of current type)
            if (path.Count == 1) {
                int argIndex = path[0];
                
                if (currentType.IsGenericTypeDefinition) {
                    // Open generic - update construction state
                    var genericParams = currentType.GetGenericArguments();
                    if (argIndex >= genericParams.Length)
                        return;
                    
                    // Get or create construction state
                    if (!constructionState.TryGetValue(currentType, out var args)) {
                        args = new Type?[genericParams.Length];
                        constructionState[currentType] = args;
                    }
                    
                    args[argIndex] = newArgumentType;
                    
                    // Try to construct the type if we have all arguments
                    bool allowOpenGenerics = Options?.AllowOpenGenerics ?? false;
                    bool allSelected = args.All(a => a != null);
                    
                    if (allSelected) {
                        // All arguments selected - construct the type
                        try {
                            var constructedType = currentType.MakeGenericType(args.Cast<Type>().ToArray());
                            ValueEntry.SmartValue = new TypeRef<TBase> { Type = constructedType };
                            ValueEntry.ApplyChanges();
                        } catch {
                            // Construction failed - keep as open generic
                        }
                    }
                } else if (currentType.IsGenericType) {
                    // Already constructed - directly update
                    var genericDefinition = currentType.GetGenericTypeDefinition();
                    var currentArgs = currentType.GetGenericArguments();
                    
                    if (argIndex >= currentArgs.Length)
                        return;
                    
                    var newArgs = new Type[currentArgs.Length];
                    for (int i = 0; i < newArgs.Length; i++) {
                        newArgs[i] = i == argIndex ? newArgumentType : currentArgs[i];
                    }
                    
                    try {
                        var constructedType = genericDefinition.MakeGenericType(newArgs);
                        // Also update construction state
                        constructionState[genericDefinition] = newArgs;
                        ValueEntry.SmartValue = new TypeRef<TBase> { Type = constructedType };
                        ValueEntry.ApplyChanges();
                    } catch {
                        // Construction failed
                    }
                }
            } else {
                // Nested path - need to update construction state for nested type
                int firstIndex = path[0];
                var remainingPath = path.Skip(1).ToList();
                
                if (currentType.IsGenericTypeDefinition) {
                    // Open generic - get or create construction state
                    var genericParams = currentType.GetGenericArguments();
                    if (firstIndex >= genericParams.Length)
                        return;
                    
                    if (!constructionState.TryGetValue(currentType, out var args)) {
                        args = new Type?[genericParams.Length];
                        constructionState[currentType] = args;
                    }
                    
                    var nestedType = args[firstIndex];
                    if (nestedType == null) {
                        // No nested type yet - can't navigate further
                        return;
                    }
                    
                    // Recursively update the nested type
                    var updatedNestedType = UpdateTypeAtPathRecursive(nestedType, remainingPath, newArgumentType);
                    if (updatedNestedType != null) {
                        args[firstIndex] = updatedNestedType;
                        
                        // Try to construct if all args are selected
                        bool allSelected = args.All(a => a != null);
                        if (allSelected) {
                            try {
                                var constructedType = currentType.MakeGenericType(args.Cast<Type>().ToArray());
                                ValueEntry.SmartValue = new TypeRef<TBase> { Type = constructedType };
                                ValueEntry.ApplyChanges();
                            } catch {
                                // Construction failed
                            }
                        }
                    }
                } else if (currentType.IsGenericType) {
                    // Already constructed - update recursively
                    var updatedType = UpdateTypeAtPathRecursive(currentType, path, newArgumentType);
                    if (updatedType != null) {
                        ValueEntry.SmartValue = new TypeRef<TBase> { Type = updatedType };
                        ValueEntry.ApplyChanges();
                    }
                }
            }
        }
        
        Type? UpdateTypeAtPathRecursive(Type currentType, List<int> path, Type newArgumentType) {
            if (path.Count == 0)
                return newArgumentType;
            
            int argIndex = path[0];
            var remainingPath = path.Skip(1).ToList();
            
            if (currentType.IsGenericTypeDefinition) {
                // Open generic - we need to construct it
                var genericParams = currentType.GetGenericArguments();
                if (argIndex >= genericParams.Length)
                    return currentType;
                
                // Get or create construction state
                if (!constructionState.TryGetValue(currentType, out var stateArgs)) {
                    stateArgs = new Type?[genericParams.Length];
                    constructionState[currentType] = stateArgs;
                }
                
                var newArgs = new Type?[genericParams.Length];
                for (int i = 0; i < newArgs.Length; i++) {
                    if (i == argIndex) {
                        if (remainingPath.Count > 0 && stateArgs[i] != null) {
                            newArgs[i] = UpdateTypeAtPathRecursive(stateArgs[i]!, remainingPath, newArgumentType);
                        } else {
                            newArgs[i] = newArgumentType;
                        }
                    } else {
                        newArgs[i] = stateArgs[i];
                    }
                }
                
                // Update construction state
                constructionState[currentType] = newArgs;
                
                // Try to construct if all args are present
                if (newArgs.All(a => a != null)) {
                    try {
                        return currentType.MakeGenericType(newArgs.Cast<Type>().ToArray());
                    } catch {
                        return currentType;
                    }
                }
                return currentType;
            } else if (currentType.IsGenericType) {
                var genericDefinition = currentType.GetGenericTypeDefinition();
                var currentArgs = currentType.GetGenericArguments();
                
                if (argIndex >= currentArgs.Length)
                    return currentType;
                
                var newArgs = new Type[currentArgs.Length];
                for (int i = 0; i < newArgs.Length; i++) {
                    if (i == argIndex) {
                        if (remainingPath.Count > 0) {
                            newArgs[i] = UpdateTypeAtPathRecursive(currentArgs[i], remainingPath, newArgumentType) ?? currentArgs[i];
                        } else {
                            newArgs[i] = newArgumentType;
                        }
                    } else {
                        newArgs[i] = currentArgs[i];
                    }
                }
                
                try {
                    var result = genericDefinition.MakeGenericType(newArgs);
                    // Update construction state
                    constructionState[genericDefinition] = newArgs;
                    return result;
                } catch {
                    return currentType;
                }
            }
            
            return currentType;
        }
        
        List<Type> BuildValidTypesForGenericParameter(Type[] constraints, List<int> path) {
            bool allowOpenGenerics = Options?.AllowOpenGenerics ?? false;
            bool allowSelfNesting = Options?.AllowSelfNesting ?? false;
            
            // Collect all types in the current type tree path to check for self-nesting
            var typesInPath = new HashSet<Type>();
            if (!allowSelfNesting) {
                var currentType = ValueEntry.SmartValue?.Type;
                if (currentType != null) {
                    // Build the effective current type including construction state
                    var effectiveType = BuildTypeFromConstructionState(currentType);
                    CollectTypesInPath(effectiveType, path, typesInPath);
                }
            }
            
            return AvailableTypes.Where(t => {
                // Check self-nesting against all types in the path
                if (!allowSelfNesting && typesInPath.Count > 0) {
                    if (t.IsGenericType || t.IsGenericTypeDefinition) {
                        var tDef = t.IsGenericTypeDefinition ? t : t.GetGenericTypeDefinition();
                        if (typesInPath.Contains(tDef)) {
                            return false;
                        }
                    }
                }
                
                // Check constraints
                foreach (var constraint in constraints) {
                    if (!constraint.IsAssignableFrom(t)) {
                        // For generic constraints, check if t implements the generic interface
                        if (constraint.IsGenericType && t.IsGenericType) {
                            var constraintDef = constraint.GetGenericTypeDefinition();
                            var interfaces = t.GetInterfaces();
                            bool implements = interfaces.Any(i => 
                                i.IsGenericType && i.GetGenericTypeDefinition() == constraintDef);
                            if (!implements)
                                return false;
                        } else {
                            return false;
                        }
                    }
                }
                
                return true;
            }).ToList();
        }
        
        Type BuildTypeFromConstructionState(Type type) {
            if (type == null) return null;
            
            if (type.IsGenericTypeDefinition) {
                // Check if we have construction state
                if (constructionState.TryGetValue(type, out var args) && args.All(a => a != null)) {
                    try {
                        return type.MakeGenericType(args);
                    } catch {
                        return type;
                    }
                }
                return type;
            } else if (type.IsGenericType) {
                // Constructed generic - recursively rebuild arguments
                var genericDef = type.GetGenericTypeDefinition();
                var currentArgs = type.GetGenericArguments();
                var newArgs = new Type[currentArgs.Length];
                bool anyChanged = false;
                
                for (int i = 0; i < currentArgs.Length; i++) {
                    newArgs[i] = BuildTypeFromConstructionState(currentArgs[i]);
                    if (newArgs[i] != currentArgs[i]) {
                        anyChanged = true;
                    }
                }
                
                if (anyChanged) {
                    try {
                        return genericDef.MakeGenericType(newArgs);
                    } catch {
                        return type;
                    }
                }
            }
            
            return type;
        }
        
        void CollectTypesInPath(Type type, List<int> targetPath, HashSet<Type> result) {
            // If path is empty, we've reached the target node - don't add it
            // We want to collect types UP TO but not INCLUDING the target
            if (targetPath.Count == 0) {
                return;
            }
            
            // Add the current type to the result (this is an ancestor of the target)
            if (type.IsGenericType || type.IsGenericTypeDefinition) {
                var genericDef = type.IsGenericType && !type.IsGenericTypeDefinition 
                    ? type.GetGenericTypeDefinition() 
                    : type;
                result.Add(genericDef);
            }
            
            // Navigate down to the next level in the path
            int argIndex = targetPath[0];
            var remainingPath = targetPath.Skip(1).ToList();
            
            if (type.IsGenericType && !type.IsGenericTypeDefinition) {
                // Constructed generic - navigate through actual arguments
                var args = type.GetGenericArguments();
                if (argIndex >= 0 && argIndex < args.Length) {
                    var nextType = args[argIndex];
                    CollectTypesInPath(nextType, remainingPath, result);
                }
            } else if (type.IsGenericTypeDefinition) {
                // Open generic - check construction state
                if (constructionState.TryGetValue(type, out var stateArgs)) {
                    if (argIndex >= 0 && argIndex < stateArgs.Length && stateArgs[argIndex] != null) {
                        var nextType = stateArgs[argIndex]!;
                        CollectTypesInPath(nextType, remainingPath, result);
                    }
                }
            }
        }
        
        struct DropdownInfo {
            public Type? Type;
            public bool IsBaseType;
            public bool IsGenericArgument;
            public int? ArgumentIndex;
            public List<int> Path;
            public Type? GenericDefinition;
            public Type? ConstructedType;
            public Type? GenericParameter;
        }
    }
}

