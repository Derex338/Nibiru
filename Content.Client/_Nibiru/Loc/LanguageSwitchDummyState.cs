using Robust.Client.State;

namespace Content.Client.Localization
{
    /// <summary>
    /// A temporary dummy state used during language switching to safely reload the actual active state UI.
    /// </summary>
    public sealed class LanguageSwitchDummyState : State
    {
        protected override void Startup() { }
        protected override void Shutdown() { }
    }
}
