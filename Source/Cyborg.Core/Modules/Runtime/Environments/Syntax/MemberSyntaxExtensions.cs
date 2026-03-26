using System.Runtime.CompilerServices;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class MemberSyntaxExtensions
{
    extension<T> (T syntax) where T : struct, IChildSyntaxProvider<T>
    {
        public T Member(string memberName) => syntax.Child(syntax.NamingPolicy.ConvertName(VariableSyntaxHelpers.NormalizeMemberName(memberName, nameof(memberName))));

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "CallerArgumentExpression is used to capture the property name.")]
        public T Property<TProperty>(TProperty propertyExpression, [CallerArgumentExpression(nameof(propertyExpression))] string? propertyName = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            int dotIndex = propertyName.LastIndexOf('.');
            string memberName = dotIndex >= 0 ? propertyName[(dotIndex + 1)..] : propertyName;
            return syntax.Member(memberName);
        }
    }
}