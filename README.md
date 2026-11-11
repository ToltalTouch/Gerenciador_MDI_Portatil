# Gerenciador MDI Portátil em C# (Avalonia UI)

Aplicação desktop leve em **C# (.NET 8 + Avalonia UI)** que implementa um gerenciador de janelas internas (**MDI Workspace**) com baixo consumo de recursos (pincéis imutáveis em cache e zero animações em loop), temas Escuro/Claro, organização automática em cascata/grade e **Widgets** integrados.

## Funcionalidades Principais
* **Janelas MDI Completas (`DraggableWindow.cs`):** Arrastáveis pela barra de título, redimensionáveis pelas bordas, minimizáveis, maximizáveis (incluindo duplo clique no topo), **edição direta do título da janela** no cabeçalho (`✎`) e botão **`💾` (`Ctrl+S`)** para salvar.
* **Barra e Menu de Formatação de Texto (`Aa ▾`):**
  * Disponível em todos os blocos com botões rápidos de **Negrito (`B` / `Ctrl+B`)**, **Itálico (`I` / `Ctrl+I`)**, **Aumentar/Diminuir Fonte (`A+` / `A-` de `9pt` a `32pt`)**, além de alinhamento (Esquerda, Centro, Direita) e estilo de fonte (Sans-Serif, Monoespaçada, Serifada).
* **Menu `🧩 Widgets ▾`:**
  * **📝 Novo Bloco de Notas:** Editor de texto multilinha com formatação e persistência.
  * **☑ Nova Lista de Marcação:** Checklist interativa onde marcar um item risca a linha automaticamente, botão **`📝`** em cada linha para abrir um **Bloco de Notas atrelado ao nome daquela linha**, e botão **`+`** posicionado abaixo da última linha com barra de rolagem automática.
  * **💾 Salvar Janela Ativa / Salvar Todas:** Persiste blocos independentes, notas atreladas à lista e listas completas no catálogo.
  * **⭐ Mais Usadas & 📂 Janelas Salvas:** Submenus dinâmicos para reabrir rapidamente as janelas mais acessadas ou qualquer janela salva no catálogo.
* **Organização Rápida:** Botões **❐ Cascata** e **⊞ Grade** na barra superior para organizar todas as janelas abertas.
* **Barra de Tarefas (`TaskBar.cs`):** Indicador de janela em foco, atualização automática ao renomear janelas, rolagem horizontal e contador de janelas ativas.
* **Persistência Completa (`SettingsManager.cs` / `settings.json`):** Salva automaticamente o tema, preferências, catálogo de janelas salvas, histórico de uso, textos, formatações e notas vinculadas a cada item da lista.

## Estrutura do Projeto
* **Raiz:**
  * `Program.cs` / `App.axaml` / `App.axaml.cs` (Inicialização da aplicação)
  * `AvaloniaMdiApp.csproj` / `settings.json`
* **`Views/` (Janelas Principais e Diálogos):**
  * `Views/MainWindow.axaml` / `Views/MainWindow.axaml.cs` (Workspace principal e menu de Widgets)
  * `Views/SettingsWindow.cs` (Janela de preferências do sistema)
* **`Controls/` (Componentes Visuais e Widgets MDI):**
  * `Controls/DraggableWindow.cs` (Janelas MDI e widgets de Bloco de Notas / Lista de Marcação)
  * `Controls/TaskBar.cs` (Barra de tarefas inferior)
  * `Controls/ToastNotification.cs` (Notificações flutuantes leves)
* **`Models/` (Modelos de Dados):**
  * `Models/AppSettings.cs` (`AppSettings`, `WindowStateData`, `ChecklistItemData`, `TextFormatData`)
* **`Services/` (Serviços e Gerenciadores):**
  * `Services/SettingsManager.cs` (Persistência JSON)
  * `Services/ZIndexManager.cs` (Controle de foco e ordem das janelas)
* **`Themes/` (Temas e Cores):**
  * `Themes/ThemePalette.cs` (Paleta estática de pincéis imutáveis para Dark/Light Mode)
* **`Scripts/` (Automação de Build):**
  * `Scripts/build.bat` / `Scripts/build.sh` (Geração de executáveis portáteis)

---

## Como Compilar / Executar

### Modo Desenvolvimento
```powershell
dotnet run
```

### Gerar Executável Portátil (Windows / Linux)
* **Windows:** Execute `build.bat` (gera o executável em `dist\windows\AvaloniaMdiApp.exe`).
* **Linux / macOS:** Execute `./build.sh` (gera os binários em `dist/linux/` e `dist/windows/`).
