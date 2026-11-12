<div align="center">
  <img src="Assets/app-icon.png" alt="Logo Gerenciador MDI Portátil" width="128" height="128" />

  # Gerenciador MDI Portátil
  **Ambiente de Trabalho Visual Multi-Janelas (MDI), Fluxogramas e Widgets Integrados em C# (.NET 10 + Avalonia UI)**

  [![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
  [![Avalonia UI](https://img.shields.io/badge/Avalonia_UI-11.1-8B5CF6)](https://avaloniaui.net/)
  [![Platform](https://img.shields.io/badge/Plataforma-Linux%20%7C%20Windows-2563EB)](#como-executar--releases)
  [![Performance](https://img.shields.io/badge/Recursos-Hiberna%C3%A7%C3%A3o_Inteligente_%F0%9F%92%A4-059669)](#economia-de-recursos-e-hiberna%C3%A7%C3%A3o-inteligente)
</div>

---

## ![GitHub release (latest by date)](https://shields.io)

## 📌 Visão Geral

O **Gerenciador MDI Portátil** é uma aplicação desktop multiplataforma (**Linux** e **Windows**) desenvolvida em **C# (.NET 10)** com **Avalonia UI**. Ele oferece um **Workspace visual infinito com Zoom e Pan**, onde o usuário pode abrir múltiplas janelas internas independentes, conectá-las visualmente como um **fluxograma interativo**, organizar múltiplos **Workspaces isolados** e utilizar **Widgets integrados** (Bloco de Notas, Lista de Marcação, Planilhas estilo Excel, Galeria de Imagens e Navegador Web).

Projetado com foco extremo em **baixo consumo de memória RAM e CPU**, o aplicativo conta com um motor de **Hibernação Automática**: planilhas em segundo plano e Workspaces inativos têm seus controles visuais e recursos nativos descarregados da memória automaticamente.

---

## 🖼️ Demonstração Visual (Screenshots)

> 💡 **Espaços reservados para imagens ilustrativas:** Adicione suas capturas de tela na pasta `docs/screenshots/` com os nomes abaixo (ou altere os caminhos conforme preferir).

### 1. Visão Geral do Workspace e Fluxograma Interativo
<!-- Substitua pelo print geral mostrando janelas abertas conectadas por linhas de fluxograma -->
![Visão Geral do Workspace e Fluxograma](docs/screenshots/01-visao-geral-fluxograma.png)
* legenda sugerida: Múltiplas janelas conectadas com curvas Bézier no modo Fluxograma, barra de menus tradicional no topo e controles de Zoom/Conectar no canto inferior direito.*

---

### 2. Widget de Planilhas (Fórmulas estilo Excel e Formatação Condicional)
<!-- Substitua pelo print da Planilha mostrando fórmulas (=SOMA, =MEDIA, =SE) e células coloridas pela formatação condicional -->
![Widget de Planilhas e Formatação Condicional](docs/screenshots/02-planilhas-excel-condicional.png)
* legenda sugerida: Planilha ativa com barra de fórmulas (`fx`), cálculo automático (`=SOMA`, `=MEDIA`, `=SE`) e realce condicional em verde/vermelho, enquanto outra planilha ao fundo permanece em modo hibernado (`💤`).*

---

### 3. Bloco de Notas, Lista de Marcação (Checklist) e Notas Vinculadas
<!-- Substitua pelo print mostrando uma Lista de Marcação conectada a um Bloco de Notas de uma tarefa -->
![Checklist e Bloco de Notas Vinculado](docs/screenshots/03-checklist-bloco-de-notas.png)
* legenda sugerida: Lista de marcação com abertura direta de Bloco de Notas vinculado a cada item da lista e formatação rica de texto.*

---

### 4. Galeria de Imagens, Visualizador e Navegador Web Integrado
<!-- Substitua pelo print mostrando a Galeria de Imagens, uma imagem aberta em janela e o Navegador Web -->
![Galeria de Imagens e Navegador Web](docs/screenshots/04-galeria-e-navegador.png)
* legenda sugerida: Galeria de Imagens com suporte a Drag & Drop, aplicação de plano de fundo no Workspace e Navegador Web embutido.*

---

### 5. Múltiplos Workspaces, Personalização de Cores e Ajustes de Layout
<!-- Substitua pelo print mostrando as abas de Workspaces (ativos e hibernados 💤), cores personalizadas nas janelas e a janela de Preferências -->
![Múltiplos Workspaces e Configurações de Layout](docs/screenshots/05-workspaces-e-configuracoes.png)
* legenda sugerida: Alternância rápida entre Workspaces (com hibernação automática `💤`), paleta de cores por janela/linha e menu de escala de interface.*

---

## ✨ Funcionalidades Principais

### 🗂️ 1. Múltiplos Workspaces Isolados
* Crie quantos **Workspaces** desejar pela barra superior (`Workspaces ▸ ➕ Novo Workspace...` ou botão `+`).
* Cada Workspace possui suas próprias janelas, conexões de fluxograma, nível de zoom, imagem de plano de fundo e catálogo de itens salvos.
* **Confirmação de salvamento ao sair:** Ao fechar o aplicativo, um modal pergunta se deseja **Salvar e Sair**, **Sair sem Salvar** ou **Cancelar**.

### 🔗 2. Fluxograma entre Janelas + Zoom Out / Zoom In e Pan
* **Conexões Visuais (`🔗`):** Ligue qualquer janela a outra clicando no botão `🔗` na barra de título da janela ou no botão flutuante `🔗 Conectar`.
* **Curvas Bézier com Setas:** As linhas acompanham o movimento e o redimensionamento das janelas em tempo real.
* **Personalização e Remoção:** Clique no emblema `🔗` no centro da linha com o **botão esquerdo** para remover a ligação ou com o **botão direito** para mudar a cor individual da linha.
* **Zoom do Workspace (`25%` a `200%`):** Use `Ctrl + Roda do Mouse` ou os botões `➖ / 100% / ➕` no HUD inferior para afastar a câmera (*Zoom Out*), visualizar dezenas de janelas e arrastar o fundo (*Pan*).

### 📊 3. Widget de Planilhas (Estilo Microsoft Excel)
* **Barra de Fórmulas (`fx`)** e referência da célula ativa (`A1`, `B2`...).
* **Motor de Fórmulas em Português e Inglês:**
  * `=SOMA(A1:A10)` / `=SUM(...)`
  * `=MEDIA(A1:A10)` / `=AVERAGE(...)`
  * `=MINIMO(A1:A10)` / `=MIN(...)` e `=MAXIMO(A1:A10)` / `=MAX(...)`
  * `=CONT.NUM(A1:A10)` / `=COUNT(...)` e `=CONT.VALORES(A1:A10)` / `=COUNTA(...)`
  * `=SE(A1>=100; "Aprovado"; "Reprovado")` / `=IF(...)`
  * `=CONT.SE(A1:A10; ">50")` / `=COUNTIF(...)` e `=SOMASE(A1:A10; ">0")` / `=SUMIF(...)`
  * `=MED(A1:A10)`, `=ARRED(A1; 2)`, `=POTENCIA(A1; 2)`, `=RAIZ(A1)`, `=ABS(A1)` e expressões aritméticas (`+`, `-`, `*`, `/`, `^`).
* **Formatação Condicional Estilo Excel (`🎨 Formatação Condicional ▾`):**
  * Regras rápidas e personalizadas (*Maior que*, *Menor que*, *Igual a*, *Está Entre*, *Texto Contém*) com estilos clássicos do Excel (Verde Claro, Vermelho Claro, Amarelo, Azul e Roxo).

### 📝 4. Bloco de Notas & ☑ Lista de Marcação (Checklist)
* **Formatação de Texto (`Aa ▾`):** Negrito (`Ctrl+B`), Itálico (`Ctrl+I`), tamanho da fonte (`9pt` a `32pt`), alinhamento (Esquerda, Centro, Direita) e família tipográfica (Sans-Serif, Monoespaçada, Serifada).
* **Checklist Conectada:** Cada item da lista possui um botão `📝` que abre (e conecta via fluxograma) um Bloco de Notas exclusivo para aquela tarefa.

### 🖼️ 5. Galeria de Imagens e Visualizador
* Adicione imagens por seletor de arquivos, caminho de diretório ou **Arrastar e Soltar (Drag & Drop)**.
* Abra imagens em janelas dedicadas no Workspace (com controle de Zoom e ajuste) ou defina qualquer imagem como **Plano de Fundo do Workspace**.

### 🌐 6. Navegador Web Integrado
* Navegação web dentro das janelas MDI com barra de endereço, botões Voltar/Avançar/Recarregar e atalhos rápidos (Google, YouTube, GitHub, Wikipédia).
* Suporte nativo via **WebKit2GTK** no Linux e **WebView2** no Windows.

### 🎨 7. Cores Personalizadas e Ajuste de Tamanho do Layout
* **Paleta de Cores por Janela (`🎨`):** Personalize a cor de destaque do cabeçalho e da borda de cada janela individualmente para categorizar seus quadros.
* **Ajustes de Layout (`Preferências ▸ ⚙ Configurar Preferências e Layout...`):**
  * Escala Geral da Interface (`85%`, `100%`, `115%`, `125%`).
  * Tamanho da Janela Principal (`900×600`, `1024×680`, `1280×720`, `1440×860`, `Maximizada`).
  * Proporção das Janelas Internas (`Compacto`, `Normal`, `Grande`).

---

## 💤 Economia de Recursos e Hibernação Inteligente

O aplicativo foi arquitetado para rodar com uso mínimo de memória RAM e `0%` de uso de CPU em repouso:

1. **Hibernação de Planilhas Inativas (`💤`):**
   * Quando você está trabalhando em uma planilha (ou em outra janela), todas as **demais planilhas abertas entram automaticamente em modo hibernado**.
   * Os controles visuais pesados de todas as células (`TextBox`, `Border`, cabeçalhos e caches) são descarregados da memória, mantendo apenas os dados brutos leves. Ao clicar novamente na planilha hibernada, a grade é reconstruída instantaneamente.
2. **Hibernação Completa de Workspaces (`💤`):**
   * Ao alternar de um Workspace para outro, o Workspace anterior tem **100% de suas janelas visuais, imagens em memória e instâncias nativas de navegador encerradas e coletadas pelo Garbage Collector** (incluindo compactação imediata do *Large Object Heap*).
   * Workspaces inativos ficam identificados com o ícone `💤` na barra superior e consomem virtualmente zero recursos gráficos.
3. **Otimização de Miniaturas e Timers:**
   * Miniaturas da Galeria utilizam decodificação reduzida (`DecodeToWidth`), economizando mais de 95% de RAM em imagens de alta resolução.
   * Os *timers* de integração nativa do navegador Linux são pausados automaticamente quando não há navegadores visíveis/ativos.

---

## 📂 Estrutura do Projeto

```text
Gerenciador_MDI_Portatil/
├── Assets/
│   ├── app-icon.png                        # Ícone oficial da aplicação (PNG)
│   └── app-icon.ico                        # Ícone multi-resolução para executável Windows (ICO)
├── Controls/
│   ├── DraggableWindow.cs                  # Núcleo da janela MDI, cores, foco e barra de título
│   ├── DraggableWindow.Formatting.cs       # Barra de formatação de texto e Checklist
│   ├── DraggableWindow.Spreadsheet.cs      # Motor de Planilhas Excel, fórmulas, formatação condicional e hibernação
│   ├── DraggableWindow.Gallery.cs          # Galeria de Imagens, visualizador e otimização de memória
│   ├── DraggableWindow.Browser.cs          # Interface do widget de Navegador Web
│   ├── ChromiumWebViewHost.cs              # Host nativo WebView2 (Windows) / WebKit2GTK (Linux) com hibernação
│   ├── FlowchartConnectionVisual.cs        # Linhas curvas Bézier e setas do Fluxograma entre janelas
│   ├── TaskBar.cs                          # Barra de tarefas inferior com indicador de janelas hibernadas (💤)
│   └── ToastNotification.cs                # Notificações flutuantes leves
├── Models/
│   └── AppSettings.cs                      # Modelos de dados (Workspaces, Janelas, Células, Regras e Conexões)
├── Services/
│   ├── SettingsManager.cs                  # Persistência automática em JSON (settings.json)
│   └── ZIndexManager.cs                    # Controle de camada Z, janela ativa e disparo de hibernação
├── Themes/
│   └── ThemePalette.cs                     # Paleta estática de pincéis imutáveis (Dark Mode / Light Mode)
├── Views/
│   ├── MainWindow.axaml                    # Estrutura visual da janela principal, Menu e HUD de Zoom
│   ├── MainWindow.axaml.cs                 # Gerenciamento de Workspaces, Fluxograma, Zoom/Pan e Catálogo
│   └── SettingsWindow.cs                   # Janela de configurações de tema e escala de layout
├── Scripts/
│   ├── build.sh                            # Script de publicação de executáveis e pacotes para Releases
│   ├── build-appimage.sh                   # Gerador do pacote portátil .AppImage para Ubuntu/Linux
│   ├── instalar-icone-desktop.sh           # Instala atalho com ícone na Área de Trabalho e Barra de Tarefas
│   └── build.bat                           # Script de publicação para Windows
└── AvaloniaMdiApp.csproj                   # Configuração do projeto (.NET 10) e recursos embarcados
```

---

## 🚀 Como Executar / Releases

### Baixar Executável Pronto (GitHub Releases)
Na aba **[Releases](../../releases)** do repositório no GitHub, você encontra os pacotes portáteis pré-compilados (*Self-Contained*, sem precisar instalar o .NET separadamente):
* 🐧 **Ubuntu / Linux (`AppImage`):** `GerenciadorMdiPortatil-x86_64.AppImage`
  ```bash
  chmod +x GerenciadorMdiPortatil-x86_64.AppImage
  ./GerenciadorMdiPortatil-x86_64.AppImage
  ```
  * *Nota:* Ao abrir o AppImage (ou clicando em `Preferências ▸ 📌 Criar Atalho com Ícone na Área de Trabalho`), o aplicativo registra automaticamente seu ícone na **Barra de Tarefas (Ubuntu Dock)**, no **Menu de Aplicativos** e na **Área de Trabalho**.
  * *Requisito opcional no Linux para o módulo Navegador:* `libwebkit2gtk-4.1-0` (`sudo apt install libwebkit2gtk-4.1-0`).
* 🐧 **Linux (`tar.gz` / `zip`):** `GerenciadorMdiPortatil-linux-x64.tar.gz` ou `GerenciadorMdiPortatil-linux-x64.zip`
* 🪟 **Windows (`x64`):** `GerenciadorMdiPortatil-win-x64.zip` (contendo `AvaloniaMdiApp.exe` com ícone nativo).

### Executar pelo Código-Fonte (Desenvolvimento)
Certifique-se de ter o **[.NET SDK 10.0+](https://dotnet.microsoft.com/download)** instalado:

```bash
dotnet run
```

### Gerar os Executáveis e o AppImage para o GitHub Releases
Para compilar os binários finais autocontidos (*Single-File*) e o pacote `.AppImage` na pasta `dist/`:

```bash
./Scripts/build.sh
```
Os pacotes prontos para upload no GitHub Releases serão gerados em:
* `GerenciadorMdiPortatil-x86_64.AppImage` (na raiz e em `dist/release/GerenciadorMdiPortatil-x86_64.AppImage`)
* `dist/release/GerenciadorMdiPortatil-linux-x64.tar.gz` e `.zip`
* `dist/linux/AvaloniaMdiApp`
* Para instalar o atalho com ícone diretamente na Área de Trabalho e Barra de Tarefas do Ubuntu via terminal:
  ```bash
  ./Scripts/instalar-icone-desktop.sh
  ```
