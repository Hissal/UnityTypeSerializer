using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    /// <summary>
    /// Complex constructor drawer for SerializedType that provides a detailed UI for constructing generic types.
    /// This drawer shows a full constructor interface with nested generic type support.
    /// </summary>
    internal sealed class ComplexConstructorSerializedTypeDrawer<TBase> : SerializedTypeDrawerBase<TBase>, ISerializedTypeDrawerImplementation 
        where TBase : class {
        
        Type[]? selectedTypeArguments;
        GenericConstructionState? constructionState;
        readonly List<GenericSelectorItem<Type>> dropdownItems;
        
        public ComplexConstructorSerializedTypeDrawer(
            InspectorProperty property,
            PropertyValueEntry<SerializedType<TBase>> valueEntry,
            SerializedTypeOptionsAttribute? options,
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
            bool allowGenericTypeConstruction = Options?.AllowGenericTypeConstruction ?? false;
            bool allowOpenGenerics = Options?.AllowOpenGenerics ?? false;
            
            // Check if we're in active construction mode
            bool isConstructing = currentType != null && currentType.IsGenericTypeDefinition 
                && selectedTypeArguments != null;
            
            if (isConstructing && currentType != null) {
                DrawGenericTypeConstructor(label, currentType);
                return;
            }
            
            // Draw dropdown with optional construct button
            EditorGUILayout.BeginHorizontal();
            
            var rect = EditorGUILayout.GetControlRect(true, GUILayout.ExpandWidth(true));
            rect = EditorGUI.PrefixLabel(rect, label);

            var displayName = currentType != null ? GetTypeName(currentType) : "None";
            
            if (EditorGUI.DropdownButton(rect, new GUIContent(displayName), FocusType.Keyboard)) {
                if (dropdownItems == null)
                    return;
                    
                var selector = new GenericSelector<Type>("Select Type", false, dropdownItems);
                selector.SelectionConfirmed += selection => {
                    var selectedType = selection.FirstOrDefault();
                    if (selectedType != null && selectedType.IsGenericTypeDefinition) {
                        if (allowOpenGenerics && !allowGenericTypeConstruction) {
                            // When only AllowOpenGenerics is true (no construction), immediately assign the open generic
                            ValueEntry.SmartValue = new SerializedType<TBase> { Type = selectedType };
                            selectedTypeArguments = null;
                        }
                        else if (!allowOpenGenerics && allowGenericTypeConstruction) {
                            // When only AllowGenericTypeConstruction is true (no open generics), force construction
                            ValueEntry.SmartValue = new SerializedType<TBase> { Type = selectedType };
                            var argCount = selectedType.GetGenericArguments().Length;
                            selectedTypeArguments = new Type[argCount];
                        }
                        else if (allowOpenGenerics && allowGenericTypeConstruction) {
                            // When both are true, assign the open generic (construct button will appear)
                            ValueEntry.SmartValue = new SerializedType<TBase> { Type = selectedType };
                            selectedTypeArguments = null;
                        }
                        else {
                            // Neither option is enabled - this shouldn't happen
                            ValueEntry.SmartValue = new SerializedType<TBase> { Type = null };
                            selectedTypeArguments = null;
                        }
                    }
                    else {
                        // Concrete type selected
                        ValueEntry.SmartValue = new SerializedType<TBase> { Type = selectedType };
                        selectedTypeArguments = null;
                    }
                    ValueEntry.ApplyChanges();
                };
                selector.ShowInPopup(rect.position);
            }
            
            // Show construct button when both options are enabled and current type is an open generic
            if (allowOpenGenerics && allowGenericTypeConstruction 
                && currentType != null && currentType.IsGenericTypeDefinition) {
                if (GUILayout.Button("▶ Construct", GUILayout.Width(80))) {
                    // Initialize construction state to enter construction mode
                    var argCount = currentType.GetGenericArguments().Length;
                    selectedTypeArguments = new Type[argCount];
                    // Force a repaint to show the constructor UI
                    GUIHelper.RequestRepaint();
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        void DrawGenericTypeConstructor(GUIContent label, Type openGenericType) {
            // Initialize construction state if needed
            if (constructionState == null) {
                constructionState = new GenericConstructionState();
            }
            
            // Initialize selectedTypeArguments if needed
            var genericArgs = openGenericType.GetGenericArguments();
            if (selectedTypeArguments == null || selectedTypeArguments.Length != genericArgs.Length) {
                // Create new array with proper size
                var newArgs = new Type?[genericArgs.Length];
                
                // Preserve existing arguments if resizing
                if (selectedTypeArguments != null) {
                    for (int i = 0; i < Math.Min(selectedTypeArguments.Length, newArgs.Length); i++) {
                        newArgs[i] = selectedTypeArguments[i];
                    }
                }
                
                selectedTypeArguments = newArgs;
            }
            
            bool allowOpenGenerics = Options?.AllowOpenGenerics ?? false;
            
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(label.text, EditorStyles.boldLabel);
            
            // Draw type preview at the top
            var previewType = BuildTypePreviewString(openGenericType, selectedTypeArguments);
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            EditorGUILayout.LabelField("Type Preview:", GUILayout.Width(90));
            EditorGUILayout.SelectableLabel(previewType, EditorStyles.wordWrappedLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            
            // Draw recursively starting from root path
            var rootPath = new List<int>();
            var constructionResult = DrawGenericConstructorRecursive(openGenericType, rootPath, 0, selectedTypeArguments);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            // Determine button states based on construction completeness
            bool isFullyConcrete = constructionResult.IsFullyConcrete;
            bool allArgsSelected = constructionResult.AllArgumentsSelected;
            
            // Show appropriate button labels and enable states
            if (allowOpenGenerics) {
                // When open generics are allowed, we can construct types that contain open generic arguments
                GUI.enabled = allArgsSelected;
                
                // Visual feedback: use different color when type contains generic parameters
                var originalColor = GUI.backgroundColor;
                if (!isFullyConcrete) {
                    GUI.backgroundColor = new Color(0.8f, 0.6f, 1f);
                }
                
                string buttonLabel = isFullyConcrete ? "Construct Type" : "Construct Type (w/ Open)";
                if (GUILayout.Button(buttonLabel)) {
                    ApplyConstruction(openGenericType, selectedTypeArguments);
                }
                
                GUI.backgroundColor = originalColor;
                GUI.enabled = true;
            }
            else {
                // When open generics are not allowed, must be fully concrete
                GUI.enabled = isFullyConcrete && allArgsSelected;
                if (GUILayout.Button("Construct Type")) {
                    ApplyConstruction(openGenericType, selectedTypeArguments);
                }
                GUI.enabled = true;
            }
            
            if (GUILayout.Button("Cancel")) {
                if (allowOpenGenerics) {
                    // Keep the open generic type, just exit construction mode
                }
                else {
                    // Clear the type entirely when open generics aren't allowed
                    ValueEntry.SmartValue = new SerializedType<TBase> { Type = null };
                    ValueEntry.ApplyChanges();
                }
                
                selectedTypeArguments = null;
                constructionState = null;
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        
        string BuildTypePreviewString(Type openGenericType, Type?[]? typeArgs) {
            if (typeArgs == null || typeArgs.Length == 0)
                return GetTypeName(openGenericType);
            
            var baseName = openGenericType.Name.Split('`')[0];
            var argStrings = new List<string>();
            
            for (int i = 0; i < typeArgs.Length; i++) {
                if (typeArgs[i] == null) {
                    argStrings.Add("?");
                }
                else if (typeArgs[i]!.IsGenericTypeDefinition) {
                    // Check if we have nested construction state for this
                    var childPath = new List<int> { i };
                    var childArgs = constructionState?.GetArguments(childPath);
                    if (childArgs != null && childArgs.Any(a => a != null)) {
                        argStrings.Add(BuildTypePreviewStringRecursive(typeArgs[i]!, childPath));
                    }
                    else {
                        argStrings.Add(GetTypeName(typeArgs[i]!));
                    }
                }
                else {
                    argStrings.Add(GetTypeName(typeArgs[i]!));
                }
            }
            
            return $"{baseName}<{string.Join(", ", argStrings)}>";
        }
        
        string BuildTypePreviewStringRecursive(Type openGenericType, List<int> path) {
            var args = constructionState?.GetArguments(path);
            if (args == null || args.Length == 0)
                return GetTypeName(openGenericType);
            
            var baseName = openGenericType.Name.Split('`')[0];
            var argStrings = new List<string>();
            
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == null) {
                    argStrings.Add("?");
                }
                else if (args[i]!.IsGenericTypeDefinition) {
                    var childPath = new List<int>(path) { i };
                    var childArgs = constructionState?.GetArguments(childPath);
                    if (childArgs != null && childArgs.Any(a => a != null)) {
                        argStrings.Add(BuildTypePreviewStringRecursive(args[i]!, childPath));
                    }
                    else {
                        argStrings.Add(GetTypeName(args[i]!));
                    }
                }
                else {
                    argStrings.Add(GetTypeName(args[i]!));
                }
            }
            
            return $"{baseName}<{string.Join(", ", argStrings)}>";
        }
        
        bool IsDeepestExpandedConstructor(List<int> path) {
            if (constructionState == null)
                return false;
            
            // Check if any child of this path has an expanded constructor
            var args = constructionState.GetArguments(path);
            if (args == null)
                return true;
            
            var expandedIndex = constructionState.GetExpandedIndex(path);
            if (expandedIndex.HasValue) {
                return false;
            }
            
            // Check if any of the arguments are open generics that could be expanded
            for (int i = 0; i < args.Length; i++) {
                if (args[i] != null && args[i]!.IsGenericTypeDefinition) {
                    var childPath = new List<int>(path) { i };
                    var childExpandedIndex = constructionState.GetExpandedIndex(childPath);
                    if (childExpandedIndex.HasValue) {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        void ApplyNestedConstruction(List<int> parentPath, int argIndex) {
            bool allowOpenGenerics = Options?.AllowOpenGenerics ?? false;
            
            // Get the nested constructor path
            var childPath = new List<int>(parentPath) { argIndex };
            var childArgs = constructionState!.GetArguments(childPath);
            var parentArgs = constructionState.GetArguments(parentPath);
            
            if (childArgs == null || parentArgs == null)
                return;
            
            var nestedGenericType = parentArgs[argIndex];
            if (nestedGenericType == null || !nestedGenericType.IsGenericTypeDefinition)
                return;
            
            // Check if all child arguments are selected
            if (!childArgs.All(a => a != null)) {
                Debug.LogWarning("Cannot apply nested construction - not all type arguments are selected.");
                return;
            }
            
            try {
                var constructedNested = nestedGenericType.MakeGenericType(childArgs.Cast<Type>().ToArray());
                
                bool containsOpenGenerics = constructedNested.ContainsGenericParameters;
                
                if (!allowOpenGenerics && containsOpenGenerics) {
                    Debug.LogWarning("Cannot apply nested construction - result contains open generic type definitions and AllowOpenGenerics is false.");
                    return;
                }
                
                // Update parent's argument with the constructed type
                parentArgs[argIndex] = constructedNested;
                constructionState.SetArguments(parentPath, parentArgs);
                
                // Clear the child construction state since we've applied it
                constructionState.ClearPath(childPath);
                constructionState.SetExpandedIndex(parentPath, null);
                
                GUIHelper.RequestRepaint();
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to apply nested construction: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        void ApplyConstruction(Type openGenericType, Type?[]? typeArgs) {
            if (typeArgs == null)
                return;
            
            bool allowOpenGenerics = Options?.AllowOpenGenerics ?? false;
            
            try {
                // Process all arguments
                var processedArgs = new Type?[typeArgs.Length];
                bool allConcrete = true;
                
                for (int i = 0; i < typeArgs.Length; i++) {
                    if (typeArgs[i] == null) {
                        processedArgs[i] = null;
                        allConcrete = false;
                    }
                    else {
                        // If this argument is an open generic type definition, recursively construct it first
                        if (typeArgs[i]!.IsGenericTypeDefinition && constructionState != null) {
                            // Check if there's nested construction state for this argument
                            var nestedPath = new List<int> { i };
                            var nestedArgs = constructionState.GetArguments(nestedPath);
                            
                            if (nestedArgs != null && nestedArgs.All(a => a != null)) {
                                // Recursively construct the nested type
                                var constructedNested = ConstructNestedTypeRecursively(typeArgs[i]!, nestedPath);
                                processedArgs[i] = constructedNested;
                                
                                if (constructedNested.ContainsGenericParameters) {
                                    allConcrete = false;
                                }
                            }
                            else {
                                // Nested arguments not fully selected, keep as open generic
                                processedArgs[i] = typeArgs[i];
                                allConcrete = false;
                            }
                        }
                        else {
                            processedArgs[i] = typeArgs[i];
                            
                            if (processedArgs[i]!.ContainsGenericParameters) {
                                allConcrete = false;
                            }
                        }
                    }
                }
                
                if (!processedArgs.All(t => t != null)) {
                    Debug.LogWarning("Cannot apply construction - all type arguments must be selected.");
                    return;
                }
                
                if (!allConcrete && !allowOpenGenerics) {
                    Debug.LogWarning("Cannot apply construction - one or more type arguments contain generic parameters. Enable AllowOpenGenerics to construct types with generic parameters.");
                    return;
                }
                
                // All arguments are selected - construct the type
                var constructedType = openGenericType.MakeGenericType(processedArgs.Cast<Type>().ToArray());
                ValueEntry.SmartValue = new SerializedType<TBase> { Type = constructedType };
                ValueEntry.ApplyChanges();
                selectedTypeArguments = null;
                constructionState = null;
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to construct generic type: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        Type ConstructNestedTypeRecursively(Type openGenericType, List<int> path) {
            if (!openGenericType.IsGenericTypeDefinition) {
                return openGenericType;
            }
            
            var nestedArgs = constructionState!.GetArguments(path);
            if (nestedArgs == null) {
                throw new InvalidOperationException($"No construction state found for type '{openGenericType.Name}' at path {string.Join("/", path)}");
            }
            
            var processedNestedArgs = new Type[nestedArgs.Length];
            
            for (int i = 0; i < nestedArgs.Length; i++) {
                if (nestedArgs[i] == null) {
                    throw new InvalidOperationException($"Argument {i} for type '{openGenericType.Name}' at path {string.Join("/", path)} is null (expected {nestedArgs.Length} arguments)");
                }
                
                // If this nested argument is also an open generic, recursively construct it
                if (nestedArgs[i]!.IsGenericTypeDefinition) {
                    var childPath = new List<int>(path) { i };
                    var childArgs = constructionState.GetArguments(childPath);
                    
                    if (childArgs != null && childArgs.All(a => a != null)) {
                        processedNestedArgs[i] = ConstructNestedTypeRecursively(nestedArgs[i]!, childPath);
                    }
                    else {
                        // Keep as open generic if nested args not available
                        processedNestedArgs[i] = nestedArgs[i]!;
                    }
                }
                else {
                    processedNestedArgs[i] = nestedArgs[i]!;
                }
            }
            
            return openGenericType.MakeGenericType(processedNestedArgs);
        }

        struct ConstructionResult {
            public bool IsFullyConcrete;
            public bool AllArgumentsSelected;
        }
        
        ConstructionResult DrawGenericConstructorRecursive(Type openGenericType, List<int> path, int depth, Type?[]? targetArgsArray) {
            bool allowOpenGenerics = Options?.AllowOpenGenerics ?? false;
            var isNested = depth > 0;
            
            if (isNested) {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                EditorGUILayout.BeginVertical(SerializedTypeDrawerStyles.MinimalVertical);
                
                // Draw the label and repick button on the same line
                EditorGUILayout.BeginHorizontal();
                var arrow = new string('↳', depth);
                EditorGUILayout.LabelField($"{arrow} Constructing: {GetTypeName(openGenericType)}", EditorStyles.miniLabel);
                
                // Add a subtle repick button
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
                if (GUILayout.Button("⟲", GUILayout.Width(20), GUILayout.Height(14))) {
                    // Clear this nested constructor to allow repicking
                    constructionState!.ClearPath(path);
                    // Also clear the parent's selection for this argument AND expanded index
                    if (path.Count > 0) {
                        var parentPath = path.Take(path.Count - 1).ToList();
                        var parentArgs = constructionState.GetArguments(parentPath);
                        if (parentArgs != null) {
                            var argIndex = path[path.Count - 1];
                            parentArgs[argIndex] = null;
                            constructionState.SetArguments(parentPath, parentArgs);
                            // Clear the expanded index so the new selection won't auto-expand
                            constructionState.SetExpandedIndex(parentPath, null);
                        }
                    }
                }
                GUI.backgroundColor = originalColor;
                EditorGUILayout.EndHorizontal();
            }
            else {
                // Root level - draw label and repick button
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Constructing: {GetTypeName(openGenericType)}", EditorStyles.miniLabel);
                
                // Add a subtle repick button at root level
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
                if (GUILayout.Button("⟲", GUILayout.Width(20), GUILayout.Height(14))) {
                    // Clear the entire construction and reset to allow repicking the root type
                    ValueEntry.SmartValue = new SerializedType<TBase> { Type = null };
                    ValueEntry.ApplyChanges();
                    
                    // Clear all construction state including nested paths
                    if (constructionState != null) {
                        constructionState.ClearAll();
                    }
                    selectedTypeArguments = null;
                    constructionState = null;
                    
                    GUI.backgroundColor = originalColor;
                    EditorGUILayout.EndHorizontal();
                    GUIHelper.RequestRepaint();
                    return new ConstructionResult {
                        IsFullyConcrete = false,
                        AllArgumentsSelected = false
                    };
                }
                GUI.backgroundColor = originalColor;
                EditorGUILayout.EndHorizontal();
            }
            
            var genericArgs = openGenericType.GetGenericArguments();
            
            // Get or create argument array for this path
            var args = constructionState!.GetArguments(path);
            if (args == null || args.Length != genericArgs.Length) {
                // Create new array or recreate if size doesn't match
                var newArgs = new Type?[genericArgs.Length];
                
                // Preserve existing arguments if we're resizing
                if (args != null) {
                    for (int j = 0; j < Math.Min(args.Length, newArgs.Length); j++) {
                        newArgs[j] = args[j];
                    }
                }
                
                // For root level (depth 0), use selectedTypeArguments as the source
                if (depth == 0 && targetArgsArray != null) {
                    for (int j = 0; j < Math.Min(targetArgsArray.Length, newArgs.Length); j++) {
                        if (args == null || j >= args.Length) {
                            // Only copy if we don't already have a value from previous args
                            newArgs[j] = targetArgsArray[j];
                        }
                    }
                }
                // For nested levels, restore from parent's constructed type if available
                else if (targetArgsArray != null && path.Count > 0) {
                    var parentIndex = path[path.Count - 1];
                    if (parentIndex < targetArgsArray.Length) {
                        var existingType = targetArgsArray[parentIndex];
                        if (existingType != null && existingType.IsGenericType && !existingType.IsGenericTypeDefinition) {
                            var existingArgs = existingType.GetGenericArguments();
                            for (int j = 0; j < Math.Min(existingArgs.Length, newArgs.Length); j++) {
                                if (args == null || j >= args.Length) {
                                    // Only copy if we don't already have a value from previous args
                                    newArgs[j] = existingArgs[j];
                                }
                            }
                        }
                    }
                }
                
                args = newArgs;
                constructionState.SetArguments(path, args);
            }
            
            // At root level, keep selectedTypeArguments in sync
            if (depth == 0 && targetArgsArray != null) {
                // Sync both ways - ensure they match in size and content
                for (int j = 0; j < args.Length; j++) {
                    if (j < targetArgsArray.Length) {
                        targetArgsArray[j] = args[j];
                    }
                }
            }
            
            var expandedIndex = constructionState.GetExpandedIndex(path);
            bool allArgsConcrete = true;
            bool allArgsSelected = true;
            
            // Draw each type argument
            for (int i = 0; i < genericArgs.Length; i++) {
                var arg = genericArgs[i];
                var currentArg = args[i];
                
                // Track if all arguments are selected
                if (currentArg == null)
                    allArgsSelected = false;
                
                // When AllowOpenGenerics is false, automatically expand nested generics for simultaneous construction
                bool shouldAutoExpand = !allowOpenGenerics && currentArg != null && currentArg.IsGenericTypeDefinition;
                
                // If this argument is expanded (or should be auto-expanded), draw its constructor
                if ((expandedIndex == i || shouldAutoExpand) && currentArg != null && currentArg.IsGenericTypeDefinition) {
                    var childPath = new List<int>(path) { i };
                    var childResult = DrawGenericConstructorRecursive(currentArg, childPath, depth + 1, args);
                    
                    bool shouldShowApplyCancel = allowOpenGenerics && IsDeepestExpandedConstructor(childPath);
                    
                    if (shouldShowApplyCancel) {
                        EditorGUILayout.Space(2);
                        EditorGUILayout.BeginHorizontal();
                        
                        // Can only apply if all arguments are selected
                        bool canApply = childResult.AllArgumentsSelected;
                        bool isFullyConcrete = childResult.IsFullyConcrete;
                        
                        // Show label based on whether result contains open generics
                        string applyLabel = isFullyConcrete ? "Apply" : "Apply (w/ Open)";
                        
                        // Visual feedback: use different color when type contains generic parameters
                        var originalColor = GUI.backgroundColor;
                        if (!isFullyConcrete) {
                            GUI.backgroundColor = new Color(0.8f, 0.6f, 1f);
                        }
                        
                        GUI.enabled = canApply;
                        if (GUILayout.Button(applyLabel, GUILayout.Width(110))) {
                            ApplyNestedConstruction(path, i);
                            GUIHelper.RequestRepaint();
                        }
                        GUI.enabled = true;
                        
                        GUI.backgroundColor = originalColor;
                        
                        if (GUILayout.Button("Cancel", GUILayout.Width(60))) {
                            constructionState!.ClearPath(childPath);
                            constructionState.SetExpandedIndex(path, null);
                            args[i] = null;
                            constructionState.SetArguments(path, args);
                            GUIHelper.RequestRepaint();
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    // Check nested construction status
                    if (!childResult.AllArgumentsSelected) {
                        allArgsSelected = false;
                    }
                    if (!childResult.IsFullyConcrete) {
                        allArgsConcrete = false;
                    }
                    
                    // When auto-expanding or manually expanded, skip drawing the type selector for this argument
                    continue;
                }
                
                // Draw type selector for this argument
                var constraints = arg.GetGenericParameterConstraints();
                var constraintNames = constraints.Length > 0 
                    ? $" (where {arg.Name} : {string.Join(", ", constraints.Select(c => c.Name))})"
                    : "";
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{arg.Name}{constraintNames}", GUILayout.Width(150));
                
                var displayName = currentArg != null ? GetTypeName(currentArg) : "Select Type...";
                
                var argIndex = i;
                if (GUILayout.Button(displayName, EditorStyles.popup)) {
                    ShowTypeArgumentSelectorRecursive(path, argIndex, arg, args);
                }
                
                // If the selected type is an open generic or contains generic parameters, handle it appropriately
                if (currentArg != null && (currentArg.IsGenericTypeDefinition || currentArg.ContainsGenericParameters)) {
                    allArgsConcrete = false;
                    
                    if (allowOpenGenerics) {
                        // Visual feedback: use different color for types that already contain some generic parameters
                        var originalColor = GUI.backgroundColor;
                        bool isPartiallyConstructed = !currentArg.IsGenericTypeDefinition && currentArg.ContainsGenericParameters;
                        
                        if (isPartiallyConstructed) {
                            GUI.backgroundColor = new Color(0.8f, 0.6f, 1f);
                        }
                        
                        if (GUILayout.Button("▶ Construct", GUILayout.Width(80))) {
                            constructionState.SetExpandedIndex(path, i);
                        }
                        
                        GUI.backgroundColor = originalColor;
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            if (isNested) {
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            
            return new ConstructionResult {
                IsFullyConcrete = allArgsConcrete,
                AllArgumentsSelected = allArgsSelected
            };
        }

        void ShowTypeArgumentSelectorRecursive(List<int> path, int argIndex, Type genericParameter, Type?[] targetArgsArray) {
            var constraints = genericParameter.GetGenericParameterConstraints();
            bool allowSelfNesting = Options?.AllowSelfNesting ?? false;
            bool allowOpenGenerics = Options?.AllowOpenGenerics ?? false;
            
            // Resolve custom filter types if any
            var customFilterTypes = GetFilteredTypes(
                Options?.IncludeTypes,
                Options?.IncludeTypesResolver
            )?.ToList();
            
            // Resolve excluded types if any
            var excludedTypes = GetFilteredTypes(
                Options?.ExcludeTypes,
                Options?.ExcludeTypesResolver
            )?.ToHashSet();
            
            // Build chain of parent generic types for self-nesting check
            var parentGenericTypes = new HashSet<Type>();
            if (!allowSelfNesting) {
                // Get the root generic type being constructed
                var rootType = ValueEntry.SmartValue?.Type;
                if (rootType != null && rootType.IsGenericTypeDefinition) {
                    parentGenericTypes.Add(rootType);
                }
                
                // Walk the construction path to find all parent generic types
                var currentPath = new List<int>();
                foreach (var index in path) {
                    var args = constructionState!.GetArguments(currentPath);
                    if (args != null && index < args.Length && index >= 0) {
                        var parentArg = args[index];
                        if (parentArg != null && parentArg.IsGenericTypeDefinition) {
                            parentGenericTypes.Add(parentArg);
                        }
                    }
                    currentPath.Add(index);
                }
            }
            
            // Get all types from all assemblies
            IEnumerable<Type> allTypes;
            
            if (customFilterTypes != null && customFilterTypes.Any()) {
                // Use custom filter as base
                allTypes = customFilterTypes;
            }
            else {
                // Get all types from all assemblies
                allTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => {
                        try {
                            return a.GetTypes();
                        }
                        catch {
                            return Enumerable.Empty<Type>();
                        }
                    });
            }
            
            // Filter types that satisfy the constraints
            var validTypes = allTypes
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => {
                    // Apply exclusion filter
                    if (excludedTypes != null && excludedTypes.Contains(t))
                        return false;
                    
                    // Check for self-nesting if not allowed
                    if (!allowSelfNesting && t.IsGenericTypeDefinition && parentGenericTypes.Contains(t))
                        return false;
                    
                    // Check special constraints first
                    var attributes = genericParameter.GenericParameterAttributes;
                    
                    // If new() constraint is present, exclude open generic types
                    if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0) {
                        if (t.IsGenericTypeDefinition)
                            return false;
                        if (!t.IsValueType && t.GetConstructor(Type.EmptyTypes) == null)
                            return false;
                    }
                    
                    if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) {
                        if (!t.IsValueType || Nullable.GetUnderlyingType(t) != null)
                            return false;
                    }
                    
                    if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0) {
                        if (t.IsValueType)
                            return false;
                    }
                    
                    // For generic type definitions, check if they could satisfy interface constraints
                    if (t.IsGenericTypeDefinition) {
                        var interfaces = t.GetInterfaces();
                        foreach (var constraint in constraints) {
                            bool satisfies;
                            
                            // Check direct constraint match or generic interface match
                            if (constraint.IsGenericType) {
                                var constraintDef = constraint.GetGenericTypeDefinition();
                                satisfies = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == constraintDef);
                            }
                            else {
                                // For non-generic constraints, check if the type or its interfaces implement it
                                satisfies = interfaces.Any(i => constraint.IsAssignableFrom(i));
                            }
                            
                            if (!satisfies)
                                return false;
                        }
                        return true;
                    }
                    
                    // For concrete types, check all constraints normally
                    foreach (var constraint in constraints) {
                        if (!constraint.IsAssignableFrom(t)) {
                            return false;
                        }
                    }
                    
                    return true;
                })
                .OrderBy(t => GetTypeName(t))
                .ToList();
            
            var items = validTypes.Select(t => new GenericSelectorItem<Type>(GetTypeName(t), t));
            
            var selector = new GenericSelector<Type>($"Select {genericParameter.Name}", false, items);
            selector.SelectionConfirmed += selection => {
                var selectedType = selection.FirstOrDefault();
                if (selectedType != null) {
                    targetArgsArray[argIndex] = selectedType;
                    constructionState!.SetArguments(path, targetArgsArray);
                }
            };
            selector.ShowInPopup();
        }
        
        IEnumerable<Type>? GetFilteredTypes(Type[]? typeArray, string? resolverMemberName) {
            var result = new HashSet<Type>();
            
            // Add types from array
            if (typeArray != null && typeArray.Length > 0) {
                foreach (var type in typeArray) {
                    if (type != null)
                        result.Add(type);
                }
            }
            
            // Add types from resolver
            if (!string.IsNullOrWhiteSpace(resolverMemberName)) {
                var resolvedTypes = ResolveTypesFromMember(resolverMemberName);
                if (resolvedTypes != null) {
                    foreach (var type in resolvedTypes) {
                        if (type != null)
                            result.Add(type);
                    }
                }
            }
            
            return result.Count > 0 ? result : null;
        }
        
        IEnumerable<Type>? ResolveTypesFromMember(string memberName) {
            try {
                Type? targetType = null;
                string? targetMemberName = null;
                
                // Parse format: "TypeName.MemberName" or "MemberName"
                var parts = memberName.Split('.');
                if (parts.Length == 1) {
                    // Just member name - search in declaring type
                    targetType = Property.Parent?.ValueEntry?.TypeOfValue;
                    targetMemberName = parts[0];
                }
                else if (parts.Length == 2) {
                    // TypeName.MemberName
                    var typeName = parts[0];
                    targetMemberName = parts[1];
                    
                    // Try to find the type
                    targetType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => {
                            try { return a.GetTypes(); }
                            catch { return Enumerable.Empty<Type>(); }
                        })
                        .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
                }
                
                if (targetType == null || targetMemberName == null) {
                    Debug.LogWarning($"Could not resolve type from: {memberName}");
                    return null;
                }
                
                // Try to find and invoke the member
                const BindingFlags c_flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                
                // Try property first
                var property = targetType.GetProperty(targetMemberName, c_flags);
                if (property != null && typeof(IEnumerable<Type>).IsAssignableFrom(property.PropertyType)) {
                    var value = property.GetValue(null);
                    return value as IEnumerable<Type>;
                }
                
                // Try method
                var method = targetType.GetMethod(targetMemberName, c_flags, null, Type.EmptyTypes, null);
                if (method != null && typeof(IEnumerable<Type>).IsAssignableFrom(method.ReturnType)) {
                    var value = method.Invoke(null, null);
                    return value as IEnumerable<Type>;
                }
                
                // Try field
                var field = targetType.GetField(targetMemberName, c_flags);
                if (field != null && typeof(IEnumerable<Type>).IsAssignableFrom(field.FieldType)) {
                    var value = field.GetValue(null);
                    return value as IEnumerable<Type>;
                }
                
                Debug.LogWarning($"Member '{targetMemberName}' not found on type '{targetType.Name}' or is not an IEnumerable<Type>");
                return null;
            }
            catch (Exception ex) {
                Debug.LogError($"Error resolving types from member '{memberName}': {ex.Message}");
                return null;
            }
        }
    }
    
    static class SerializedTypeDrawerStyles {
        static GUIStyle? s_minimalVertical;
        
        public static GUIStyle MinimalVertical {
            get {
                if (s_minimalVertical == null) {
                    s_minimalVertical = new GUIStyle {
                        padding = new RectOffset(0, 0, 2, 2),
                        margin = new RectOffset(0, 0, 0, 0)
                    };
                }
                return s_minimalVertical;
            }
        }
    }
    
    /// <summary>
    /// Manages the state for constructing nested generic types.
    /// Uses a path-based approach to support infinite nesting depth.
    /// </summary>
    sealed class GenericConstructionState {
        readonly Dictionary<string, Type?[]> argumentCache = new();
        readonly Dictionary<string, int?> expandedArgument = new();
        
        public Type?[]? GetArguments(List<int> path) {
            var key = PathToKey(path);
            return argumentCache.TryGetValue(key, out var args) ? args : null;
        }
        
        public void SetArguments(List<int> path, Type?[] arguments) {
            var key = PathToKey(path);
            argumentCache[key] = arguments;
        }
        
        public int? GetExpandedIndex(List<int> path) {
            var key = PathToKey(path);
            return expandedArgument.TryGetValue(key, out var index) ? index : null;
        }
        
        public void SetExpandedIndex(List<int> path, int? index) {
            var key = PathToKey(path);
            if (index.HasValue) {
                expandedArgument[key] = index;
            }
            else {
                expandedArgument.Remove(key);
            }
        }
        
        public void ClearPath(List<int> path) {
            var key = PathToKey(path);
            argumentCache.Remove(key);
            expandedArgument.Remove(key);
            
            // Also clear all child paths
            var keyPrefix = string.Concat(key, "/");
            var keysToRemove = argumentCache.Keys
                .Where(k => k.StartsWith(keyPrefix))
                .ToList();
            foreach (var k in keysToRemove) {
                argumentCache.Remove(k);
                expandedArgument.Remove(k);
            }
        }
        
        public void ClearAll() {
            argumentCache.Clear();
            expandedArgument.Clear();
        }
        
        static string PathToKey(List<int> path) => string.Join("/", path);
    }
}
