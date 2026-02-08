using System;

// ReSharper disable once CheckNamespace
namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
internal sealed class MemberNotNullWhenAttribute : Attribute {
    public MemberNotNullWhenAttribute(bool returnValue, string member) {
        ReturnValue = returnValue;
        Members = new[] { member };
    }

    public MemberNotNullWhenAttribute(bool returnValue, params string[] members) {
        ReturnValue = returnValue;
        Members = members;
    }

    public bool ReturnValue { get; }
    public string[] Members { get; }
}