namespace Mossvale.Farming
{
    /// <summary>
    /// Implemented by any component whose state is saved. SaveManager finds every ISaveable in the scene,
    /// stores CaptureState() under SaveKey, and calls RestoreState() in ascending RestoreOrder on load.
    /// </summary>
    public interface ISaveable
    {
        string SaveKey { get; }
        int RestoreOrder { get; }
        string CaptureState();
        void RestoreState(string json);
    }
}
