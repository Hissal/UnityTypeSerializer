using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    /// <summary>
    /// Inline drawer for SerializedType that displays everything on a single line with multiple dropdowns.
    /// This is the default, simpler mode for SerializedType fields.
    /// Shared between generic and non-generic SerializedType via <see cref="ISerializedTypeValueAccessor"/>.
    /// </summary>
    internal sealed class SerializedTypeDrawerInlineMode : SerializedTypeDrawerBase, ISerializedTypeDrawerImplementation {
        
        // UI Layout Constants
        const float MIN_DROPDOWN_WIDTH = 60f;
        
        readonly List<GenericSelectorItem<Type>> dropdownItems;
        
        // Track construction state for multi-parameter generics
        // Maps from generic definition to array of selected argument types
        readonly Dictionary<Type, Type?[]> constructionState = new Dictionary<Type, Type?[]>();
        
        // Track the last type we rebuilt construction state from
        Type? lastRebuiltType = null;
        
        // Guard against re-entrant updates
        bool isUpdating = false;
        
        // Cached styles for token rendering
        GUIStyle? labelStyle;
        GUIStyle? literalStyle;
        
        public SerializedTypeDrawerInlineMode(
            InspectorProperty property,
            ISerializedTypeValueAccessor accessor,
            SerializedTypeOptionsAttribute? options,
            List<Type> availableTypes) 
            : base(property, accessor, options, availableTypes) {
            
            // Build dropdown items
            dropdownItems = new List<GenericSelectorItem<Type>>();
            foreach (var type in availableTypes) {
                dropdownItems.Add(new GenericSelectorItem<Type>(GetTypeName(type), type));
            }
        }
        
        public void DrawPropertyLayout(GUIContent label) {
            var currentType = Accessor.GetSelectedType();
            
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
            // Draw inline construction UI using token-based rendering
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
                    UpdateValue(selectedType);
                };
                selector.ShowInPopup(rect.position);
            }
        }
        
        void DrawInlineGenericConstruction(Rect rect, Type currentType) {
            // Build tokens for code-like rendering
            var tokens = BuildRenderTokens(currentType);
            
            if (tokens.Count == 0) {
                // Fallback to simple dropdown
                DrawBaseTypeDropdown(rect, currentType);
                return;
            }
            
            // Draw the tokens
            DrawInlineTokens(rect, tokens);
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
                var currentType = Accessor.GetSelectedType();
                var genericParam = currentType != null ? GetGenericParameterAtPath(currentType, info.Path) : null;
                var validTypes = BuildValidTypesForGenericParameter(genericParam, info.Path);
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
                        UpdateValue(selectedType);
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
            
            // Build list of valid types for this generic parameter
            var validTypes = BuildValidTypesForGenericParameter(genericParam, info.Path);
            
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
            var currentType = Accessor.GetSelectedType();
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
                    bool allSelected = args.All(a => a != null);
                    
                    if (allSelected) {
                        // All arguments selected - construct the type
                        try {
                            var constructedType = currentType.MakeGenericType(args.Cast<Type>().ToArray());
                            UpdateValue(constructedType);
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
                        UpdateValue(constructedType);
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
                                UpdateValue(constructedType);
                            } catch {
                                // Construction failed
                            }
                        }
                    }
                } else if (currentType.IsGenericType) {
                    // Already constructed - update recursively
                    var updatedType = UpdateTypeAtPathRecursive(currentType, path, newArgumentType);
                    if (updatedType != null) {
                        UpdateValue(updatedType);
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
        
        List<Type> BuildValidTypesForGenericParameter(Type? genericParameter, List<int> path) {
            bool allowSelfNesting = Options?.AllowSelfNesting ?? false;
            
            // Collect all types in the current type tree path to check for self-nesting
            var typesInPath = new HashSet<Type>();
            if (!allowSelfNesting) {
                var currentType = Accessor.GetSelectedType();
                if (currentType != null) {
                    // Use the effective type that incorporates constructionState so that
                    // self-nesting detection also works for partially-constructed selections.
                    var effectiveType = BuildTypeFromConstructionState(currentType) ?? currentType;
                    CollectTypesInPath(effectiveType, path, typesInPath);
                }
            }
            
            // Generic argument candidates are filtered only by generic parameter constraints
            // and generic construction rules (self-nesting prevention, etc.).
            // CustomTypeFilter is NOT applied here — it only applies to the final assignable type list.
            IEnumerable<Type> candidateTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => {
                    try {
                        return a.GetTypes();
                    } catch {
                        return Enumerable.Empty<Type>();
                    }
                });
            
            return candidateTypes
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => {
                    // Check self-nesting
                    if (!allowSelfNesting && t.IsGenericTypeDefinition && typesInPath.Contains(t))
                        return false;
                    
                    // Check all generic parameter constraints (class, struct, new(), base/interface)
                    return SerializedTypeDrawerCore.CheckGenericParameterConstraints(t, genericParameter).ShowInDropdown;
                })
                .OrderBy(t => GetTypeName(t))
                .ToList();
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
        
        /// <summary>
        /// Safe wrapper for updating the SerializedType value.
        /// Guards against re-entrant updates that cause collection modification errors.
        /// </summary>
        void UpdateValue(Type? newType) {
            if (isUpdating) return;
            
            try {
                isUpdating = true;
                ApplySelectedType(newType);
            } finally {
                isUpdating = false;
            }
        }
        
        /// <summary>
        /// Build render tokens for code-like display with literal &lt;, ,, &gt; and labels.
        /// </summary>
        List<RenderToken> BuildRenderTokens(Type? currentType) {
            var tokens = new List<RenderToken>();
            
            if (currentType == null) {
                // No type selected - single dropdown
                tokens.Add(new RenderToken {
                    Kind = TokenKind.Dropdown,
                    Info = new DropdownInfo {
                        Type = null,
                        IsBaseType = true,
                        Path = new List<int>()
                    }
                });
                return tokens;
            }
            
            var visited = new HashSet<string>();
            AppendTypeTokens(currentType, tokens, new List<int>(), visited, 0);
            return tokens;
        }
        
        /// <summary>
        /// Recursively append tokens for a type, including nested generics.
        /// </summary>
        void AppendTypeTokens(Type type, List<RenderToken> tokens, List<int> path, HashSet<string> visited, int depth) {
            const int MAX_DEPTH = 10;
            if (depth > MAX_DEPTH) return;
            
            // Create a unique key for this type+path combination
            var pathKey = string.Join("/", path);
            var visitKey = $"{type.AssemblyQualifiedName}@{pathKey}";
            if (visited.Contains(visitKey)) return;
            visited.Add(visitKey);
            
            bool isGeneric = type.IsGenericType || type.IsGenericTypeDefinition;
            Type? genericDef = null;
            Type[]? genericArgs = null;
            Type[]? genericParams = null;
            
            if (type.IsGenericTypeDefinition) {
                genericDef = type;
                genericParams = type.GetGenericArguments();
                // Check construction state for selected args
                // If no construction state exists yet, initialize it so placeholders can be rendered
                if (!constructionState.TryGetValue(type, out var stateArgs)) {
                    stateArgs = new Type?[genericParams.Length];
                    constructionState[type] = stateArgs;
                }
                genericArgs = stateArgs;
            } else if (type.IsGenericType) {
                genericDef = type.GetGenericTypeDefinition();
                genericArgs = type.GetGenericArguments();
                genericParams = genericDef.GetGenericArguments();
            }
            
            // Add the base type dropdown
            tokens.Add(new RenderToken {
                Kind = TokenKind.Dropdown,
                Info = new DropdownInfo {
                    Type = isGeneric ? genericDef : type,
                    IsBaseType = true,
                    Path = new List<int>(path),
                    GenericDefinition = genericDef,
                    ConstructedType = isGeneric && !type.IsGenericTypeDefinition ? type : null
                }
            });
            
            if (!isGeneric || genericArgs == null || genericParams == null) {
                return; // Non-generic type, we're done
            }
            
            // Add opening angle bracket
            tokens.Add(RenderToken.Literal(" <"));
            
            // Process each generic argument
            for (int i = 0; i < genericArgs.Length; i++) {
                if (i > 0) {
                    tokens.Add(RenderToken.Literal(", "));
                }
                
                // Add parameter label (T:, TKey:, etc.)
                string label = GetGenericLabel(genericParams[i], i);
                tokens.Add(RenderToken.Label(label + ": "));
                
                var arg = genericArgs[i];
                var argPath = new List<int>(path) { i };
                
                if (arg == null) {
                    // No selection yet - placeholder dropdown
                    tokens.Add(new RenderToken {
                        Kind = TokenKind.Dropdown,
                        Info = new DropdownInfo {
                            Type = null,
                            IsGenericArgument = true,
                            ArgumentIndex = i,
                            Path = argPath,
                            GenericDefinition = genericDef,
                            GenericParameter = genericParams[i]
                        }
                    });
                } else if (arg.IsGenericParameter) {
                    // Unresolved generic parameter
                    tokens.Add(new RenderToken {
                        Kind = TokenKind.Dropdown,
                        Info = new DropdownInfo {
                            Type = null,
                            IsGenericArgument = true,
                            ArgumentIndex = i,
                            Path = argPath,
                            GenericDefinition = genericDef,
                            GenericParameter = arg
                        }
                    });
                } else if (arg.IsGenericType || arg.IsGenericTypeDefinition) {
                    // Nested generic - recurse
                    AppendTypeTokens(arg, tokens, argPath, visited, depth + 1);
                } else {
                    // Concrete type
                    tokens.Add(new RenderToken {
                        Kind = TokenKind.Dropdown,
                        Info = new DropdownInfo {
                            Type = arg,
                            IsGenericArgument = true,
                            ArgumentIndex = i,
                            Path = argPath,
                            GenericDefinition = genericDef
                        }
                    });
                }
            }
            
            // Add closing angle bracket
            tokens.Add(RenderToken.Literal(" >"));
        }
        
        /// <summary>
        /// Get a label for a generic parameter.
        /// Uses the parameter name if available, otherwise falls back to T1, T2, etc.
        /// </summary>
        string GetGenericLabel(Type genericParam, int index) {
            if (genericParam != null && !string.IsNullOrWhiteSpace(genericParam.Name)) {
                return genericParam.Name;
            }
            return $"T{index + 1}";
        }
        
        /// <summary>
        /// Draw tokens as a code-like inline expression.
        /// </summary>
        void DrawInlineTokens(Rect rect, List<RenderToken> tokens) {
            if (tokens.Count == 0) return;
            
            // Initialize cached styles if needed
            if (labelStyle == null) {
                labelStyle = new GUIStyle(EditorStyles.label) {
                    fontSize = Mathf.Max(9, EditorStyles.label.fontSize - 1),
                    normal = { textColor = new Color(0.6f, 0.6f, 0.6f, 0.8f) }
                };
            }
            
            if (literalStyle == null) {
                literalStyle = new GUIStyle(EditorStyles.label) {
                    fontSize = EditorStyles.label.fontSize,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 1f) }
                };
            }
            
            // First pass: calculate widths
            float totalLiteralWidth = 0f;
            float totalLabelWidth = 0f;
            int dropdownCount = 0;
            
            foreach (var token in tokens) {
                switch (token.Kind) {
                    case TokenKind.Literal:
                        totalLiteralWidth += literalStyle.CalcSize(new GUIContent(token.Text)).x;
                        break;
                    case TokenKind.Label:
                        totalLabelWidth += labelStyle.CalcSize(new GUIContent(token.Text)).x;
                        break;
                    case TokenKind.Dropdown:
                        dropdownCount++;
                        break;
                }
            }
            
            // Allocate remaining space to dropdowns
            float availableForDropdowns = rect.width - totalLiteralWidth - totalLabelWidth;
            float dropdownWidth = dropdownCount > 0 ? availableForDropdowns / dropdownCount : 0f;
            dropdownWidth = Mathf.Max(MIN_DROPDOWN_WIDTH, dropdownWidth);
            
            // Second pass: render tokens
            float currentX = rect.x;
            
            foreach (var token in tokens) {
                switch (token.Kind) {
                    case TokenKind.Literal: {
                        var size = literalStyle.CalcSize(new GUIContent(token.Text));
                        var literalRect = new Rect(currentX, rect.y, size.x, rect.height);
                        EditorGUI.LabelField(literalRect, token.Text, literalStyle);
                        currentX += size.x;
                        break;
                    }
                    case TokenKind.Label: {
                        var size = labelStyle.CalcSize(new GUIContent(token.Text));
                        var labelRect = new Rect(currentX, rect.y, size.x, rect.height);
                        EditorGUI.LabelField(labelRect, token.Text, labelStyle);
                        currentX += size.x;
                        break;
                    }
                    case TokenKind.Dropdown: {
                        if (token.Info.HasValue) {
                            var dropdownRect = new Rect(currentX, rect.y, dropdownWidth, rect.height);
                            DrawDropdown(dropdownRect, token.Info.Value);
                            currentX += dropdownWidth;
                        }
                        break;
                    }
                }
            }
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
        
        enum TokenKind {
            Literal,    // Text tokens like "<", ",", ">"
            Label,      // Parameter labels like "T1:", "TKey:"
            Dropdown    // Interactive type selector dropdown
        }
        
        struct RenderToken {
            public TokenKind Kind;
            public string Text;
            public DropdownInfo? Info;
            
            public static RenderToken Literal(string text) => new RenderToken { 
                Kind = TokenKind.Literal, 
                Text = text 
            };
            
            public static RenderToken Label(string text) => new RenderToken { 
                Kind = TokenKind.Label, 
                Text = text 
            };
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
