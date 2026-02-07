# TypeRefDrawer Layout Group Bugfix

## Issues Fixed

### 1. BeginLayoutGroup/EndLayoutGroup Mismatch Error
**Problem**: When clicking "▶ Construct" on a generic type argument (e.g., T1 in `MegaWrapper3<T1, T2, T3>`), the console showed:
```
EndLayoutGroup: BeginLayoutGroup must be called first.
```
And the inspector became broken/non-functional.

**Root Cause**: In the `DrawGenericConstructorRecursive` method, when drawing a nested constructor:
- Line 169: `EditorGUILayout.BeginVertical()` was called if `isNested = true`
- Line 264: `EditorGUILayout.EndVertical()` was called inside the argument loop when an expanded argument finished
- Line 301: `EditorGUILayout.EndVertical()` was called again at the end of the method

This caused a double-End for the same Begin, breaking Unity's layout group stack.

**Solution**: Removed the premature `EditorGUILayout.EndVertical()` call at line 264 (inside the loop). Now the vertical group is only ended once at the end of the method, properly matching the Begin call.

### 2. Excessive Vertical Spacing
**Problem**: Nested type pickers were drawn very far apart vertically, making the UI look spread out and harder to use.

**Root Cause**: Multiple `EditorGUILayout.Space()` calls with values of 3 and 5 pixels were adding up across nested levels.

**Solution**: Reduced spacing from:
- `EditorGUILayout.Space(3)` → `EditorGUILayout.Space(2)`
- `EditorGUILayout.Space(5)` → (removed entirely)

This makes the nested constructor UI more compact and easier to read.

## Changes Made

**File**: `TypeRefDrawer.cs`

**Modified Lines**:
- **Line 217**: Removed unused `hasDrawnNestedConstructor` variable declaration
- **Line 230**: Changed `EditorGUILayout.Space(3)` to `EditorGUILayout.Space(2)`
- **Line 257**: Removed `EditorGUILayout.Space(5)` 
- **Line 259-262**: Removed the conditional `EditorGUILayout.EndVertical()` block that was causing layout mismatch
- **Line 261**: Removed unused `hasDrawnNestedConstructor = true` assignment

## Testing
After these changes, you should be able to:
1. ✅ Click "▶ Construct" on any generic type argument without console errors
2. ✅ See nested constructors displayed with proper spacing (not excessively far apart)
3. ✅ Navigate through multiple levels of nested generic construction without inspector breakage
4. ✅ Use Apply/Cancel buttons on nested constructors without issues

## Example Working Flow
```
MegaWrapper3<T1, T2, T3>
  where T1 : IDamageEffect
  
1. Click "▶ Construct" on T1
2. Select ElementalDamage<T> 
3. Click "▶ Construct" on T
4. Select FireElement
5. Click "Apply" to collapse back
6. Click "Apply" again to finalize
7. Result: MegaWrapper3<ElementalDamage<FireElement>, T2, T3>
```

All without errors! 🎉
