using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvaloniaMdiApp
{
    public partial class DraggableWindow
    {
        private sealed class RichLineState
        {
            public RichTextLineData Data { get; set; } = new();
            public Grid Container { get; set; } = null!;
            public TextBox Editor { get; set; } = null!;
            public SelectableTextBlock RichPreview { get; set; } = null!;
            public int LastSelStart { get; set; }
            public int LastSelEnd { get; set; }
            public bool SuppressTextSync { get; set; }

            public string GetPlainText() => Editor.Text ?? string.Empty;
        }

        private StackPanel? _richLinesPanel;
        private readonly List<RichLineState> _richLines = new();
        private RichLineState? _activeRichLine;

        private Control CriarConteudoBlocoNotas(string initialNote, List<RichTextLineData>? initialRichLines, TextFormatData? initialFormat)
        {
            _richLinesPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2,
                Margin = new Thickness(10)
            };

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = _richLinesPanel,
                Background = Brushes.Transparent
            };

            scroll.PointerPressed += (s, e) =>
            {
                if (e.Source == scroll || e.Source == _richLinesPanel)
                {
                    var last = _richLines.LastOrDefault();
                    if (last != null)
                    {
                        AtivarEdicaoLinha(last, last.GetPlainText().Length);
                    }
                }
            };

            if (initialRichLines != null && initialRichLines.Count > 0)
            {
                foreach (var lineData in initialRichLines)
                {
                    AdicionarLinhaRica(CloneLineData(lineData), focus: false);
                }
            }
            else
            {
                var baseFmt = initialFormat ?? new TextFormatData();
                var rawLines = (initialNote ?? string.Empty).Replace("\r\n", "\n").Split('\n');
                foreach (var raw in rawLines)
                {
                    AdicionarLinhaRica(new RichTextLineData
                    {
                        Alignment = baseFmt.Alignment ?? "left",
                        Spans = new List<RichTextSpanData>
                        {
                            new()
                            {
                                Text = raw,
                                IsBold = baseFmt.IsBold,
                                IsItalic = baseFmt.IsItalic,
                                FontSize = baseFmt.FontSize > 0 ? baseFmt.FontSize : 13,
                                FontFamilyName = baseFmt.FontFamilyName ?? "default"
                            }
                        }
                    }, focus: false);
                }
            }

            if (_richLines.Count == 0)
            {
                AdicionarLinhaRica(CriarLinhaPadrao(""), focus: false);
            }

            _activeRichLine = _richLines[0];
            AtualizarToolbarParaLinhaAtiva();
            return scroll;
        }

        private static RichTextLineData CriarLinhaPadrao(string text, RichTextSpanData? template = null, string alignment = "left")
        {
            return new RichTextLineData
            {
                Alignment = alignment,
                Spans = new List<RichTextSpanData>
                {
                    new()
                    {
                        Text = text,
                        IsBold = template?.IsBold ?? false,
                        IsItalic = template?.IsItalic ?? false,
                        FontSize = template != null && template.FontSize > 0 ? template.FontSize : 13,
                        FontFamilyName = template?.FontFamilyName ?? "default"
                    }
                }
            };
        }

        private static RichTextLineData CloneLineData(RichTextLineData src)
        {
            var clone = new RichTextLineData
            {
                Alignment = string.IsNullOrWhiteSpace(src.Alignment) ? "left" : src.Alignment,
                Spans = new List<RichTextSpanData>()
            };
            if (src.Spans != null && src.Spans.Count > 0)
            {
                foreach (var sp in src.Spans)
                {
                    clone.Spans.Add(new RichTextSpanData
                    {
                        Text = sp.Text ?? string.Empty,
                        IsBold = sp.IsBold,
                        IsItalic = sp.IsItalic,
                        FontSize = sp.FontSize > 0 ? sp.FontSize : 13,
                        FontFamilyName = string.IsNullOrWhiteSpace(sp.FontFamilyName) ? "default" : sp.FontFamilyName
                    });
                }
            }
            else
            {
                clone.Spans.Add(new RichTextSpanData());
            }
            return clone;
        }

        private RichLineState AdicionarLinhaRica(RichTextLineData lineData, int insertIndex = -1, bool focus = false, int caretIndex = 0)
        {
            if (_richLinesPanel == null) throw new InvalidOperationException("RichLinesPanel não inicializado.");
            if (lineData.Spans.Count == 0)
            {
                lineData.Spans.Add(new RichTextSpanData());
            }

            string plainText = string.Concat(lineData.Spans.Select(s => s.Text));
            var editor = new TextBox
            {
                Text = plainText,
                Watermark = _richLines.Count == 0 ? "Digite suas anotações aqui..." : string.Empty,
                AcceptsReturn = false,
                TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(2, 2),
                MinHeight = 20,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var preview = new SelectableTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(2, 2),
                MinHeight = 20,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Ibeam)
            };

            var container = new Grid();
            container.Children.Add(editor);
            container.Children.Add(preview);

            var state = new RichLineState
            {
                Data = lineData,
                Container = container,
                Editor = editor,
                RichPreview = preview
            };

            editor.GotFocus += (s, e) =>
            {
                _activeRichLine = state;
                AtualizarToolbarParaLinhaAtiva();
            };

            editor.PropertyChanged += (s, e) =>
            {
                if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty)
                {
                    if (editor.IsFocused)
                    {
                        state.LastSelStart = Math.Min(editor.SelectionStart, editor.SelectionEnd);
                        state.LastSelEnd = Math.Max(editor.SelectionStart, editor.SelectionEnd);
                    }
                }
            };

            editor.TextChanged += (s, e) =>
            {
                if (state.SuppressTextSync) return;
                SincronizarTextoParaSpans(state, editor.Text ?? string.Empty);
                state.LastSelStart = Math.Min(editor.SelectionStart, editor.SelectionEnd);
                state.LastSelEnd = Math.Max(editor.SelectionStart, editor.SelectionEnd);
            };

            editor.LostFocus += (s, e) =>
            {
                if (state.Data.Spans.Count > 1)
                {
                    RenderizarLinhaRica(state, forcePreview: true);
                }
            };

            preview.PointerPressed += (s, e) =>
            {
                AtivarEdicaoLinha(state, state.GetPlainText().Length);
            };

            editor.KeyDown += (s, e) =>
            {
                int idx = _richLines.IndexOf(state);
                if (idx < 0) return;

                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    string txt = editor.Text ?? string.Empty;
                    int caret = Math.Clamp(editor.CaretIndex, 0, txt.Length);
                    var (leftSpans, rightSpans) = DividirSpansEm(state.Data.Spans, caret);

                    state.SuppressTextSync = true;
                    state.Data.Spans = leftSpans;
                    editor.Text = string.Concat(leftSpans.Select(sp => sp.Text));
                    state.SuppressTextSync = false;
                    RenderizarLinhaRica(state, forcePreview: false);

                    var newLineData = new RichTextLineData
                    {
                        Alignment = state.Data.Alignment,
                        Spans = rightSpans
                    };
                    AdicionarLinhaRica(newLineData, idx + 1, focus: true, caretIndex: 0);
                }
                else if (e.Key == Key.Back && editor.CaretIndex == 0 && editor.SelectionStart == editor.SelectionEnd && idx > 0)
                {
                    e.Handled = true;
                    var prev = _richLines[idx - 1];
                    int prevLen = prev.GetPlainText().Length;
                    prev.Data.Spans = MesclarSpans(prev.Data.Spans, state.Data.Spans);

                    _richLines.RemoveAt(idx);
                    _richLinesPanel.Children.Remove(state.Container);

                    prev.SuppressTextSync = true;
                    prev.Editor.Text = string.Concat(prev.Data.Spans.Select(sp => sp.Text));
                    prev.SuppressTextSync = false;
                    AtivarEdicaoLinha(prev, prevLen);
                }
                else if (e.Key == Key.Up && idx > 0)
                {
                    e.Handled = true;
                    var prev = _richLines[idx - 1];
                    AtivarEdicaoLinha(prev, Math.Min(editor.CaretIndex, prev.GetPlainText().Length));
                }
                else if (e.Key == Key.Down && idx < _richLines.Count - 1)
                {
                    e.Handled = true;
                    var next = _richLines[idx + 1];
                    AtivarEdicaoLinha(next, Math.Min(editor.CaretIndex, next.GetPlainText().Length));
                }
            };

            if (insertIndex >= 0 && insertIndex <= _richLines.Count)
            {
                _richLines.Insert(insertIndex, state);
                _richLinesPanel.Children.Insert(insertIndex, container);
            }
            else
            {
                _richLines.Add(state);
                _richLinesPanel.Children.Add(container);
            }

            RenderizarLinhaRica(state, forcePreview: !focus && state.Data.Spans.Count > 1);

            if (focus)
            {
                AtivarEdicaoLinha(state, caretIndex);
            }

            return state;
        }

        private void AtivarEdicaoLinha(RichLineState state, int caretIndex)
        {
            _activeRichLine = state;
            state.RichPreview.IsVisible = false;
            state.Editor.IsVisible = true;
            state.Editor.Focus();
            int len = state.GetPlainText().Length;
            state.Editor.CaretIndex = Math.Clamp(caretIndex, 0, len);
            AtualizarToolbarParaLinhaAtiva();
        }

        private void RenderizarLinhaRica(RichLineState state, bool forcePreview)
        {
            var align = state.Data.Alignment == "center"
                ? TextAlignment.Center
                : state.Data.Alignment == "right"
                    ? TextAlignment.Right
                    : TextAlignment.Left;

            var fg = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;

            if (state.Data.Spans.Count <= 1 || !forcePreview)
            {
                var first = state.Data.Spans.FirstOrDefault() ?? new RichTextSpanData();
                state.Editor.FontWeight = first.IsBold ? FontWeight.Bold : FontWeight.Normal;
                state.Editor.FontStyle = first.IsItalic ? FontStyle.Italic : FontStyle.Normal;
                state.Editor.FontSize = first.FontSize > 0 ? first.FontSize : 13;
                state.Editor.FontFamily = ObterFontFamily(first.FontFamilyName);
                state.Editor.TextAlignment = align;
                state.Editor.Foreground = fg;
                state.Editor.IsVisible = true;
                state.RichPreview.IsVisible = false;
            }

            if (state.Data.Spans.Count > 1 && forcePreview)
            {
                state.RichPreview.Inlines?.Clear();
                state.RichPreview.Inlines ??= new InlineCollection();
                state.RichPreview.TextAlignment = align;
                state.RichPreview.Foreground = fg;

                foreach (var sp in state.Data.Spans)
                {
                    state.RichPreview.Inlines.Add(new Run(sp.Text)
                    {
                        FontWeight = sp.IsBold ? FontWeight.Bold : FontWeight.Normal,
                        FontStyle = sp.IsItalic ? FontStyle.Italic : FontStyle.Normal,
                        FontSize = sp.FontSize > 0 ? sp.FontSize : 13,
                        FontFamily = ObterFontFamily(sp.FontFamilyName),
                        Foreground = fg
                    });
                }

                state.Editor.IsVisible = false;
                state.RichPreview.IsVisible = true;
            }
        }

        private void AplicarFormatacaoNaLinhaOuSelecao(Action<RichTextSpanData> mutator, string? newAlignment = null)
        {
            var target = _activeRichLine ?? _richLines.FirstOrDefault();
            if (target == null) return;

            if (newAlignment != null)
            {
                target.Data.Alignment = newAlignment;
                RenderizarLinhaRica(target, forcePreview: !target.Editor.IsFocused && target.Data.Spans.Count > 1);
                AtualizarToolbarParaLinhaAtiva();
                return;
            }

            string txt = target.GetPlainText();
            int selStart = target.LastSelStart;
            int selEnd = target.LastSelEnd;
            if (target.Editor.IsFocused)
            {
                selStart = Math.Min(target.Editor.SelectionStart, target.Editor.SelectionEnd);
                selEnd = Math.Max(target.Editor.SelectionStart, target.Editor.SelectionEnd);
            }

            selStart = Math.Clamp(selStart, 0, txt.Length);
            selEnd = Math.Clamp(selEnd, 0, txt.Length);

            if (selStart == selEnd || (selStart == 0 && selEnd == txt.Length))
            {
                foreach (var sp in target.Data.Spans)
                {
                    mutator(sp);
                }
                target.Data.Spans = CompactarSpans(target.Data.Spans);
                RenderizarLinhaRica(target, forcePreview: target.Data.Spans.Count > 1);
            }
            else
            {
                var (before, rest) = DividirSpansEm(target.Data.Spans, selStart);
                var (middle, after) = DividirSpansEm(rest, selEnd - selStart);
                foreach (var sp in middle)
                {
                    mutator(sp);
                }
                var combined = new List<RichTextSpanData>();
                combined.AddRange(before);
                combined.AddRange(middle);
                combined.AddRange(after);
                target.Data.Spans = CompactarSpans(combined);
                RenderizarLinhaRica(target, forcePreview: target.Data.Spans.Count > 1);
            }

            AtualizarToolbarParaLinhaAtiva();
        }

        private void AtualizarToolbarParaLinhaAtiva()
        {
            var target = _activeRichLine ?? _richLines.FirstOrDefault();
            if (target == null) return;

            var refSpan = target.Data.Spans.FirstOrDefault() ?? new RichTextSpanData();
            _isBold = refSpan.IsBold;
            _isItalic = refSpan.IsItalic;
            _fontSize = refSpan.FontSize > 0 ? refSpan.FontSize : 13;
            _alignment = target.Data.Alignment ?? "left";
            _fontFamilyName = refSpan.FontFamilyName ?? "default";

            if (_lblFontSize != null)
            {
                _lblFontSize.Text = $"{(int)_fontSize}pt";
            }
        }

        private static void SincronizarTextoParaSpans(RichLineState state, string newText)
        {
            string oldText = string.Concat(state.Data.Spans.Select(s => s.Text));
            if (oldText == newText) return;

            if (state.Data.Spans.Count <= 1)
            {
                var sp = state.Data.Spans.FirstOrDefault() ?? new RichTextSpanData();
                sp.Text = newText;
                state.Data.Spans = new List<RichTextSpanData> { sp };
                return;
            }

            int prefix = 0;
            int minLen = Math.Min(oldText.Length, newText.Length);
            while (prefix < minLen && oldText[prefix] == newText[prefix]) prefix++;

            int suffix = 0;
            while (suffix < (minLen - prefix) &&
                   oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
            {
                suffix++;
            }

            int oldMidEnd = oldText.Length - suffix;
            string inserted = newText.Substring(prefix, newText.Length - prefix - suffix);

            var (before, rest) = DividirSpansEm(state.Data.Spans, prefix);
            var (_, after) = DividirSpansEm(rest, oldMidEnd - prefix);

            var template = before.LastOrDefault() ?? rest.FirstOrDefault() ?? after.FirstOrDefault() ?? new RichTextSpanData();
            var result = new List<RichTextSpanData>();
            result.AddRange(before);
            if (!string.IsNullOrEmpty(inserted))
            {
                result.Add(new RichTextSpanData
                {
                    Text = inserted,
                    IsBold = template.IsBold,
                    IsItalic = template.IsItalic,
                    FontSize = template.FontSize,
                    FontFamilyName = template.FontFamilyName
                });
            }
            result.AddRange(after);
            state.Data.Spans = CompactarSpans(result);
        }

        private static (List<RichTextSpanData> Left, List<RichTextSpanData> Right) DividirSpansEm(List<RichTextSpanData> spans, int index)
        {
            var left = new List<RichTextSpanData>();
            var right = new List<RichTextSpanData>();
            int pos = 0;

            foreach (var sp in spans)
            {
                string t = sp.Text ?? string.Empty;
                int end = pos + t.Length;

                if (end <= index)
                {
                    left.Add(CloneSpan(sp, t));
                }
                else if (pos >= index)
                {
                    right.Add(CloneSpan(sp, t));
                }
                else
                {
                    int splitAt = index - pos;
                    if (splitAt > 0) left.Add(CloneSpan(sp, t.Substring(0, splitAt)));
                    if (splitAt < t.Length) right.Add(CloneSpan(sp, t.Substring(splitAt)));
                }

                pos = end;
            }

            var template = spans.FirstOrDefault() ?? new RichTextSpanData();
            if (left.Count == 0) left.Add(CloneSpan(template, string.Empty));
            if (right.Count == 0) right.Add(CloneSpan(spans.LastOrDefault() ?? template, string.Empty));
            return (CompactarSpans(left), CompactarSpans(right));
        }

        private static List<RichTextSpanData> MesclarSpans(List<RichTextSpanData> a, List<RichTextSpanData> b)
        {
            var list = new List<RichTextSpanData>();
            list.AddRange(a.Select(s => CloneSpan(s, s.Text)));
            list.AddRange(b.Select(s => CloneSpan(s, s.Text)));
            return CompactarSpans(list);
        }

        private static List<RichTextSpanData> CompactarSpans(List<RichTextSpanData> spans)
        {
            var result = new List<RichTextSpanData>();
            foreach (var sp in spans)
            {
                if (string.IsNullOrEmpty(sp.Text)) continue;
                if (result.Count > 0)
                {
                    var prev = result[result.Count - 1];
                    if (prev.IsBold == sp.IsBold &&
                        prev.IsItalic == sp.IsItalic &&
                        Math.Abs(prev.FontSize - sp.FontSize) < 0.1 &&
                        prev.FontFamilyName == sp.FontFamilyName)
                    {
                        prev.Text += sp.Text;
                        continue;
                    }
                }
                result.Add(CloneSpan(sp, sp.Text));
            }
            if (result.Count == 0)
            {
                var first = spans.FirstOrDefault() ?? new RichTextSpanData();
                result.Add(CloneSpan(first, string.Empty));
            }
            return result;
        }

        private static RichTextSpanData CloneSpan(RichTextSpanData s, string text) => new()
        {
            Text = text,
            IsBold = s.IsBold,
            IsItalic = s.IsItalic,
            FontSize = s.FontSize > 0 ? s.FontSize : 13,
            FontFamilyName = string.IsNullOrWhiteSpace(s.FontFamilyName) ? "default" : s.FontFamilyName
        };

        public List<RichTextLineData> ExportRichLines()
        {
            return _richLines.Select(l => CloneLineData(l.Data)).ToList();
        }

        private void AtualizarTemaRichEditor()
        {
            foreach (var line in _richLines)
            {
                RenderizarLinhaRica(line, forcePreview: !line.Editor.IsVisible);
            }
        }
    }
}
