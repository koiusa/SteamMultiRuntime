namespace Koiusa.UI.Core
{
    public interface IUiMenu
    {
        bool IsVisible { get; }
        void Activate();
        void Deactivate();
        void FocusInitial();
    }
}
