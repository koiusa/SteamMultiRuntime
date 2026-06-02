namespace Koiusa.SteamMultiRuntime
{
    public interface ITraversalSettingsSync
    {
        void WriteSettings(ref TraversalFeatureSettings settings);
        void ReadSettings(TraversalFeatureSettings settings);
    }
}
