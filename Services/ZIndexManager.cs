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

            if (window is DraggableWindow dw)
            {
                if (_activeWindow != dw)
                {
                    var previous = _activeWindow;
                    _activeWindow = dw;
                    previous?.SetActive(false);
                    _activeWindow.SetActive(true);
                }
                else
                {
                    _activeWindow.SetActive(true);
                }
            }
        }

        public static void ClearActive(DraggableWindow? window = null)
        {
            if (window == null || _activeWindow == window)
            {
                var previous = _activeWindow;
                _activeWindow = null;
                previous?.SetActive(false);
            }
        }
    }
}
