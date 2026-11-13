using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace AvaloniaMdiApp
{
    /// <summary>
    /// Serviço responsável por extrair demonstrações do Workspace (PDF, PNG, HTML)
    /// e exportar/importar arquivos, janelas e pacotes portáteis (.mdiws / .zip).
    /// </summary>
    public static class WorkspaceExportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        // ==================== CAPTURA VISUAL DO WORKSPACE ====================

        internal static RenderTargetBitmap? CapturarWorkspaceBitmap(Canvas workspace, out int pixelWidth, out int pixelHeight)
        {
            pixelWidth = 0;
            pixelHeight = 0;

            try
            {
                double w = workspace.Bounds.Width > 10 ? workspace.Bounds.Width : workspace.Width;
                double h = workspace.Bounds.Height > 10 ? workspace.Bounds.Height : workspace.Height;
                if (double.IsNaN(w) || w <= 0) w = 1280;
                if (double.IsNaN(h) || h <= 0) h = 720;

                // Limita dimensões máximas para manter uso baixo de RAM e PDF rápido
                double maxDim = 2400.0;
                double scale = 1.0;
                if (w > maxDim || h > maxDim)
                {
                    scale = Math.Min(maxDim / w, maxDim / h);
                }

                pixelWidth = Math.Max(1, (int)Math.Round(w * scale));
                pixelHeight = Math.Max(1, (int)Math.Round(h * scale));

                var dpi = new Vector(96 * scale, 96 * scale);
                var rtb = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), dpi);
                rtb.Render(workspace);
                return rtb;
            }
            catch
            {
                return null;
            }
        }

        public static bool ExportarWorkspaceParaPng(Canvas workspace, string filePath)
        {
            using var rtb = CapturarWorkspaceBitmap(workspace, out _, out _);
            if (rtb == null) return false;

            try
            {
                using var fs = File.Create(filePath);
                rtb.Save(fs);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ==================== EXTRAÇÃO EM PDF (SEM DEPENDÊNCIAS EXTERNAS) ====================

        public static bool ExportarWorkspaceParaPdf(
            Canvas workspace,
            WorkspaceData wsData,
            string filePath)
        {
            using var rtb = CapturarWorkspaceBitmap(workspace, out int imgW, out int imgH);
            if (rtb == null || imgW <= 0 || imgH <= 0) return false;

            byte[] compressedRgb;
            try
            {
                int stride = imgW * 4;
                byte[] bgra = new byte[stride * imgH];
                GCHandle handle = GCHandle.Alloc(bgra, GCHandleType.Pinned);
                try
                {
                    rtb.CopyPixels(new PixelRect(0, 0, imgW, imgH), handle.AddrOfPinnedObject(), bgra.Length, stride);
                }
                finally
                {
                    handle.Free();
                }

                byte[] rgb = new byte[imgW * imgH * 3];
                int p = 0;
                for (int i = 0; i < bgra.Length; i += 4)
                {
                    // BGRA8888 -> RGB24
                    rgb[p++] = bgra[i + 2];
                    rgb[p++] = bgra[i + 1];
                    rgb[p++] = bgra[i];
                }

                using var msZlib = new MemoryStream();
                using (var zlib = new ZLibStream(msZlib, CompressionLevel.Fastest, leaveOpen: true))
                {
                    zlib.Write(rgb, 0, rgb.Length);
                }
                compressedRgb = msZlib.ToArray();
            }
            catch
            {
                return false;
            }

            try
            {
                GerarArquivoPdf(wsData, imgW, imgH, compressedRgb, filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void GerarArquivoPdf(
            WorkspaceData wsData,
            int imgW,
            int imgH,
            byte[] compressedRgb,
            string filePath)
        {
            // A4 Paisagem: 842 x 595 pt
            const double pageW = 842.0;
            const double pageH = 595.0;
            const double margin = 32.0;

            string dataHora = DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            int qtdJanelas = wsData.SavedWindows?.Count ?? 0;
            int qtdConexoes = wsData.Connections?.Count ?? 0;

            // Conteúdo da Página 1: Cabeçalho + Imagem do Workspace + Rodapé
            double availW = pageW - margin * 2;
            double headerH = 44.0;
            double footerH = 22.0;
            double availH = pageH - margin * 2 - headerH - footerH;

            double scale = Math.Min(availW / imgW, availH / imgH);
            double drawW = imgW * scale;
            double drawH = imgH * scale;
            double drawX = margin + (availW - drawW) / 2.0;
            double drawY = margin + footerH + (availH - drawH) / 2.0;

            var p1 = new StringBuilder();
            p1.AppendLine("BT");
            p1.AppendLine("/F2 15 Tf");
            p1.AppendLine($"0.12 0.16 0.23 rg {Fmt(margin)} {Fmt(pageH - margin - 14)} Td ({EscPdf($"Workspace: {wsData.Name}")}) Tj");
            p1.AppendLine("/F1 9.5 Tf");
            p1.AppendLine($"0.38 0.45 0.55 rg 0 -15 Td ({EscPdf($"Demonstracao visual gerada em {dataHora}  |  {qtdJanelas} janela(s)  |  {qtdConexoes} ligacao(oes)")}) Tj");
            p1.AppendLine("ET");

            // Moldura sutil ao redor da captura
            p1.AppendLine("0.82 0.85 0.90 RG 0.8 w");
            p1.AppendLine($"{Fmt(drawX - 1)} {Fmt(drawY - 1)} {Fmt(drawW + 2)} {Fmt(drawH + 2)} re S");

            // Imagem do Workspace
            p1.AppendLine("q");
            p1.AppendLine($"{Fmt(drawW)} 0 0 {Fmt(drawH)} {Fmt(drawX)} {Fmt(drawY)} cm");
            p1.AppendLine("/Im1 Do");
            p1.AppendLine("Q");

            // Rodapé Página 1
            p1.AppendLine("BT /F1 8.5 Tf 0.45 0.50 0.58 rg");
            p1.AppendLine($"{Fmt(margin)} {Fmt(margin)} Td ({EscPdf("Gerenciador MDI Portatil - Pagina 1 (Visao Geral do Workspace)")}) Tj ET");

            // Conteúdo da Página 2: Resumo Estruturado dos Itens, Criticidade e Ligações
            var p2 = new StringBuilder();
            double y = pageH - margin - 14;
            p2.AppendLine("BT");
            p2.AppendLine($"/F2 14 Tf 0.12 0.16 0.23 rg {Fmt(margin)} {Fmt(y)} Td ({EscPdf($"Resumo Estruturado - {wsData.Name}")}) Tj");
            p2.AppendLine("ET");
            y -= 24;

            void AddLine(string text, bool bold = false, double size = 9.5, double r = 0.20, double g = 0.25, double b = 0.33, double indent = 0)
            {
                if (y < margin + 24) return;
                string font = bold ? "/F2" : "/F1";
                p2.AppendLine($"BT {font} {Fmt(size)} Tf {Fmt(r)} {Fmt(g)} {Fmt(b)} rg {Fmt(margin + indent)} {Fmt(y)} Td ({EscPdf(Truncar(text, 115))}) Tj ET");
                y -= (size + 4.5);
            }

            AddLine($"Janelas e Formas no Workspace ({qtdJanelas}):", bold: true, size: 11, r: 0.10, g: 0.30, b: 0.65);
            y -= 2;

            if (wsData.SavedWindows != null)
            {
                foreach (var win in wsData.SavedWindows)
                {
                    if (y < margin + 35) break;
                    string tipo = TraduzirTipoJanela(win.WindowType);
                    string crit = string.IsNullOrWhiteSpace(win.CriticalityLevel) || win.CriticalityLevel == "none"
                        ? ""
                        : $" [CRITICIDADE: {win.CriticalityLevel.ToUpperInvariant()}]";

                    AddLine($"- [{tipo}] {win.Title}{crit}", bold: true, size: 9.5);

                    if (!string.IsNullOrWhiteSpace(win.NoteContent))
                    {
                        string resumo = win.NoteContent.Replace("\r", " ").Replace("\n", " ").Trim();
                        AddLine($"Conteudo: {Truncar(resumo, 98)}", bold: false, size: 8.5, r: 0.38, g: 0.44, b: 0.52, indent: 14);
                    }
                    else if (win.WindowType == "checklist" && win.ChecklistItems.Count > 0)
                    {
                        int feitos = win.ChecklistItems.Count(c => c.IsChecked);
                        AddLine($"Itens: {feitos}/{win.ChecklistItems.Count} concluido(s)", bold: false, size: 8.5, r: 0.38, g: 0.44, b: 0.52, indent: 14);
                    }
                    else if (win.WindowType == "gallery" && win.GalleryItems.Count > 0)
                    {
                        AddLine($"Midias na galeria: {win.GalleryItems.Count} arquivo(s)", bold: false, size: 8.5, r: 0.38, g: 0.44, b: 0.52, indent: 14);
                    }
                    else if (win.WindowType == "spreadsheet")
                    {
                        AddLine($"Planilha: {win.SpreadsheetRows} linhas x {win.SpreadsheetCols} colunas ({win.SpreadsheetCells.Count} celulas preenchidas)", bold: false, size: 8.5, r: 0.38, g: 0.44, b: 0.52, indent: 14);
                    }
                }
            }

            if (qtdConexoes > 0 && y > margin + 50 && wsData.Connections != null)
            {
                y -= 8;
                AddLine($"Ligacoes do Fluxograma ({qtdConexoes}):", bold: true, size: 11, r: 0.10, g: 0.30, b: 0.65);
                y -= 2;

                foreach (var conn in wsData.Connections)
                {
                    if (y < margin + 30) break;
                    string fromTitle = wsData.SavedWindows?.FirstOrDefault(w => w.Id == conn.FromWindowId)?.Title ?? conn.FromWindowId;
                    string toTitle = wsData.SavedWindows?.FirstOrDefault(w => w.Id == conn.ToWindowId)?.Title ?? conn.ToWindowId;
                    string lbl = string.IsNullOrWhiteSpace(conn.Label) ? "" : $" ({conn.Label})";
                    AddLine($"- {fromTitle}  -->  {toTitle}{lbl}", bold: false, size: 9);
                }
            }

            p2.AppendLine("BT /F1 8.5 Tf 0.45 0.50 0.58 rg");
            p2.AppendLine($"{Fmt(margin)} {Fmt(margin)} Td ({EscPdf("Gerenciador MDI Portatil - Pagina 2 (Resumo Executivo)")}) Tj ET");

            // Monta estrutura PDF 1.4 binária
            using var fs = File.Create(filePath);
            var offsets = new List<long> { 0 };

            void WriteAscii(string s)
            {
                byte[] b = Encoding.Latin1.GetBytes(s);
                fs.Write(b, 0, b.Length);
            }

            void BeginObj(int id)
            {
                offsets.Add(fs.Position);
                WriteAscii($"{id} 0 obj\n");
            }

            WriteAscii("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

            // 1: Catalog
            BeginObj(1);
            WriteAscii("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            // 2: Pages
            BeginObj(2);
            WriteAscii("<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>\nendobj\n");

            // 3: Page 1
            BeginObj(3);
            WriteAscii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Fmt(pageW)} {Fmt(pageH)}] /Resources << /Font << /F1 5 0 R /F2 6 0 R >> /XObject << /Im1 7 0 R >> >> /Contents 8 0 R >>\nendobj\n");

            // 4: Page 2
            BeginObj(4);
            WriteAscii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Fmt(pageW)} {Fmt(pageH)}] /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> /Contents 9 0 R >>\nendobj\n");

            // 5: Font Regular (WinAnsiEncoding para acentos PT-BR)
            BeginObj(5);
            WriteAscii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

            // 6: Font Bold
            BeginObj(6);
            WriteAscii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

            // 7: Image XObject
            BeginObj(7);
            WriteAscii($"<< /Type /XObject /Subtype /Image /Width {imgW} /Height {imgH} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {compressedRgb.Length} >>\nstream\n");
            fs.Write(compressedRgb, 0, compressedRgb.Length);
            WriteAscii("\nendstream\nendobj\n");

            // 8: Content stream Page 1
            byte[] p1Bytes = Encoding.Latin1.GetBytes(p1.ToString());
            BeginObj(8);
            WriteAscii($"<< /Length {p1Bytes.Length} >>\nstream\n");
            fs.Write(p1Bytes, 0, p1Bytes.Length);
            WriteAscii("\nendstream\nendobj\n");

            // 9: Content stream Page 2
            byte[] p2Bytes = Encoding.Latin1.GetBytes(p2.ToString());
            BeginObj(9);
            WriteAscii($"<< /Length {p2Bytes.Length} >>\nstream\n");
            fs.Write(p2Bytes, 0, p2Bytes.Length);
            WriteAscii("\nendstream\nendobj\n");

            long xrefPos = fs.Position;
            WriteAscii($"xref\n0 {offsets.Count}\n");
            WriteAscii("0000000000 65535 f \n");
            for (int i = 1; i < offsets.Count; i++)
            {
                WriteAscii($"{offsets[i]:D10} 00000 n \n");
            }
            WriteAscii($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");
        }

        // ==================== EXTRAÇÃO EM HTML DE DEMONSTRAÇÃO ====================

        public static bool ExportarWorkspaceParaHtml(Canvas workspace, WorkspaceData wsData, string filePath)
        {
            try
            {
                string imgBase64 = string.Empty;
                using (var rtb = CapturarWorkspaceBitmap(workspace, out _, out _))
                {
                    if (rtb != null)
                    {
                        using var ms = new MemoryStream();
                        rtb.Save(ms);
                        imgBase64 = Convert.ToBase64String(ms.ToArray());
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">");
                sb.AppendLine($"<title>Demonstração - {WebUtility.HtmlEncode(wsData.Name)}</title>");
                sb.AppendLine("<style>");
                sb.AppendLine("body{font-family:Segoe UI,system-ui,sans-serif;background:#0f172a;color:#e2e8f0;margin:0;padding:28px;}");
                sb.AppendLine(".wrap{max-width:1180px;margin:0 auto;}");
                sb.AppendLine("h1{margin:0 0 6px;font-size:24px;color:#f8fafc;} .sub{color:#94a3b8;font-size:13px;margin-bottom:20px;}");
                sb.AppendLine(".preview{background:#1e293b;border:1px solid #334155;border-radius:10px;padding:12px;margin-bottom:24px;}");
                sb.AppendLine(".preview img{width:100%;height:auto;border-radius:6px;display:block;}");
                sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(290px,1fr));gap:14px;}");
                sb.AppendLine(".card{background:#1e293b;border:1px solid #334155;border-radius:8px;padding:14px;}");
                sb.AppendLine(".badge{display:inline-block;font-size:11px;font-weight:700;padding:2px 8px;border-radius:12px;background:#2563eb;color:#fff;margin-right:6px;}");
                sb.AppendLine(".crit-critical{background:#dc2626;} .crit-high{background:#ea580c;} .crit-medium{background:#d97706;} .crit-low{background:#2563eb;}");
                sb.AppendLine("pre{white-space:pre-wrap;font-family:inherit;font-size:12.5px;color:#cbd5e1;margin:8px 0 0;}");
                sb.AppendLine("</style></head><body><div class=\"wrap\">");
                sb.AppendLine($"<h1>{WebUtility.HtmlEncode(wsData.Name)}</h1>");
                sb.AppendLine($"<div class=\"sub\">Demonstração exportada em {DateTime.Now:dd/MM/yyyy HH:mm} • {wsData.SavedWindows.Count} janela(s) • {wsData.Connections.Count} ligação(ões)</div>");

                if (!string.IsNullOrEmpty(imgBase64))
                {
                    sb.AppendLine($"<div class=\"preview\"><img src=\"data:image/png;base64,{imgBase64}\" alt=\"Workspace\"/></div>");
                }

                sb.AppendLine("<div class=\"grid\">");
                foreach (var w in wsData.SavedWindows)
                {
                    sb.AppendLine("<div class=\"card\">");
                    sb.Append($"<div><span class=\"badge\">{WebUtility.HtmlEncode(TraduzirTipoJanela(w.WindowType))}</span>");
                    if (!string.IsNullOrWhiteSpace(w.CriticalityLevel) && w.CriticalityLevel != "none")
                    {
                        sb.Append($"<span class=\"badge crit-{WebUtility.HtmlEncode(w.CriticalityLevel)}\">{WebUtility.HtmlEncode(w.CriticalityLevel.ToUpperInvariant())}</span>");
                    }
                    sb.AppendLine("</div>");
                    sb.AppendLine($"<h3 style=\"margin:8px 0 4px;font-size:15px;\">{WebUtility.HtmlEncode(w.Title)}</h3>");

                    if (!string.IsNullOrWhiteSpace(w.NoteContent))
                    {
                        sb.AppendLine($"<pre>{WebUtility.HtmlEncode(w.NoteContent)}</pre>");
                    }
                    else if (w.WindowType == "checklist" && w.ChecklistItems.Count > 0)
                    {
                        sb.AppendLine("<ul style=\"padding-left:18px;margin:8px 0 0;font-size:12.5px;color:#cbd5e1;\">");
                        foreach (var item in w.ChecklistItems)
                        {
                            string check = item.IsChecked ? "☑" : "☐";
                            sb.AppendLine($"<li>{check} {WebUtility.HtmlEncode(item.Text)}</li>");
                        }
                        sb.AppendLine("</ul>");
                    }
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div></div></body></html>");

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ==================== EXPORTAÇÃO E IMPORTAÇÃO DE ARQUIVOS ====================

        public static bool ExportarWorkspaceJson(WorkspaceData wsData, string filePath)
        {
            try
            {
                string json = JsonSerializer.Serialize(wsData, JsonOptions);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool ExportarWorkspaceZip(WorkspaceData wsData, string zipFilePath)
        {
            try
            {
                string cloneJson = JsonSerializer.Serialize(wsData, JsonOptions);
                var clone = JsonSerializer.Deserialize<WorkspaceData>(cloneJson);
                if (clone == null) return false;

                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }

                using var zip = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);
                var copiedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int mediaCounter = 0;

                string? TryAddMediaToZip(string originalPath)
                {
                    if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath))
                        return null;

                    if (copiedMap.TryGetValue(originalPath, out string? existingEntry))
                        return existingEntry;

                    mediaCounter++;
                    string safeName = $"{mediaCounter:D3}_{Path.GetFileName(originalPath)}";
                    string entryPath = $"media/{safeName}";
                    zip.CreateEntryFromFile(originalPath, entryPath, CompressionLevel.Optimal);
                    copiedMap[originalPath] = entryPath;
                    return entryPath;
                }

                if (!string.IsNullOrWhiteSpace(clone.BackgroundImagePath))
                {
                    string? rel = TryAddMediaToZip(clone.BackgroundImagePath);
                    if (rel != null) clone.BackgroundImagePath = rel;
                }

                void RemapWindowList(List<WindowStateData> list)
                {
                    foreach (var w in list)
                    {
                        if ((w.WindowType == "image" || w.WindowType == "video") && !string.IsNullOrWhiteSpace(w.NoteContent))
                        {
                            string? rel = TryAddMediaToZip(w.NoteContent);
                            if (rel != null) w.NoteContent = rel;
                        }
                        if (w.GalleryItems != null)
                        {
                            foreach (var g in w.GalleryItems)
                            {
                                string? rel = TryAddMediaToZip(g.FilePath);
                                if (rel != null) g.FilePath = rel;
                            }
                        }
                    }
                }

                RemapWindowList(clone.SavedWindows);
                RemapWindowList(clone.SavedCatalog);

                var wsEntry = zip.CreateEntry("workspace.mdiws", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(wsEntry.Open(), Encoding.UTF8))
                {
                    writer.Write(JsonSerializer.Serialize(clone, JsonOptions));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static WorkspaceData? ImportarWorkspace(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".zip")
                {
                    using var zip = ZipFile.OpenRead(filePath);
                    var wsEntry = zip.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith(".mdiws", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

                    if (wsEntry == null) return null;

                    string json;
                    using (var reader = new StreamReader(wsEntry.Open(), Encoding.UTF8))
                    {
                        json = reader.ReadToEnd();
                    }

                    var imported = JsonSerializer.Deserialize<WorkspaceData>(json);
                    if (imported == null) return null;

                    imported.Id = Guid.NewGuid().ToString("N");
                    string mediaDir = Path.Combine(AppContext.BaseDirectory, "ImportedMedia", imported.Id);
                    Directory.CreateDirectory(mediaDir);

                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName.StartsWith("media/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Name))
                        {
                            string dest = Path.Combine(mediaDir, entry.Name);
                            entry.ExtractToFile(dest, overwrite: true);
                        }
                    }

                    string ResolveImportedPath(string path)
                    {
                        if (string.IsNullOrWhiteSpace(path)) return path;
                        if (path.StartsWith("media/", StringComparison.OrdinalIgnoreCase))
                        {
                            string fileName = Path.GetFileName(path);
                            string candidate = Path.Combine(mediaDir, fileName);
                            if (File.Exists(candidate)) return candidate;
                        }
                        return path;
                    }

                    imported.BackgroundImagePath = ResolveImportedPath(imported.BackgroundImagePath);
                    foreach (var w in imported.SavedWindows.Concat(imported.SavedCatalog))
                    {
                        if (w.WindowType == "image" || w.WindowType == "video")
                        {
                            w.NoteContent = ResolveImportedPath(w.NoteContent);
                        }
                        if (w.GalleryItems != null)
                        {
                            foreach (var g in w.GalleryItems)
                            {
                                g.FilePath = ResolveImportedPath(g.FilePath);
                            }
                        }
                    }

                    return imported;
                }
                else
                {
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    var imported = JsonSerializer.Deserialize<WorkspaceData>(json);
                    if (imported == null) return null;
                    imported.Id = Guid.NewGuid().ToString("N");
                    return imported;
                }
            }
            catch
            {
                return null;
            }
        }

        public static bool ExportarJanelaIndividual(WindowStateData win, string targetPath)
        {
            try
            {
                if (win.WindowType is "image" or "video")
                {
                    if (!string.IsNullOrWhiteSpace(win.NoteContent) && File.Exists(win.NoteContent))
                    {
                        File.Copy(win.NoteContent, targetPath, overwrite: true);
                        return true;
                    }
                    return false;
                }

                if (win.WindowType == "spreadsheet")
                {
                    var sb = new StringBuilder();
                    var map = win.SpreadsheetCells.ToDictionary(c => c.CellKey, c => c.RawValue, StringComparer.OrdinalIgnoreCase);
                    for (int r = 1; r <= win.SpreadsheetRows; r++)
                    {
                        var cols = new List<string>();
                        for (int c = 0; c < win.SpreadsheetCols; c++)
                        {
                            string key = $"{(char)('A' + c)}{r}";
                            map.TryGetValue(key, out string? val);
                            val ??= string.Empty;
                            if (val.Contains(';') || val.Contains('"') || val.Contains('\n'))
                            {
                                val = "\"" + val.Replace("\"", "\"\"") + "\"";
                            }
                            cols.Add(val);
                        }
                        sb.AppendLine(string.Join(";", cols));
                    }
                    File.WriteAllText(targetPath, sb.ToString(), new UTF8Encoding(true));
                    return true;
                }

                if (win.WindowType == "checklist")
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"# {win.Title}");
                    sb.AppendLine();
                    foreach (var item in win.ChecklistItems)
                    {
                        sb.AppendLine($"- [{(item.IsChecked ? "x" : " ")}] {item.Text}");
                        if (!string.IsNullOrWhiteSpace(item.LinkedNoteContent))
                        {
                            sb.AppendLine($"  Nota: {item.LinkedNoteContent.Replace("\n", "\n  ")}");
                        }
                    }
                    File.WriteAllText(targetPath, sb.ToString(), Encoding.UTF8);
                    return true;
                }

                // notepad, shape_*, browser, default
                File.WriteAllText(targetPath, win.NoteContent ?? string.Empty, Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static (int rows, int cols, List<SpreadsheetCellData> cells) ParseCsvParaPlanilha(string csvContent)
        {
            var lines = (csvContent ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
                return (12, 6, new List<SpreadsheetCellData>());

            char sep = lines[0].Contains(';') ? ';' : ',';
            var cells = new List<SpreadsheetCellData>();
            int maxCols = 1;

            for (int r = 0; r < lines.Length && r < 50; r++)
            {
                var parts = lines[r].Split(sep);
                maxCols = Math.Max(maxCols, Math.Min(26, parts.Length));
                for (int c = 0; c < parts.Length && c < 26; c++)
                {
                    string val = parts[c].Trim();
                    if (val.StartsWith('"') && val.EndsWith('"') && val.Length >= 2)
                    {
                        val = val[1..^1].Replace("\"\"", "\"");
                    }
                    if (!string.IsNullOrEmpty(val))
                    {
                        cells.Add(new SpreadsheetCellData
                        {
                            CellKey = $"{(char)('A' + c)}{r + 1}",
                            RawValue = val
                        });
                    }
                }
            }

            return (Math.Max(12, lines.Length + 2), Math.Max(6, maxCols), cells);
        }

        private static string TraduzirTipoJanela(string? wt) => wt switch
        {
            "notepad" => "Bloco de Notas",
            "checklist" => "Lista de Marcação",
            "spreadsheet" => "Planilha",
            "gallery" => "Galeria",
            "image" => "Imagem",
            "video" => "Vídeo",
            "browser" => "Navegador",
            "shape_rect" => "Retângulo",
            "shape_diamond" => "Losango",
            "shape_circle" => "Círculo",
            _ => "Janela"
        };

        private static string EscPdf(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static string Truncar(string text, int max)
            => text.Length <= max ? text : text[..(max - 3)] + "...";

        private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
