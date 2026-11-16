using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public class NotificationHistoryItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info"; // "success", "warning", "danger", "info"

        public string FormattedTime => Timestamp.ToString("HH:mm:ss");
        public string FormattedDate => Timestamp.ToString("dd/MM/yyyy HH:mm");
    }

    public static class NotificationHistoryService
    {
        private static readonly List<NotificationHistoryItem> _items = new();
        private const int MaxItems = 150;

        public static event Action? OnHistoryUpdated;

        public static IReadOnlyList<NotificationHistoryItem> ObterItens()
        {
            lock (_items)
            {
                return _items.ToArray();
            }
        }

        public static int TotalNaoLidos { get; private set; }

        public static void Adicionar(string mensagem, string tipo = "info", string? tituloCustom = null)
        {
            string titulo = !string.IsNullOrWhiteSpace(tituloCustom)
                ? tituloCustom
                : tipo switch
                {
                    "success" => "Sucesso",
                    "warning" => "Aviso",
                    "danger" or "error" => "Erro",
                    _ => "Informação"
                };

            var item = new NotificationHistoryItem
            {
                Title = titulo,
                Message = mensagem,
                Type = (tipo == "error" ? "danger" : tipo),
                Timestamp = DateTime.Now
            };

            lock (_items)
            {
                _items.Insert(0, item);
                if (_items.Count > MaxItems)
                {
                    _items.RemoveAt(_items.Count - 1);
                }
                TotalNaoLidos++;
            }

            OnHistoryUpdated?.Invoke();
        }

        public static void MarcarTodosComoLidos()
        {
            lock (_items)
            {
                TotalNaoLidos = 0;
            }
            OnHistoryUpdated?.Invoke();
        }

        public static void LimparHistorico()
        {
            lock (_items)
            {
                _items.Clear();
                TotalNaoLidos = 0;
            }
            OnHistoryUpdated?.Invoke();
        }
    }
}
