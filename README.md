# Wave Generator — Mobile

Générateur d'ondes audio (application web autonome). Toute l'application tient
dans un seul fichier : **`index.html`**. Elle utilise l'API Web Audio, qui exige
d'être servie par un serveur web (elle ne fonctionne pas de façon fiable en
ouvrant simplement le fichier `index.html` directement dans le navigateur).

Ce dépôt fournit un petit serveur web sans aucune dépendance à installer.

## Démarrage rapide sur un nouvel ordinateur

1. Récupérez le dépôt :

   ```bash
   git clone https://github.com/tibalotte/wave-generator-mobile.git
   cd wave-generator-mobile
   ```

2. Lancez le serveur avec **l'une** des méthodes ci-dessous (selon ce qui est
   déjà installé sur la machine — aucune installation supplémentaire n'est
   nécessaire).

### Option A — Python (le plus courant)

Python 3 est préinstallé sur macOS et la plupart des Linux, et disponible sur
Windows.

```bash
python3 server.py
```

Sur Windows, la commande est souvent `python` :

```bash
python server.py
```

### Option B — Node.js

Si Node.js est installé :

```bash
node server.js
```

### Option C — sans script (une seule ligne)

Si vous ne voulez pas utiliser les scripts fournis :

```bash
python3 -m http.server 8000
```

## Utilisation

Une fois le serveur démarré, ouvrez :

```
http://localhost:8000
```

(Les scripts `server.py` et `server.js` ouvrent le navigateur automatiquement.)

Pour changer de port, passez-le en argument :

```bash
python3 server.py 8080
# ou
node server.js 8080
```

Pour arrêter le serveur : `Ctrl+C`.

## Accès depuis un téléphone sur le même réseau Wi-Fi

L'application est pensée pour le mobile. Pour l'ouvrir sur votre téléphone :

1. Trouvez l'adresse IP locale de l'ordinateur (par ex. `192.168.1.42`) :
   - macOS / Linux : `ipconfig getifaddr en0` ou `hostname -I`
   - Windows : `ipconfig`
2. Sur le téléphone (connecté au même Wi-Fi), ouvrez `http://192.168.1.42:8000`.

Note : certaines fonctions audio (comme le micro) peuvent exiger un contexte
sécurisé (HTTPS) selon le navigateur. Sur `localhost`, tout fonctionne ; en accès
réseau par IP, la lecture de sons fonctionne, mais l'entrée micro peut être
bloquée par le navigateur tant qu'on n'est pas en HTTPS.

## Contenu du dépôt

- `index.html` — l'application complète (Wave Generator).
- `server.py` — serveur web statique en Python 3, sans dépendance.
- `server.js` — serveur web statique en Node.js, sans dépendance.
