<p align="left">
  <img src="../assets/branding/quicktext-256.png" width="72" alt="QuickText">
</p>

[English](../README.md) · [简体中文](../README.zh.md) · [繁體中文](README.zh-Hant.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Português](README.pt.md) · **Français** · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [Tiếng Việt](README.vi.md) · [ไทย](README.th.md) · [Bahasa Indonesia](README.id.md) · [हिन्दी](README.hi.md) · [বাংলা](README.bn.md) · [العربية](README.ar.md) · [Türkçe](README.tr.md)

# QuickText

> Membre du **[Plan 365 open source](https://github.com/rockbenben/365opensource)** — projet n° 023 · un gestionnaire d'extraits et expanseur de texte dans la barre d'état de Windows.

**Ne tapez plus jamais deux fois la même chose.** QuickText réside dans la zone de notification de Windows : enregistrez une seule fois les textes que vous saisissez encore et encore — e-mail, adresses, signatures, modèles, réponses toutes faites, images — puis, dans **n'importe quel champ de saisie**, tapez quelques touches ou une abréviation et le texte se dépose juste à l'emplacement de votre curseur. Multiligne, caractères spéciaux et emoji préservés caractère par caractère.

> Vos textes réutilisables à quelques frappes de distance — déposés directement au curseur.

- WPF / .NET 10, exe portable en un seul fichier, **sans compte, hors ligne par défaut** — seule la vérification des mises à jour facultative contacte GitHub.
- Les données sont du **JSON local** dans votre propre dossier — placez-le dans Dropbox / OneDrive / un NAS pour le synchroniser.
- Thème sombre, **18 langues d'interface** (avec miroir de droite à gauche pour l'arabe), les réglages s'appliquent instantanément.

---

## Où l'utiliser

Partout où vous **tapez la même chose encore et encore sous Windows**. Il réside dans la zone de notification et fonctionne dans tous les champs de saisie (fenêtres de chat, formulaires de navigateur, éditeurs, clients de messagerie — sans être lié à une seule application). C'est un **gestionnaire de fragments et un expandeur de texte (text expander) en un**, avec en plus la recherche pinyin, les modèles à variables et les images.

| Qui | Ce qu'ils enregistrent |
|---|---|
| **Support / e-commerce** | Réponses toutes faites, réponses standard, textes promotionnels, QR codes ou photos de produits |
| **Ventes / affaires** | Modèles d'e-mail, phrases d'accroche, devis, formules de politesse |
| **Développeurs / ops** | Commandes, config, JSON, code standard (`{...}` émis tel quel, jamais interprété) |
| **Bureau / remplissage de formulaires** | E-mail, adresse, téléphone, numéros de pièce d'identité, modèles de comptes rendus (vous invitent à saisir, se souviennent de la dernière valeur) |
| **RH / administratif / juridique** | Notifications d'intégration, notifications standard, clauses de non-responsabilité — local, hors ligne, adapté au contenu sensible |

## Le voir en 30 secondes

Dans **n'importe quel endroit où vous pouvez taper** — disons que vous avez besoin de votre e-mail dans une fenêtre de chat :

1. **Appeler** — appuyez sur `Ctrl+Shift+8` ; le panneau apparaît au-dessus de la fenêtre active.
2. **Rechercher** — tapez quelques lettres ; les correspondances sont surlignées. La recherche porte sur le nom, le pinyin (complet + initiales), l'abréviation et le corps.
3. **Entrée** — l'e-mail est collé **là où se trouvait votre curseur**. Terminé.

> Ne tapez rien pour parcourir par catégorie, avec `Récents` / `Favoris` épinglés en haut ; ceux que vous utilisez le plus **remontent automatiquement** en tête.
> `↑↓` déplacer · `←→` changer de catégorie · `Alt+1–9` sélection rapide · double-clic pour envoyer · `Esc` pour fermer.

## Trois façons d'y accéder — choisissez celle qui convient

Même bibliothèque, trois façons de l'exploiter ; mélangez-les librement :

| Façon | Comment déclencher | Idéal pour |
|---|---|---|
| 🔍 **Recherche par panneau** | `Ctrl+Shift+8` → taper → Entrée | Beaucoup de fragments, usage occasionnel, choisir en parcourant |
| ⌨️ **Abréviation en ligne** | Tapez simplement `;sig` puis Espace / Tab / Entrée | Phrases fixes à haute fréquence, sans panneau |
| 🧩 **Modèle à variables** | Récupérez un fragment avec `{variables}` par l'une des deux méthodes ci-dessus | E-mail / formulaires : un même modèle, quelques mots changés |

