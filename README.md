# dwaTop

Appli Windows (façon WindowTop) pour garder une fenêtre au premier plan, la rendre transparente, la mettre en **miniature vivante**, la passer en **mode sombre**… Écrite de zéro en C# (.NET Framework 4.8 intégré à Windows : aucun runtime à installer).

**Lancer :** `dist\dwaTop.exe` → une icône apparaît dans la zone de notification, et un petit bouton **⌄** dans la barre de titre de la fenêtre active.

## Fonctions

| Fonction | Comment |
|---|---|
| **Épingler** (toujours au-dessus) + bordure colorée | Alt+Z · bouton ⌄ · clic molette sur ⌄ |
| **Opacité** (10–100 %), opaque au survol | Alt+A · Menu+↑ / Menu+↓ · curseur du panneau |
| **Click-through** (la souris passe à travers) | Menu+X · Menu+Shift+X = l'enlever partout |
| **Miniature PiP** vivante, toujours devant, original caché | Alt+Q |
| **Cacher / afficher toutes les miniatures** | **Menu+Z** |
| **Recadrer** une zone (glisser, ou clic sur un élément détecté) | Menu+C |
| **Mode sombre** — 7 filtres, veille auto si déjà sombre | Menu+D |
| **Ancres** : icône pour les fenêtres recouvertes (clic = devant, clic droit = réduire, molette = fermer, glisser un fichier = devant) | panneau / paramètres |
| **Flou** derrière la fenêtre, **mode verre** (fond transparent) — expérimental | panneau |
| **Règles par appli** (réappliquées à chaque nouvelle fenêtre) | panneau → « Mémoriser » |
| Panneau d'actions | bouton ⌄ ou Menu+Espace |
| Réinitialiser la fenêtre | Menu+Retour arrière |

« Menu » = la touche Menu à droite du clavier. Tapée seule, elle ouvre toujours le menu contextuel.

**Sur une miniature :** glisser = déplacer (aimantée aux bords) · molette = taille · Ctrl+molette = opacité · survol = grand aperçu · double-clic = restaurer l'original · clic molette = fermer · clic droit = menu (interagir, taille, opacité, recadrer…). En mode *interagir*, clics / molette / clavier sont transmis à la vraie fenêtre (Shift+Échap pour sortir).

Tous les raccourcis se changent dans **Paramètres → Raccourcis**.

## Commandes

`dwaTop.exe <action>` envoie une action à l'instance lancée (ex. depuis LayerKeyApp) :
`pin`, `opacity`, `opacityUp`, `opacityDown`, `clickThrough`, `clickThroughOff`, `pip`, `hidePips`, `crop`, `dark`, `blur`, `glass`, `anchor`, `anchorsAll`, `panel`, `reset`, `resetAll`, `settings`, `quit`.

> Si **LayerKeyApp** tourne, il capte la touche Menu avant dwaTop : mets plutôt `dwaTop.exe hidePips` (etc.) comme action dans LayerKeyApp.

## Sécurité

- En quittant, toutes les fenêtres retrouvent leur état d'origine.
- Si dwaTop plante, l'état d'origine est noté dans `%APPDATA%\dwaTop\session.txt` et restauré au lancement suivant.
- Les fenêtres lancées en administrateur ne peuvent être modifiées que si dwaTop est lui-même lancé en admin.

## Développement

- `build.bat` → `dist\dwaTop.exe` (compilateur C# de Windows, aucun SDK).
- `tests\run-tests.bat` → 54 tests (raccourcis, filtres, config, mode sombre réel, miniature, opacité, règles, récupération après plantage…). La fenêtre cible est hors écran ; seul un carré de 24 px clignote ~1 s dans le coin.
- Config : `%APPDATA%\dwaTop\config.json` (variable `DWATOP_DATA` pour un dossier de test).

| Fichier | Rôle |
|---|---|
| `src/Manager.cs` | état des fenêtres, épingler / opacité / click-through / miniatures / règles / restauration |
| `src/Pip.cs` | miniature DWM + aperçu au survol + interaction |
| `src/Overlays.cs` | mode sombre (loupe Windows + matrices de couleur), bordure |
| `src/Crop.cs` | sélection de zone (hook souris + UI Automation) |
| `src/Toolbar.cs` | bouton de barre de titre + panneau rapide |
| `src/Anchors.cs` | ancres |
| `src/Hotkeys.cs` | raccourcis globaux, combinaisons avec la touche Menu, événements fenêtres |
| `src/SettingsForm.cs`, `src/App.cs` | paramètres, zone de notification |

Limites connues : le mode sombre n'épargne pas les images ; le mode verre ne marche qu'avec les applis qui dessinent en GDI (Bloc-notes, Explorateur…) ; l'interaction dans une miniature dépend de l'appli.
