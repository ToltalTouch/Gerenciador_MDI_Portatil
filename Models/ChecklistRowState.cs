using Avalonia.Controls;

namespace AvaloniaMdiApp
{
    public class ChecklistRowState
    {
        public CheckBox Check { get; init; } = null!;
        public TextBox Input { get; init; } = null!;
        public Border StrikeLine { get; init; } = null!;
        public Border RowBorder { get; init; } = null!;
        public Button BtnNote { get; init; } = null!;
        public string LinkedNoteContent { get; set; } = string.Empty;
        public TextFormatData LinkedNoteFormat { get; set; } = new();
        public DraggableWindow? OpenNoteWindow { get; set; }
    }
}