- **Les images aussi** : ajoutez depuis le presse-papiers ou un fichier, collées comme image à l'envoi ; les images peuvent avoir des abréviations, tapez donc l'abréviation et obtenez l'image (QR codes, signatures avec logo).
- **Sortie par fragment** : les phrases de chat s'envoient automatiquement après le collage, les fragments de code jamais — ils ne se gênent pas.

Tous les détails sur les abréviations et les variables se trouvent dans **En détail** ci-dessous — les sections **Espaces réservés** et **Abréviations**.

---

# En détail

## Fonctionne dès l'installation

Double-cliquez sur **`QuickText.exe`** ; il réside dans la **zone de notification** (pas de bouton dans la barre des tâches). Au **premier lancement**, il :

- dépose une petite **bibliothèque de démarrage** (deux catégories, dans votre **langue d'interface**) pour que vous puissiez l'essayer immédiatement ;
- affiche une bulle avec le raccourci d'appel — par défaut **`Ctrl+Shift+8`**.

> Les icônes en haut à droite du panneau : **＋ Nouveau** · **Gestionnaire** (ouvre l'éditeur) · **Réglages** · **📌 Épingler** (garde le panneau ouvert après un envoi, pour en enchaîner plusieurs). Accédez directement au Gestionnaire / aux Réglages sans repasser par la zone de notification.

## Ajouter / modifier vos textes

- **Zone de notification → Ouvrir le Gestionnaire** — l'éditeur complet : catégories à gauche, fragments à droite, éditeur en dessous. Ajouter/renommer/supprimer des catégories (avec une étiquette **7 couleurs**), modifier les fragments (nom, abréviation, corps, image). Glissez pour réordonner ou déplacer entre catégories ; `Ctrl+Z` annule une suppression.
- **Zone de notification → Nouveau depuis le presse-papiers** — créez un nouveau fragment à partir du presse-papiers actuel et ouvrez-le dans le Gestionnaire pour le compléter (enregistré quand le Gestionnaire est enregistré/fermé).
- **Créer dans le panneau** — tapez le texte dans le champ de recherche et appuyez sur `Ctrl+N` pour l'enregistrer comme nouveau fragment (ce texte devient le corps ; `@catégorie …` le classe dans cette catégorie) et sauter au Gestionnaire pour le compléter (`Ctrl+E` modifie celui qui est sélectionné).

## Espaces réservés (un modèle, de nombreuses situations) · activation par fragment

Les espaces réservés sont un **interrupteur par fragment** : cochez « **Activer les espaces réservés {variable}** » dans l'éditeur du Gestionnaire et les jetons ci-dessous sont résolus à l'envoi ; **laissé décoché (par défaut), le corps est envoyé tel quel** — le code, les scripts et le JSON pleins de `{...}` littéraux ne sont jamais mal interprétés ni source d'invite.

> Mise à niveau : les espaces réservés étaient auparavant toujours actifs. Le premier lancement après la mise à niveau coche automatiquement l'interrupteur pour les **fragments existants** dont le corps contient `{...}`, de sorte que rien ne change dans le comportement ; décochez-le dans le Gestionnaire pour ceux qui sont réellement du code.
> De plus : `date` / `time` / `datetime` / `now` / `日期` / `时间` / `日期时间` sont désormais des **noms réservés de date** — `{date:xyz}` est interprété comme un format de date, et non plus comme « une variable nommée date avec la valeur par défaut xyz » ; renommez les variables qui utilisaient ces noms.

Une fois activés, ces espaces réservés sont résolus à l'envoi :

| Espace réservé | Ce qu'il fait |
|---|---|
| `{name}` (n'importe quel libellé) | **Vous invite à le remplir** avant le collage ; **se souvient de votre dernière valeur** pour ne pas redemander aux répétitions |
| `{name:John}` | Variable avec une **valeur par défaut**, pré-remplie dans l'invite |
| `{env\|dev\|test\|prod}` | Variable avec des **options** — l'invite affiche une liste déroulante (la première option sert aussi de valeur par défaut ; la saisie libre reste autorisée) |
| `{clipboard}` | Insère le contenu actuel du presse-papiers |
| `{cursor}` | Laisse le curseur à cet endroit après le collage (supprime aussi l'Entrée automatique) |
| `{date}` `{time}` `{datetime}` | Insère la date/l'heure actuelle ; prend en charge les décalages comme `{date+7}` (dans 7 jours) et des formats personnalisés comme `{date:yyyy-MM-dd}` / `{time:HH:mm:ss}`, combinables avec un décalage : `{date+7:MM-dd}`. Les alias chinois `{日期}` / `{时间}` / `{日期时间}` fonctionnent aussi |
| `{uuid}` `{random}` | Valeurs aléatoires : un UUID / 6 chiffres, régénérées à chaque occurrence |
| `{snippet:name}` | **Insère le corps d'un autre fragment** (jusqu'à 3 niveaux de profondeur, sans risque de boucle) — gardez les signatures partagées à un seul endroit |

Exemple : une signature `Best regards,\n{name}` (avec espaces réservés activés) → demande le nom à l'envoi → colle la signature complète.

## Abréviations (développement au fil de la frappe, sans panneau) · optionnel

Donnez une **abréviation** à un fragment pour le développer directement dans n'importe quel champ de saisie. Le **préfixe de déclenchement** se définit une seule fois dans les Réglages (par défaut `;`) :

1. Dans le Gestionnaire, tapez uniquement l'**abréviation elle-même** — par ex. `sig` pour votre signature. Le préfixe est ajouté automatiquement (ne le retapez pas) ; le champ affiche le préfixe actuel `;` devant.
2. Dans n'importe quel champ de saisie, tapez **`;sig`** (= préfixe + abréviation) puis **Espace / Tab / Entrée** → il supprime `;sig` et développe le corps (un `{placeholder}` invite d'abord lorsque ce fragment a les espaces réservés activés).
3. **Erreur de frappe ?** Appuyez sur **Retour arrière juste après** le développement pour le rétablir en `;sig` (même fenêtre, dans les 5 s).

Détails : la correspondance est **insensible à la casse** (`;SIG` se déclenche avec Verr. Maj activé) ; une faute corrigée avec Retour arrière se développe quand même ; deux fragments partageant une abréviation reçoivent un **avertissement en ligne** dans le Gestionnaire ; et le menu de la zone de notification propose une bascule en un clic **« Suspendre le développement »** (pour les démos/jeux).

> **À propos du préfixe :** le préfixe (par défaut `;`) empêche la frappe ordinaire / le pinyin de déclencher par erreur une abréviation nue. Modifiez-le sous **Réglages → Abréviations → Préfixe de déclenchement** (en `,`, `:`, …) ou **laissez-le vide**. Une abréviation préfixée est autodélimitée et se déclenche même collée au texte précédent (par ex. `thx;sig`) ; sans préfixe, elle ne se déclenche qu'en tant que mot à part entière (jamais comme la fin d'un mot plus long comme `graf`). Vous pouvez aussi désactiver les abréviations par application (une liste noire, par ex. `cmd.exe; putty.exe`) ou les désactiver complètement.

## Sortie et réglages courants (Zone de notification → Réglages)

- **Sortie** — par défaut « coller dans l'application active » ; ou « copier uniquement dans le presse-papiers » (vous collez avec `Ctrl+V`). En option : appuyer sur Entrée après le collage, envoi au simple clic, restaurer le presse-papiers. **Chaque fragment peut remplacer ce réglage** (Gestionnaire → Sortie : suivre le global / coller / coller + Entrée / copier uniquement) — les phrases de chat s'envoient automatiquement, les fragments de code jamais.
- **Position du panneau** — suivre la fenêtre active (par défaut) / suivre le curseur de texte / se souvenir de la dernière position.
- **Méthode d'appel (choisissez-en une)** — ① **combinaison de touches** : cliquez sur le champ et appuyez sur une nouvelle (les touches ordinaires nécessitent `Ctrl`/`Alt`/`Shift`/`Win` ; les touches de fonction **`F1`–`F24` fonctionnent seules**) ; ou ② **tapoter un modificateur** : **tapotez une ou deux fois un modificateur** (par ex. `Ctrl` droit, `Shift` droit) pour appeler (un modificateur seul ne peut pas être un raccourci normal, il est donc détecté au tapotement). Choisir le tapotement désactive la combinaison — les deux sont mutuellement exclusifs, on voit donc toujours clairement lequel est actif.
- **Raccourci de capture** — deuxième combinaison optionnelle qui **enregistre silencieusement le presse-papiers comme nouveau fragment** (retour par bulle, sans fenêtre).
- **Dossier de données** — pointez-le vers un lecteur de synchronisation ; **exporter / importer une sauvegarde** (zip, validée avec confirmation d'écrasement) ; **sauvegarde automatique** quotidienne sur cette machine (les 10 plus récentes conservées, accès au dossier en un clic).
- **Langue** — **18 langues** : English · 简体中文 · 繁體中文 · 日本語 · 한국어 · Español · Português · Français · Deutsch · Italiano · Русский · Tiếng Việt · ไทย · Bahasa Indonesia · हिन्दी · বাংলা · العربية (RTL) · Türkçe, changées instantanément. **Démarrer avec Windows** en option.
- **Vérifier les mises à jour** — désactivé par défaut ; si activé, se connecte à GitHub une fois au démarrage pour vérifier s'il existe une nouvelle version (le seul moment où l'application accède à Internet). « Vérifier maintenant » l'exécute à la demande.

## Aide-mémoire clavier

| Action | Touches |
|---|---|
| Appeler / fermer le panneau | `Ctrl+Shift+8` (configurable) / `Esc` |
| Sélectionner / changer de catégorie / sélection rapide | `↑↓` / `←→` / `Alt+1–9` |
| Envoyer | `Enter` ou double-clic (simple clic en option) |
| Ajouter / retirer des favoris | `Ctrl+D` |
| Créer / modifier dans le panneau | `Ctrl+N` / `Ctrl+E` |
| Annuler la suppression (Gestionnaire) | `Ctrl+Z` |
| Abréviation : déclencher / annuler | taper l'abréviation + Espace·Tab·Entrée / Retour arrière après le développement |

---

## Fonctionnalités en un coup d'œil

- **Appel** : raccourci global — une **combinaison de touches** (les touches de fonction fonctionnent comme une seule touche) ou **tapotement simple/double d'un modificateur** (par ex. Ctrl droit), **au choix** ; le panneau suit la fenêtre active / le curseur de texte / la position mémorisée ; les boutons de la rangée supérieure sautent vers **Nouveau / Gestionnaire / Réglages** ; épinglez-le pour en envoyer plusieurs à la suite ; déplaçable et redimensionnable avec taille mémorisée.
- **Recherche** : nom / pinyin / initiales / abréviation / corps, surlignés ; **ex æquo départagés par la fréquence d'usage (frecency)** ; `@catégorie mots-clés` restreint la recherche à une catégorie (`@catégorie` seul la parcourt).
- **Contenu** : texte brut (multiligne, caractères spéciaux, emoji sans perte), espaces réservés (valeurs par défaut / listes déroulantes d'options / imbrication de fragments / formats de date personnalisés / uuid / random — **activation par fragment**), **images** (depuis le presse-papiers ou un fichier, collées comme image à l'envoi ; **les images peuvent aussi avoir des abréviations** — tapez l'abréviation, obtenez l'image).
- **Abréviations** : déclenchées par une touche de fin, invite de variables, annulation en une frappe, correction de faute par Retour arrière, insensible à la casse, un clic rompt le jeton, avertissement de doublon, liste noire par application, **suspension en un clic depuis la zone de notification**.
- **Sortie** : coller directement / copier uniquement ; en option Entrée automatique, restaurer le presse-papiers, envoi au simple clic ; **remplacement de sortie par fragment** ; raccourci de capture (presse-papiers → fragment en une frappe).
- **Gestionnaire** : 7 couleurs de catégorie, réorganisation / déplacement par glisser, **déplacement / suppression par lot en sélection multiple** (sélection Ctrl / Shift, puis clic droit), annulation de suppression, **corbeille (restauration sous 30 jours, avec aperçu du corps)**, avertissement de doublon d'abréviation, statistiques d'usage, mode sans retour à la ligne pour le code, retour après enregistrement.
- **Données** : JSON local, rechargement à chaud (fusionne automatiquement les modifications externes / la synchronisation), avis de conflit de synchronisation, exporter / importer une sauvegarde, **sauvegarde automatique quotidienne (10 conservées)**, démarrer avec Windows.
- **Localisation** : **18 langues d'interface** (chinois simplifié / traditionnel, English, 日本語, 한국어, Español, Français, Deutsch, Русский, العربية …) avec **miroir de droite à gauche pour l'arabe**, changées en direct dans les Réglages.
- **Robustesse** : instance unique (un second lancement appelle le panneau de recherche au lieu d'installer les hooks en double) ; la CI exécute les tests plus une vérification de fumée de fenêtre à chaque push et publie un exe en un seul fichier sur les étiquettes `v*`.

## Données et synchronisation

Dossier de données (par défaut `Documents\QuickText`, modifiable dans les Réglages, peut pointer vers un lecteur de synchronisation) :

```
<dossier de données>/
  ├─ index.json        # ordre des catégories + nom de fichier et couleur de chaque catégorie
  ├─ <catégorie>.json   # les fragments de cette catégorie (Snippet[])
  ├─ trash.json        # fragments supprimés en douceur (purgés automatiquement après 30 jours)
  └─ images/           # fichiers image des fragments image
```

- `Snippet` : `{ id, name, abbr, body, useVariables, outputMode, imagePath?, updatedAt }`.
- **Écritures atomiques** (`*.tmp` → `File.Replace`) pour qu'une synchronisation ne lise jamais un fichier écrit à moitié ; rechargement à chaud par `FileSystemWatcher` (fusionné), avec une garde contre les auto-écritures.
- L'état local à la machine reste **hors du dossier de synchronisation** : réglages dans `%APPDATA%\QuickText\settings.json`, compteurs d'usage / favoris dans `%APPDATA%\QuickText\usage.stats` (ils changent à chaque envoi et entreraient en conflit d'une machine à l'autre), sauvegardes automatiques quotidiennes dans `%APPDATA%\QuickText\backups\`.
- **Mode portable** (sans trace / USB) : activez-le sous **Réglages → Données → Mode portable** — il dépose un marqueur `QuickText.portable` à côté de `QuickText.exe` et **s'applique au prochain redémarrage** (le premier démarrage en mode portable transfère vos réglages et votre usage, vous n'avez donc pas à tout reconfigurer). Les réglages, l'usage, les sauvegardes et la bibliothèque par défaut résident alors sous **`<dossier de l'exe>\Data\`** au lieu de `%APPDATA%` / Documents, et « démarrer avec Windows » utilise un raccourci dans le dossier Démarrage plutôt que le registre — ainsi tout l'outil voyage sur une clé et ne laisse rien sur l'hôte. L'application doit se trouver dans un emplacement inscriptible (une clé USB, pas `Program Files`) ; la **bibliothèque de textes se déplace via Exporter / Importer une sauvegarde**. Le démarrage avec Windows est suivi par mode, donc recochez-le dans le nouveau mode après le changement si vous le voulez. Laissez-le désactivé pour la disposition installée ci-dessus (le bon choix quand le dossier de données est un lecteur de synchronisation).<br>*(Ce doit être un fichier marqueur, pas un simple réglage — il décide où se trouve `settings.json` lui-même ; le changement ne prend effet qu'au démarrage suivant, sans jamais perturber la session en cours.)*

## Marque

Les ressources se trouvent dans `assets/branding/` : `quicktext-mark.svg` (principal), `quicktext-mark-mono.svg` (monochrome), `quicktext.ico` (16–256, icône d'application / de zone de notification), `quicktext-256.png`, `quicktext-social.png` (og:image 1200×630), `quicktext-social-2x.png` (2400×1260 @2x), `brand.html` (charte de marque). Le logo est un **curseur d'insertion en I** sur une tuile menthe, à côté d'un bloc ambre de texte tout juste déposé — « text▮ », c'est-à-dire placer votre texte au curseur. Palette « terminal dusk » : menthe (l'accent de l'application `#3DC2A0`) + ambre `#F2B457` sur une encre tirant vers le sarcelle ; un logotype sans empattement associé à la police à chasse fixe Cascadia Code.

## Architecture

Un Core pur (sans Win32, testable unitairement) tenu à l'écart de Win32/UI.

| Projet | Contenu |
|---|---|
| `src/QuickText.Core` | `Models`, `Persistence` (`Store`, `UsageStore`, `JsonConfig`), `Search` (`SearchIndex`), `Abbr` (`AbbrMatcher`), `Snippets` (`Placeholders`), `Pinyin`, `Settings`, `Localization` (.resx, 18 langues) |
| `src/QuickText.App` | Interface WPF (`SearchPanel` / `ManagerWindow` / `SettingsWindow` / `AppDialog` / `VariablesDialog`), `Ui/Theme.xaml` (thème sombre), `Interop` (`GlobalHotkey`, `KeyboardHook`, `PasteEngine`, `Autostart`, `NativeMethods`) |
| `tests/QuickText.Core.Tests` | Tests unitaires du Core (xUnit) |

## Compiler et exécuter

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App        # ou exécuter QuickText.exe sous bin
```

Publier une version portable en un seul fichier (win-x64) :

```bash
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64
```

Nécessite le SDK .NET 10. Windows uniquement (raccourci global Win32 / hook clavier / presse-papiers).

## À propos du Plan 365 open source

Ceci est le projet n° 023 du [Plan 365 open source](https://github.com/rockbenben/365opensource).

Une personne + l'IA, plus de 300 projets open source en un an. [Proposez votre idée →](https://my.feishu.cn/share/base/form/shrcnI6y7rrmlSjbzkYXh6sjmzb)

## Licence

[MIT License](../LICENSE) · Copyright © 2026 rockbenben. Libre d'utilisation, de modification et de distribution.
