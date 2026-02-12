namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Defines which drawer mode to use for displaying and editing a <see cref="SerializedType{TBase}"/> field.
    /// </summary>
    public enum SerializedTypeDrawerMode {
        /// <summary>
        /// Uses the inline one-line drawer mode with multiple dropdowns.
        /// This is the default and recommended mode for most use cases.
        /// Shows the type and its generic arguments in a compact horizontal layout.
        /// </summary>
        Inline = 0,
        
        /// <summary>
        /// Uses the step-by-step constructor UI for generic types.
        /// Shows nested expandable sections for constructing complex generic types.
        /// Useful for deeply nested generic type construction scenarios.
        /// </summary>
        Constructor = 1
    }
}
