<p align="left">
  <img src="../../assets/branding/quicktext-256.png" width="72" alt="QuickText">
</p>

[English](../../README.md) · [简体中文](../../README.zh.md) · [繁體中文](README.zh-Hant.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · **Português** · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [Tiếng Việt](README.vi.md) · [ไทย](README.th.md) · [Bahasa Indonesia](README.id.md) · [हिन्दी](README.hi.md) · [বাংলা](README.bn.md) · [العربية](README.ar.md) · [Türkçe](README.tr.md)

# QuickText

> Parte do **[Plano 365 de código aberto](https://github.com/rockbenben/365opensource)** — projeto nº 023 · um gerenciador de snippets e expansor de texto na bandeja do Windows.

**Pare de digitar a mesma coisa duas vezes.** O QuickText fica na bandeja do Windows: armazene uma única vez o texto ao qual você recorre repetidamente — e-mails, endereços, assinaturas, modelos, respostas prontas, imagens — e depois, em **qualquer caixa de texto**, digite algumas teclas ou uma abreviação e ele aparece exatamente no cursor. Múltiplas linhas, caracteres especiais e emoji preservados caractere por caractere.

> Seu texto reutilizável, a poucas teclas de distância — inserido direto no cursor.

- WPF / .NET 10, executável portátil de arquivo único, **sem conta, offline por padrão** — apenas a verificação de atualizações opcional acessa o GitHub.
- Os dados são **JSON local** na sua própria pasta — coloque-os no Dropbox / OneDrive / num NAS para sincronizar.
- Tema escuro, **18 idiomas de interface** (com espelhamento da direita para a esquerda para o árabe), configurações aplicadas instantaneamente.

**[⬇ Baixar a versão mais recente](https://github.com/rockbenben/QuickText/releases/latest)** —— Windows x64, portátil em arquivo único. Não é assinado, então o SmartScreen avisa na primeira execução: **Mais informações → Executar assim mesmo**.

---

## Onde você usaria

Onde quer que você **digite a mesma coisa repetidamente no Windows**. Ele fica na bandeja e funciona em todas as caixas de texto (janelas de chat, formulários do navegador, editores, clientes de e-mail — não está preso a um único aplicativo). É um **gerenciador de trechos e expansor de texto em um só**, além de busca por pinyin, modelos com variáveis e imagens.

| Quem                                          | O que armazena                                                                                                                 |
| --------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| **Suporte / e-commerce**                      | Respostas prontas, respostas padrão, textos promocionais, QR codes ou fotos de produtos                                        |
| **Vendas / negócios**                         | Modelos de e-mail, aberturas, orçamentos, despedidas                                                                           |
| **Desenvolvedores / ops**                     | Comandos, configurações, JSON, código base (`{...}` emitido literalmente, nunca interpretado)                                  |
| **Escritório / preenchimento de formulários** | E-mail, endereço, telefone, números de documentos, modelos de atas de reunião (pedem os dados a você e lembram o último valor) |
| **RH / administrativo / jurídico**            | Avisos de integração, notificações padrão, isenções de responsabilidade — local, offline, adequado para conteúdo sensível      |

> **Antes de guardar algo sensível.** Os trechos são gravados como **JSON em texto simples**, sem criptografia e de propósito: assim você pode ler, comparar e editar o arquivo por conta própria. Para modelos e textos padrão numa máquina que só você usa, tudo bem. Também significa que um número de documento ou uma senha ali dentro pode ser lido por qualquer coisa capaz de ler o arquivo — inclusive outros programas rodando na sua conta e o serviço de sincronização, se a pasta estiver no Dropbox / OneDrive. Para segredos de verdade, use um gerenciador de senhas e deixe o QuickText para o texto que você não se importaria de deixar à vista.

## Veja funcionando em 30 segundos

Em **qualquer lugar onde você possa digitar** — digamos que você precise do seu e-mail numa caixa de chat:

1. **Invoque** — pressione `Ctrl+Shift+8`; o painel aparece sobre a janela ativa.
2. **Busque** — digite algumas letras; as correspondências são destacadas. Ele busca no nome, no pinyin (completo + iniciais), na abreviação e no corpo.
3. **Enter** — o e-mail é colado **exatamente onde estava o seu cursor**. Pronto.

> Não digite nada para navegar por categoria, com `Recentes` / `Favoritos` fixados no topo; os que você mais usa **sobem ao topo** automaticamente.
> `↑↓` movem · `←→` trocam de categoria · `Alt+1–9` seleção rápida · duplo clique para enviar · `Esc` para fechar.

## Três formas de chegar até ele — escolha a que combina

A mesma biblioteca, três formas de recorrer a ela; combine-as livremente:

| Forma                       | Como acionar                                                           | Melhor para                                                 |
| --------------------------- | ---------------------------------------------------------------------- | ----------------------------------------------------------- |
| 🔍 **Busca no painel**      | `Ctrl+Shift+8` → digite → Enter                                        | Muitos trechos, uso ocasional, navegar para escolher        |
| ⌨️ **Abreviação em linha**  | Basta digitar `;sig` e depois Espaço / Tab / Enter                     | Frases fixas de alta frequência, sem precisar do painel     |
| 🧩 **Modelo com variáveis** | Recupere um trecho com `{variables}` por qualquer uma das formas acima | E-mail / formulários: um modelo, algumas palavras alteradas |

- **Imagens também**: adicione da área de transferência ou de arquivo, coladas como imagem ao enviar; imagens podem ter abreviações, então digite a abreviação e receba a imagem (QR codes, assinaturas com logotipo).
- **Saída por trecho**: frases de chat são enviadas automaticamente após colar, trechos de código nunca são — assim não atrapalham.

Todos os detalhes sobre abreviações e variáveis estão em **Em detalhe**, abaixo — as seções **Espaços reservados** e **Abreviações**.

---

**Em detalhe**

## Funciona logo de cara

Dê um duplo clique em **`QuickText.exe`**; ele fica na **bandeja do sistema** (sem botão na barra de tarefas). Na **primeira execução** ele:

- adiciona uma pequena **biblioteca inicial** (duas categorias, no seu **idioma de interface**) para que você possa experimentar imediatamente;
- exibe um balão com a tecla de invocação — por padrão **`Ctrl+Shift+8`**.

> Os ícones no canto superior direito do painel: **＋ Novo** · **Gerenciador** (abre o editor) · **Configurações** · **📌 Fixar** (mantém o painel aberto após um envio, para disparar vários em sequência). Vá direto ao Gerenciador / Configurações sem voltar à bandeja.

## Adicione / edite seu texto

- **Bandeja → Abrir Gerenciador** — o editor completo: categorias à esquerda, trechos à direita, editor abaixo. Adicione/renomeie/exclua categorias (com uma etiqueta de **7 cores**), edite trechos (nome, abreviação, corpo, imagem). Arraste para reordenar ou mover entre categorias; `Ctrl+Z` desfaz uma exclusão.
- **Bandeja → Novo a partir da área de transferência** — cria um novo trecho a partir do conteúdo atual da área de transferência e o abre no Gerenciador para finalizar (pressione **Salvar** para gravá-lo no disco; fechar o Gerenciador pergunta se deseja salvar ou descartar).
- **Criar no painel** — digite o texto na caixa de busca e pressione `Ctrl+N` para salvá-lo como um novo snippet (esse texto vira o corpo; `@categoria …` o arquiva nessa categoria) e ir ao Gerenciador para finalizar (`Ctrl+E` edita o selecionado).

> **Texto longo / código**: o botão `⤢` acima do corpo o abre num editor quase em tela cheia (ou pressione `Ctrl+Shift+Enter` no corpo); `Esc` ou "Pronto" termina a edição; se alterou algo, pergunta primeiro se quer guardar ou descartar. Com "ativar espaços reservados {variable}" marcado, os tokens são **coloridos por tipo** — variáveis em ciano, tokens automáticos como `{date}` em âmbar, `{snippet:x}` em roxo, `{cursor}` em ciano tracejado — enquanto um nome de trecho digitado errado, um formato de data inválido ou uma chave não fechada recebem um **sublinhado ondulado vermelho** que você pode passar o mouse por cima para ver o motivo. Todos eles são colados **literalmente** no envio, algo que antes só se descobria depois de colar a coisa errada. Passar o mouse sobre um token de data também mostra uma prévia do valor resolvido. Para código, deixe a caixa desmarcada: o realce fica totalmente desativado, então um corpo cheio de `{}` permanece discreto, e a barra de status apenas informa quantos tokens serão emitidos como estão. Também incluído: **o Enter preserva a indentação** e **`Tab` recua toda uma seleção com várias linhas**.
>
> A janela ampliada **sempre mostra números de linha**, e seu rodapé oferece um seletor de **formato de código** — JSON, YAML, XML, HTML, Markdown, SQL, Python, JavaScript/TypeScript, C#, Java, PowerShell, Shell e INI, 13 ao todo — que realça a sintaxe do corpo e é lembrado por trecho. **O texto armazenado permanece simples e o que é colado não muda.** O realce de espaços reservados pinta o fundo enquanto o realce de sintaxe colore os caracteres, então os dois nunca entram em conflito: um corpo JSON com `{variables}` mostra ao mesmo tempo sua estrutura e suas variáveis.

## Espaços reservados (um modelo, muitas situações) · ativados por trecho

Os espaços reservados são um **interruptor por trecho**: marque "**Ativar espaços reservados {variable}**" no editor do Gerenciador e os tokens abaixo são resolvidos no envio; **deixado desmarcado (o padrão), o corpo é enviado literalmente** — código, scripts e JSON cheios de `{...}` literais nunca são mal interpretados nem geram perguntas.

> Ao atualizar: os espaços reservados costumavam estar sempre ativos. A primeira execução após a atualização marca automaticamente o interruptor para os **trechos existentes** cujo corpo contém `{...}`, de modo que nada muda de comportamento; desmarque-o no Gerenciador para os que realmente são código.

Quando ativados, estes espaços reservados são resolvidos no envio:

| Espaço reservado               | O que faz                                                                                                                                                                                                                                                                            |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `{name}` (qualquer rótulo)     | **Pede que você o preencha** antes de colar; **lembra o seu último valor** para que as repetições não perguntem de novo                                                                                                                                                              |
| `{name:John}`                  | Variável com um **valor padrão**, já preenchido na solicitação                                                                                                                                                                                                                       |
| `{env\|dev\|test\|prod}`       | Variável com **opções** — a solicitação mostra um menu suspenso (a primeira opção também serve de padrão; digitar livremente continua permitido)                                                                                                                                     |
| `{clipboard}`                  | Insere o conteúdo atual da área de transferência                                                                                                                                                                                                                                     |
| `{cursor}`                     | Deixa o cursor neste ponto após colar (também suprime o Enter automático)                                                                                                                                                                                                            |
| `{date}` `{time}` `{datetime}` | Insere a data/hora atual; suporta deslocamentos como `{date+7}` (7 dias à frente) e formatos personalizados como `{date=yyyy-MM-dd}` / `{time=HH:mm:ss}`, combináveis com deslocamentos: `{date+7=MM-dd}`. Os apelidos em chinês `{日期}` / `{时间}` / `{日期时间}` também funcionam |
| `{uuid}` `{random}`            | Valores aleatórios: um UUID / 6 dígitos, novos a cada ocorrência                                                                                                                                                                                                                     |
| `{snippet:name}`               | **Insere o corpo de outro trecho** (até 3 níveis de profundidade, sem ciclos) — mantenha assinaturas compartilhadas num só lugar                                                                                                                                                     |

Exemplo: uma assinatura `Best regards,\n{name}` (com espaços reservados ativados) → pede o nome no envio → cola a assinatura finalizada.

## Abreviações (expandem enquanto você digita, sem painel) · opcionais

Dê a um trecho uma **abreviação** para expandi-lo diretamente em qualquer caixa de texto. O **prefixo de acionamento** é definido uma única vez em Configurações (padrão `;`):

1. No Gerenciador, digite apenas a **abreviação em si** — por exemplo `sig` para a sua assinatura. O prefixo é adicionado automaticamente (não o redigite); o campo mostra o prefixo atual `;` na frente.
2. Em qualquer caixa de texto digite **`;sig`** (= prefixo + abreviação) e depois **Espaço / Tab / Enter** → ele exclui `;sig` e expande o corpo (um `{placeholder}` pergunta primeiro quando aquele trecho tem espaços reservados ativados).
3. **Digitou errado?** Pressione **Backspace logo em seguida** à expansão para revertê-la de volta para `;sig` (mesma janela, dentro de 5 s).

Detalhes: a correspondência **não diferencia maiúsculas de minúsculas** (`;SIG` dispara com CapsLock ligado); um erro de digitação corrigido com Backspace ainda expande; dois trechos que compartilham uma abreviação recebem um **aviso em linha** no Gerenciador; e o menu da bandeja tem um interruptor de um clique **"Pausar expansão"** (para demonstrações/jogos).

> **Sobre o prefixo:** o prefixo (padrão `;`) impede que a digitação comum / o pinyin acionem por engano uma abreviação sem prefixo. Altere-o em **Configurações → Abreviações → Prefixo de acionamento** (para `,`, `:`, …) ou **deixe-o vazio**. Uma abreviação com prefixo é autodelimitada e dispara mesmo colada ao texto anterior (por exemplo `thx;sig`); sem prefixo, ela só dispara como uma palavra isolada (nunca como o final de uma palavra maior, como `graf`). Você também pode desativar as abreviações por aplicativo (uma lista de bloqueio, por exemplo `cmd.exe; putty.exe`) ou desligá-las por completo.

## Saída e configurações comuns (Bandeja → Configurações)

- **Saída** — padrão "colar no aplicativo ativo"; ou "apenas copiar para a área de transferência" (você cola com `Ctrl+V`). Opcional: pressionar Enter após colar, enviar com um clique, restaurar a área de transferência. **Cada trecho pode substituir isso** (Gerenciador → Saída: seguir o global / colar / colar + Enter / apenas copiar) — frases de chat se enviam automaticamente, trechos de código nunca.
- **Posição do painel** — seguir a janela ativa (padrão) / seguir o cursor de texto / lembrar a última posição.
- **Método de invocação (escolha um)** — ① **combinação de teclas**: clique na caixa e pressione uma nova (teclas comuns precisam de `Ctrl`/`Alt`/`Shift`/`Win`; as teclas de função **`F1`–`F24` funcionam sozinhas**); ou ② **toque num modificador**: **toque uma ou duas vezes num modificador** (por exemplo `Ctrl` direito, `Shift` direito) para invocar (um modificador sozinho não pode ser uma tecla de atalho normal, então é detectado por toque). Escolher o toque desativa a combinação — os dois são mutuamente exclusivos, então sempre fica claro qual está ativo.
- **Tecla de captura** — segunda combinação opcional que **salva silenciosamente a área de transferência como um novo trecho** (retorno por balão, sem janela).
- **Pasta de dados** — aponte-a para um drive de sincronização; **exportar / importar backup** (zip, validado com confirmação de sobrescrita); **backup automático** diário nesta máquina (os 10 mais recentes são mantidos, acesso à pasta com um clique).
- **Idioma** — **18 idiomas**: English · 简体中文 · 繁體中文 · 日本語 · 한국어 · Español · Português · Français · Deutsch · Italiano · Русский · Tiếng Việt · ไทย · Bahasa Indonesia · हिन्दी · বাংলা · العربية (RTL) · Türkçe, trocados instantaneamente. **Iniciar com o Windows** opcional.
- **Verificar atualizações** — desativado por padrão; quando ativado, conecta-se ao GitHub uma vez ao iniciar para procurar uma versão mais recente (a única vez que acessa a internet). «Verificar agora» executa sob demanda.

## Referência rápida de teclado

| Ação                                              | Teclas                                                      |
| ------------------------------------------------- | ----------------------------------------------------------- |
| Invocar / fechar o painel                         | `Ctrl+Shift+8` (configurável) / `Esc`                       |
| Selecionar / trocar de categoria / seleção rápida | `↑↓` / `←→` / `Alt+1–9`                                     |
| Enviar                                            | `Enter` ou duplo clique (clique único opcional)             |
| Favoritar / desfavoritar                          | `Ctrl+D`                                                    |
| Criar / editar no painel                          | `Ctrl+N` / `Ctrl+E`                                         |
| Desfazer exclusão (Gerenciador)                   | `Ctrl+Z`                                                    |
| Abreviação: acionar / desfazer                    | digite a abrev + Espaço·Tab·Enter / Backspace após expandir |

---

## Recursos num relance

- **Invocação**: tecla de atalho global — uma **combinação de teclas** (as teclas de função funcionam como tecla única) ou **toque simples/duplo num modificador** (por exemplo `Ctrl` direito), **escolha um**; o painel segue a janela ativa / o cursor de texto / a posição lembrada; os botões da linha superior levam a **Novo / Gerenciador / Configurações**; fixe-o para enviar vários em sequência; arraste e redimensione com o tamanho lembrado.
- **Busca**: nome / pinyin / iniciais / abreviação / corpo, com destaque; **empates resolvidos por frequência de uso (frecency)**; `@categoria palavras-chave` limita a busca a uma categoria (apenas `@categoria` navega por ela).
- **Conteúdo**: texto simples (múltiplas linhas, caracteres especiais, emoji sem perdas), espaços reservados (padrões / menus de opções / trechos aninhados / formatos de data personalizados / uuid / random — **ativados por trecho**), **imagens** (da área de transferência ou de arquivo, coladas como imagem ao enviar; **imagens também podem ter abreviações** — digite a abreviação, receba a imagem).
- **Abreviações**: acionadas por terminador, solicitação de variáveis, desfazer com uma tecla, correção de erro de digitação com Backspace, sem diferenciar maiúsculas, o clique quebra o token, aviso de duplicidade, lista de bloqueio por aplicativo, **pausa na bandeja com um clique**.
- **Saída**: colar diretamente / apenas copiar; Enter automático opcional, restaurar a área de transferência, envio com clique único; **substituição de saída por trecho**; tecla de captura (área de transferência → trecho num só toque).
- **Gerenciador**: **editor de corpo espaçoso** (`⤢ Ampliar` abre-o na sua própria janela; as alterações não guardadas são sempre confirmadas: ao fechá-la e ao mudar para outro item), **realce de espaços reservados** (colorido por tipo; referências de trechos inexistentes / formatos de data inválidos / chaves não fechadas recebem um sublinhado ondulado vermelho com o motivo ao passar o mouse; nada é realçado quando os espaços reservados estão desativados — a barra de status avisa isso em vez disso), **compatível com código** (a janela ampliada sempre mostra números de linha e oferece 13 formatos de código para realce de sintaxe; o Enter preserva a indentação, `Tab` recua uma seleção com várias linhas, modo sem quebra de linha), 7 cores de categoria, reordenar / mover arrastando, **seleção múltipla para mover / excluir em lote** (selecione com Ctrl / Shift e clique com o botão direito), desfazer exclusão, **lixeira (restauração por 30 dias, com prévia do corpo)**, aviso de abreviação duplicada, estatísticas de uso, retorno de salvamento.
- **Dados**: JSON local, recarga a quente (mescla automaticamente edições/sincronizações externas), aviso de conflito de sincronização, exportar / importar backup, **backup automático diário (10 mantidos)**, iniciar com o Windows.
- **Localização**: **18 idiomas de interface** (chinês simplificado / tradicional, English, 日本語, 한국어, Español, Français, Deutsch, Русский, العربية …) com **espelhamento da direita para a esquerda para o árabe**, trocados ao vivo em Configurações.
- **Robustez**: instância única (uma segunda execução invoca o painel de busca em vez de instalar hooks em duplicidade); a CI executa testes mais uma verificação de fumaça de janela a cada push e publica um executável de arquivo único nas tags `v*`.

## Dados e sincronização

Pasta de dados (padrão `Documents\QuickText`, alterável em Configurações, pode apontar para um drive de sincronização):

```text
<data folder>/
  ├─ index.json        # category order + each category's file name and color
  ├─ <category>.json   # the snippets in that category (Snippet[])
  ├─ trash.json        # soft-deleted snippets (auto-purged after 30 days)
  └─ images/           # image files for image snippets
```

- `Snippet`: `{ id, name, abbr, body, useVariables, outputMode, imagePath?, updatedAt }`.
- **Gravações atômicas** (`*.tmp` → `File.Replace`) para que uma sincronização nunca leia um arquivo escrito pela metade; recarga a quente com `FileSystemWatcher` (coalescida), com uma proteção contra a própria gravação.
- O estado local da máquina permanece **fora da pasta de sincronização**: configurações em `%APPDATA%\QuickText\settings.json`, contagens de uso / favoritos em `%APPDATA%\QuickText\usage.stats` (mudam a cada envio e entrariam em conflito entre máquinas), backups automáticos diários em `%APPDATA%\QuickText\backups\`.
- **Modo portátil** (sem rastros / USB): ative-o em **Configurações → Dados → Modo portátil** — ele deixa um marcador `QuickText.portable` ao lado de `QuickText.exe` e **passa a valer na próxima reinicialização** (a primeira execução portátil leva junto suas configurações e uso, então você não precisa reconfigurar). As configurações, o uso, os backups e a biblioteca padrão passam a ficar em `<exe folder>\Data\` em vez de `%APPDATA%` / Documents, e o "iniciar com o Windows" usa um atalho na pasta Inicializar em vez do registro — assim a ferramenta inteira viaja num pen drive e não deixa nada na máquina anfitriã. O aplicativo precisa ficar num local gravável (um pen drive, não `Program Files`); a **biblioteca de textos migra via Exportar / Importar backup**. O "iniciar com o Windows" é rastreado por modo, então marque-o de novo no novo modo após trocar, se quiser. Deixe-o desligado para o layout instalado acima (a escolha certa quando a pasta de dados é um drive de sincronização).<br>_(Precisa ser um arquivo marcador, não uma simples configuração — ele decide onde o próprio `settings.json` fica; o interruptor só passa a valer na próxima inicialização, nunca perturbando a sessão atual.)_

## Marca

Os recursos ficam em `assets/branding/`: `quicktext-mark.svg` (principal), `quicktext-mark-mono.svg` (monocromático), `quicktext.ico` (16–256, ícone do app / bandeja), `quicktext-256.png`, `quicktext-social.png` (1200×630 og:image), `quicktext-social-2x.png` (2400×1260 @2x), `brand.html` (folha de marca). O símbolo é um **cursor I-beam de texto** sobre um ladrilho de menta ao lado de um bloco âmbar de texto recém-inserido — "text▮", ou seja, colocar o seu texto no cursor. Paleta "terminal dusk": menta (o realce do app `#3DC2A0`) + âmbar `#F2B457` sobre uma tinta com viés petróleo; uma tipografia sans para o logotipo combinada com a mono Cascadia Code.

## Arquitetura

Núcleo puro (sem Win32, testável em unidade) mantido separado do Win32/UI.

| Projeto                      | Conteúdo                                                                                                                                                                                                                 |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `src/QuickText.Core`         | `Models`, `Persistence` (`Store`, `UsageStore`, `JsonConfig`), `Search` (`SearchIndex`), `Abbr` (`AbbrMatcher`), `Snippets` (`Placeholders`), `Pinyin`, `Settings`, `Localization` (.resx, 18 idiomas)                   |
| `src/QuickText.App`          | UI em WPF (`SearchPanel` / `ManagerWindow` / `SettingsWindow` / `AppDialog` / `VariablesDialog`), `Ui/Theme.xaml` (tema escuro), `Interop` (`GlobalHotkey`, `KeyboardHook`, `PasteEngine`, `Autostart`, `NativeMethods`) |
| `tests/QuickText.Core.Tests` | Testes de unidade do núcleo (xUnit)                                                                                                                                                                                      |

## Compilar e executar

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App        # or run QuickText.exe under bin
```

Publique uma build portátil de arquivo único (win-x64):

```bash
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64
```

Requer o SDK do .NET 10. Apenas Windows (tecla de atalho global / hook de teclado / área de transferência do Win32).

## Sobre o Plano 365 Open Source

Projeto **#023** do [Plano 365 Open Source](https://github.com/rockbenben/365opensource) — uma pessoa + IA, mais de 300 projetos open-source em um ano. [Envie sua ideia →](https://365.aishort.top/) · [Discord](https://discord.gg/PZTQfJ4GjX) · [Telegram](https://t.me/aishort_top)

## Licença

[MIT License](../../LICENSE) · Copyright © 2026 rockbenben. Livre para usar, modificar e distribuir.
