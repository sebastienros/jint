#nullable enable
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.References;

public sealed class CssQueryBindingSnapshotTests
{
    [Test]
    public void QueryLayersAreExplicitlyAffineAndFrozenFactoriesRejectTheirRetention()
    {
        var work = new CssValueWork(default);
        var resolver = new Resolver();
        var query = CssSubstitutionSnapshot.CreateQueryLayer(null, resolver);
        resolver.Calls.Should().Be(0);
        query.TryGet("--x", work, out var binding).Should().BeTrue();
        binding.Scope.Should().BeSameAs(query);
        resolver.Calls.Should().Be(1);
        Assert.Throws<ArgumentException>(() => CssSubstitutionSnapshot.CreateLayer([], query, work));
        Assert.Throws<ArgumentException>(() => CssSubstitutionSnapshot.Create([binding], work));
    }

    [Test]
    public void MissingChildBindingRetainsTheActualParentDefiningScope()
    {
        var work = new CssValueWork(default);
        var parent = CssSubstitutionSnapshot.CreateQueryLayer(null, new Resolver());
        var child = CssSubstitutionSnapshot.CreateQueryLayer(parent, new Resolver { Present = false });
        child.TryGet("--x", work, out var binding).Should().BeTrue();
        binding.Scope.Should().BeSameAs(parent);
    }

    private sealed class Resolver : ICssQueryBindingResolver
    {
        internal int Calls;
        internal bool Present = true;
        public bool TryResolve(string name, CssValueWork work, out CssSubstitutionBinding binding)
        {
            work.CheckCancellation();
            Calls++;
            binding = Present ? CssSubstitutionBinding.Invalid(name, false) : default;
            return Present;
        }
    }
}
