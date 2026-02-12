namespace Hissal.UnityTypeSerializer.Editor {
    /// <summary>
    /// Severity level for a <see cref="GenericConstraintCheckResult"/>.
    /// </summary>
    internal enum ConstraintSeverity {
        /// <summary>No issue — candidate fully satisfies all constraints.</summary>
        None,

        /// <summary>
        /// Candidate may be shown but is not a valid final argument.
        /// Typically used for open generic definitions that require construction
        /// before they can satisfy constraints such as <c>new()</c>.
        /// </summary>
        Warning,

        /// <summary>Candidate does not satisfy one or more constraints and should be hidden.</summary>
        Error
    }

    /// <summary>
    /// Result of evaluating a candidate type against a generic parameter's constraints.
    /// Separates <em>visibility</em> (should the type appear in the dropdown) from
    /// <em>validity</em> (can it be used as a concrete generic argument).
    /// </summary>
    /// <remarks>
    /// Callers should interpret the result as follows:
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>ShowInDropdown == true &amp;&amp; IsValidArgument == true</c> — candidate is fully valid;
    ///     show in dropdown and allow selection/application.
    ///   </description></item>
    ///   <item><description>
    ///     <c>ShowInDropdown == true &amp;&amp; IsValidArgument == false</c> — candidate is visible
    ///     (e.g. an open generic that can be constructed) but not yet usable as a final argument.
    ///     The UI should allow picking it for construction but block applying/committing the selection.
    ///   </description></item>
    ///   <item><description>
    ///     <c>ShowInDropdown == false</c> — candidate does not satisfy the constraints; exclude
    ///     from the dropdown entirely.
    ///   </description></item>
    /// </list>
    /// </remarks>
    internal readonly struct GenericConstraintCheckResult {
        /// <summary>Whether the candidate should appear in a type-selection dropdown.</summary>
        public bool ShowInDropdown { get; init; }

        /// <summary>Whether the candidate is a valid final argument for the generic parameter.</summary>
        public bool IsValidArgument { get; init; }

        /// <summary>Optional human-readable description of why the candidate failed.</summary>
        public string? Message { get; init; }

        /// <summary>Severity of the constraint check outcome.</summary>
        public ConstraintSeverity Severity { get; init; }

        /// <summary>Creates a fully valid result (visible and valid).</summary>
        public static GenericConstraintCheckResult Valid => new() {
            ShowInDropdown = true,
            IsValidArgument = true,
            Severity = ConstraintSeverity.None
        };

        /// <summary>
        /// Creates a result where the candidate is hidden from the dropdown
        /// and is not a valid argument.
        /// </summary>
        public static GenericConstraintCheckResult Hidden(string message) => new() {
            ShowInDropdown = false,
            IsValidArgument = false,
            Message = message,
            Severity = ConstraintSeverity.Error
        };

        /// <summary>
        /// Creates a result where the candidate is shown in the dropdown
        /// (e.g. as a starting point for generic construction) but is not yet
        /// a valid concrete argument.
        /// </summary>
        public static GenericConstraintCheckResult VisibleButInvalid(string message) => new() {
            ShowInDropdown = true,
            IsValidArgument = false,
            Message = message,
            Severity = ConstraintSeverity.Warning
        };
    }
}
