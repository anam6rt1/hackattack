namespace MarkAi.Data.Servicii;

/// <summary>
/// The request is valid but clashes with the current state (a duplicate, a full session, a call in progress).
/// Becomes a 409 whose message is shown to the user, so it must be written for them, in Romanian.
/// Any other exception becomes a generic 500, so internal messages never reach the client.
/// </summary>
public class Conflict(string mesaj) : Exception(mesaj);
