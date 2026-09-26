namespace Jint.HtmlParser;

public abstract partial class Node
{
    private List<NodeMutationRegistration>? _mutationRegistrations;

    internal IReadOnlyList<NodeMutationRegistration>? MutationRegistrations => _mutationRegistrations;

    internal void AddMutationRegistration(MutationRegistration registration, bool transient)
    {
        _mutationRegistrations ??= [];
        foreach (var entry in _mutationRegistrations)
        {
            if (ReferenceEquals(entry.Registration, registration) && entry.Transient == transient)
            {
                return;
            }
        }

        _mutationRegistrations.Add(new NodeMutationRegistration(registration, transient));
        var document = this as Document ?? OwnerDocument!;
        document.MarkMutationRegistrationsPresent();
        if (registration.Subscription.CaptureHtmlMetaInsertions) document.MarkHtmlMetaCapturePresent();
        if (transient)
        {
            registration.Subscription.AddTransientNode(this);
        }
    }

    internal void RemoveMutationRegistration(MutationRegistration registration, bool transient)
    {
        _mutationRegistrations?.RemoveAll(entry =>
            ReferenceEquals(entry.Registration, registration) && entry.Transient == transient);
        if (_mutationRegistrations is { Count: 0 })
        {
            _mutationRegistrations = null;
        }
    }

    internal void RemoveTransientRegistrations(MutationSubscription subscription, MutationRegistration? source)
    {
        _mutationRegistrations?.RemoveAll(entry => entry.Transient &&
            ReferenceEquals(entry.Registration.Subscription, subscription) &&
            (source is null || ReferenceEquals(entry.Registration, source)));
        if (_mutationRegistrations is { Count: 0 })
        {
            _mutationRegistrations = null;
        }
    }

    internal bool HasTransientRegistration(MutationSubscription subscription)
        => _mutationRegistrations?.Exists(entry => entry.Transient &&
            ReferenceEquals(entry.Registration.Subscription, subscription)) == true;
}

internal readonly record struct NodeMutationRegistration(MutationRegistration Registration, bool Transient);
