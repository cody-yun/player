namespace LivLiv.Platform
{
    // Optional device-adapter status. Query on the adapter's owning (Unity main) thread.
    // A terminal failure releases owned capture resources; read its error before retiring the adapter.
    // Silence, no fresh frame, and temporary mute are not terminal failures.
    public interface ICaptureStatus
    {
        string Error { get; }
    }
}
