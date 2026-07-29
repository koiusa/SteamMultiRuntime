namespace Koiusa.UI.Common
{
    public interface IUiMenu
    {
        bool IsVisible { get; }
        void Activate();
        void Deactivate();
        void FocusInitial();
    }
}
