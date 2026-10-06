namespace Jint.HtmlParser;

public abstract partial class Node
{
    private List<NodeMutationRegistration>? RegistrationList
    {
        get => _rare?.MutationRegistrations;
        set { if (value is not null || _rare is not null) Rare.MutationRegistrations = value; }
    }

    internal IReadOnlyList<NodeMutationRegistration>? MutationRegistrations => _rare?.MutationRegistrations;

    internal void AddMutationRegistration(MutationRegistration registration, bool transient)
    {
        RegistrationList ??= [];
        foreach (var entry in RegistrationList)
        {
            if (ReferenceEquals(entry.Registration, registration) && entry.Transient == transient)
            {
                return;
            }
        }

        RegistrationList.Add(new NodeMutationRegistration(registration, transient));
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
        RegistrationList?.RemoveAll(entry =>
            ReferenceEquals(entry.Registration, registration) && entry.Transient == transient);
        if (RegistrationList is { Count: 0 })
        {
            RegistrationList = null;
        }
    }

    internal void RemoveTransientRegistrations(MutationSubscription subscription, MutationRegistration? source)
    {
        RegistrationList?.RemoveAll(entry => entry.Transient &&
            ReferenceEquals(entry.Registration.Subscription, subscription) &&
            (source is null || ReferenceEquals(entry.Registration, source)));
        if (RegistrationList is { Count: 0 })
        {
            RegistrationList = null;
        }
    }

    internal bool HasTransientRegistration(MutationSubscription subscription)
        => RegistrationList?.Exists(entry => entry.Transient &&
            ReferenceEquals(entry.Registration.Subscription, subscription)) == true;
}

internal readonly record struct NodeMutationRegistration(MutationRegistration Registration, bool Transient);
