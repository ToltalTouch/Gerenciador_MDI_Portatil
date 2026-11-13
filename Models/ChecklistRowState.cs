using Avalonia.Controls;

namespace AvaloniaMdiApp
{
    public class ChecklistRowState
    {
        public string RowId { get; init; } = System.Guid.NewGuid().ToString("N");
        public CheckBox Check { get; init; } = null!;
        public TextBox Input { get; init; } = null!;
        public Border StrikeLine { get; init; } = null!;
        public Border RowBorder { get; init; } = null!;
        public Button BtnNote { get; init; } = null!;
        public Border? ColorDot { get; init; }
        public Button? BtnLinkWindow { get; init; }
        public string ColorHex { get; set; } = string.Empty;
        public string LinkedWindowId { get; set; } = string.Empty;
        public string LinkedNoteContent { get; set; } = string.Empty;
        public TextFormatData LinkedNoteFormat { get; set; } = new();
        public DraggableWindow? OpenNoteWindow { get; set; }
    }
}
