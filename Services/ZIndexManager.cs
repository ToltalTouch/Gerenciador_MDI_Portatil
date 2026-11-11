using Avalonia.Controls;

namespace AvaloniaMdiApp
{
    public static class ZIndexManager
    {
        private static int _maxZIndex = 1;
        private static DraggableWindow? _activeWindow;

        public static DraggableWindow? ActiveWindow => _activeWindow;

        public static void BringToFront(Control window)
        {
            _maxZIndex++;
            window.SetValue(Panel.ZIndexProperty, _maxZIndex);

            if (window is DraggableWindow dw && _activeWindow != dw)
            {
                _activeWindow?.SetActive(false);
                _activeWindow = dw;
                _activeWindow.SetActive(true);
            }
        }
    }
}
