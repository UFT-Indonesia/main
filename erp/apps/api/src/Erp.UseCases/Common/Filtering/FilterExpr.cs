using System.Linq.Expressions;

namespace Erp.UseCases.Common.Filtering;

/// <summary>
/// Expression plumbing shared by the field factories. Everything here splices an
/// <em>author-written</em> selector — <c>e =&gt; e.Nik.Value</c>, <c>e =&gt; e.MonthlyWage.Amount</c> —
/// into a predicate. Nothing looks a property up by name, which is the whole reason this design
/// was chosen over reflection: a value object or an owned type has no property EF could be
/// pointed at by string, and an unknown key can never reach the database because there is no
/// path from a string to a column.
/// </summary>
internal static class FilterExpr
{
    /// <summary>
    /// Rewrites <paramref name="predicate"/> so that its parameter becomes the body of
    /// <paramref name="selector"/>. Deliberately not <see cref="Expression.Invoke"/>: an
    /// invocation node survives into the query tree and EF cannot always translate it, whereas
    /// splicing produces the same plain expression the spec would have if it were hand-written.
    /// </summary>
    public static Expression<Func<T, bool>> Compose<T, TProp>(
        Expression<Func<T, TProp>> selector,
        Expression<Func<TProp, bool>> predicate)
    {
        var body = new ParameterReplacer(predicate.Parameters[0], selector.Body).Visit(predicate.Body)!;
        return Expression.Lambda<Func<T, bool>>(body, selector.Parameters[0]);
    }

    /// <summary>
    /// A binary comparison against the selected member, for the types C# cannot express
    /// generically — an unconstrained numeric, an enum, a strongly-typed id.
    /// </summary>
    public static Expression<Func<T, bool>> Compare<T, TProp>(
        Expression<Func<T, TProp>> selector,
        ExpressionType kind,
        TProp value)
    {
        var body = Expression.MakeBinary(kind, selector.Body, Captured(value));
        return Expression.Lambda<Func<T, bool>>(body, selector.Parameters[0]);
    }

    /// <summary>
    /// Wraps a value so EF sees a captured variable rather than a literal, which keeps the
    /// generated SQL parameterised and the query plan reusable across different filter values.
    /// </summary>
    private static Expression Captured<TValue>(TValue value)
    {
        var box = new Box<TValue> { Value = value };
        return Expression.Field(Expression.Constant(box), nameof(Box<TValue>.Value));
    }

    private sealed class Box<TValue>
    {
        public TValue Value = default!;
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly Expression _to;

        public ParameterReplacer(ParameterExpression from, Expression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _from ? _to : base.VisitParameter(node);
    }
}

internal static class FilterExprCombine
{
    /// <summary>
    /// Joins two predicates built from the <em>same</em> selector, which is how "between"
    /// becomes a single lambda. Both sides already share a parameter instance because they were
    /// built from one selector, so no rewriting is needed.
    /// </summary>
    public static Expression<Func<T, bool>> And<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
        => Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(left.Body, right.Body),
            left.Parameters[0]);
}
