namespace Jint.Browser.Dom;

// WebIDL string-enumeration conversion values for the retained binding contract.
// These types carry no document, scrolling, or media state. Their numeric values
// retain the extracted contract's ordering, including its unknown-value formatting.
internal enum DomScrollBehavior
{
    Auto = 0,
    Instant = 1,
    Smooth = 2,
}

internal enum DomScrollLogicalPosition
{
    Start = 0,
    End = 1,
}

internal enum DomAdjacentPosition : byte
{
    BeforeBegin = 0,
    AfterBegin = 1,
    BeforeEnd = 2,
    AfterEnd = 3,
}

internal enum DomDocumentReadyState : byte
{
    Loading = 0,
    Interactive = 1,
    Complete = 2,
}

internal enum DomMediaControllerPlaybackState : byte
{
    Waiting = 0,
    Playing = 1,
    Ended = 2,
}

internal enum DomTextTrackMode : byte
{
    Disabled = 0,
    Hidden = 1,
    Showing = 2,
}
