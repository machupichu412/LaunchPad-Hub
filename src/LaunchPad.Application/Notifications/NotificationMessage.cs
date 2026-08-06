namespace LaunchPad.Application.Notifications;

/// <summary>
/// The on-the-wire contract between the API (producer, builds the fully-formed subject
/// and body) and the Functions notification consumer (thin relay to Graph). Kept dumb on
/// purpose — recipient/template logic stays in the API layer where it's easy to read and
/// test, not duplicated into the Function.
/// </summary>
public sealed record NotificationMessage(string ToUpn, string Subject, string Body);
