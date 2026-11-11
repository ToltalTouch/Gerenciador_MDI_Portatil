# Gerenciador MDI Portátil em C# (Avalonia UI)

Aplicação desktop leve em **C# (.NET 8 + Avalonia UI)** que implementa um gerenciador de janelas internas (**MDI Workspace**) com baixo consumo de recursos (pincéis imutáveis em cache e zero animações em loop), temas Escuro/Claro, organização automática em cascata/grade e **Widgets** integrados.

## Funcionalidades Principais
* **Janelas MDI Completas (`DraggableWindow.cs`):** Arrastáveis pela barra de título, redimensionáveis pelas bordas, minimizáveis, maximizáveis (incluindo duplo clique no topo) e com destaque visual para a janela ativa.
* **Menu `🧩 Widgets`:**
  * **📝 Bloco de Notas:** Editor de texto multilinha com quebra automática e barra de rolagem.
  * **☑ Lista de Marcação:** Checklist interativa onde marcar um item risca a linha automaticamente, com botão **`+`** posicionado abaixo da última linha (ou tecla `Enter`) para adicionar novos itens e barra de rolagem automática.
* **Organização Rápida:** Botões **❐ Cascata** e **⊞ Grade** na barra superior para organizar todas as janelas abertas.
* **Barra de Tarefas (`TaskBar.cs`):** Indicador de janela em foco, rolagem horizontal automática e contador de janelas ativas.
* **Persistência Completa (`SettingsManager.cs` / `settings.json`):** Salva automaticamente o tema, preferências, posição/tamanho original das janelas, textos dos blocos de notas e itens das listas de marcação ao fechar o aplicativo.

## Estrutura do Projeto
* `Program.cs` / `App.axaml` / `MainWindow.axaml` / `MainWindow.axaml.cs`
* `DraggableWindow.cs` (Janelas internas e widgets de Bloco de Notas / Lista de Marcação)
* `TaskBar.cs` (Barra de tarefas inferior)
* `SettingsWindow.cs` (Janela flutuante de preferências do sistema)
* `ToastNotification.cs` (Notificações flutuantes leves)
* `AppSettings.cs` / `SettingsManager.cs` (Modelos de serialização JSON e paleta estática `ThemePalette`)
* `build.bat` / `build.sh` (Scripts para gerar os executáveis portáteis)

---

## Como Compilar / Executar

### Modo Desenvolvimento
```powershell
dotnet run
```

### Gerar Executável Portátil (Windows / Linux)
* **Windows:** Execute `build.bat` (gera o executável em `dist\windows\AvaloniaMdiApp.exe`).
* **Linux / macOS:** Execute `./build.sh` (gera os binários em `dist/linux/` e `dist/windows/`).
