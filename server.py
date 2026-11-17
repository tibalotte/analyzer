#!/usr/bin/env python3
"""Serveur web minimal pour l'application Wave Generator.

Aucune dépendance : utilise uniquement la bibliothèque standard de Python 3.
Sert les fichiers statiques du dossier courant (dont index.html) et ouvre
le navigateur automatiquement.

Utilisation :
    python3 server.py            # port 8000 par défaut
    python3 server.py 9000       # port personnalisé

Puis ouvrez http://localhost:8000 (l'audio Web fonctionne sur localhost).
"""

import http.server
import socketserver
import sys
import os
import webbrowser
from functools import partial

DEFAULT_PORT = 8000


def main():
    port = DEFAULT_PORT
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
        except ValueError:
            print(f"Port invalide : {sys.argv[1]} — utilisation du port {DEFAULT_PORT}")

    # Sert le dossier où se trouve ce script (donc index.html à côté).
    root = os.path.dirname(os.path.abspath(__file__))

    handler = partial(http.server.SimpleHTTPRequestHandler, directory=root)

    # allow_reuse_address évite « Address already in use » après un redémarrage.
    socketserver.TCPServer.allow_reuse_address = True

    try:
        with socketserver.TCPServer(("", port), handler) as httpd:
            url = f"http://localhost:{port}"
            print("Wave Generator — serveur démarré")
            print(f"  Dossier servi : {root}")
            print(f"  Adresse       : {url}")
            print("  Arrêt         : Ctrl+C")
            try:
                webbrowser.open(url)
            except Exception:
                pass
            httpd.serve_forever()
    except OSError as exc:
        print(f"Impossible de démarrer sur le port {port} : {exc}")
        print("Essayez un autre port, par ex. : python3 server.py 8080")
        sys.exit(1)
    except KeyboardInterrupt:
        print("\nServeur arrêté.")


if __name__ == "__main__":
    main()
