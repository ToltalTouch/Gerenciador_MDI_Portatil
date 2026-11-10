# Gerenciador MDI Portátil em C# (Avalonia UI)

Este pacote contém o código-fonte completo de uma aplicação desktop em **C# (Avalonia UI)** que implementa um gerenciador de janelas internas (MDI) arrastáveis, redimensionáveis, minimizáveis, maximizáveis, barra de tarefas inferior, sistema de notificações Toast, persistência em JSON e alternância de temas.

## Estrutura do Projeto no ZIP:
* `Program.cs` / `App.axaml` / `MainWindow.axaml` / `MainWindow.axaml.cs`
* `DraggableWindow.cs` (Janela interna flutuante com redimensionamento pelas bordas)
* `TaskBar.cs` (Barra de tarefas inferior)
* `SettingsWindow.cs` (Janela de configurações básicas)
* `ToastNotification.cs` (Sistema de notificações flutuantes)
* `AppSettings.cs` (Modelos de serialização JSON)
* `build.sh` (Script automatizado para gerar os executáveis portáteis para Linux e Windows)
* `settings.json` (Arquivo de configuração padrão)

---

## Como Compilar / Gerar os Executáveis Portáteis

1. Abra o terminal na pasta do projeto.
2. Dê permissão de execução ao script de build:
   ```bash
   chmod +x build.sh
   ```
3. Execute o script:
   ```bash
   ./build.sh
   ```

Os binários autossuficientes serão gerados nas pastas:
* **Linux:** `dist/linux/`
* **Windows:** `dist/windows/`
