#!/usr/bin/env node
/*
 * Serveur web minimal pour l'application Wave Generator.
 *
 * Aucune dépendance : utilise uniquement les modules intégrés de Node.js.
 * Sert les fichiers statiques du dossier courant (dont index.html).
 *
 * Utilisation :
 *     node server.js            // port 8000 par défaut
 *     node server.js 9000       // port personnalisé
 *
 * Puis ouvrez http://localhost:8000 (l'audio Web fonctionne sur localhost).
 */

const http = require("http");
const fs = require("fs");
const path = require("path");

const DEFAULT_PORT = 8000;
const port = parseInt(process.argv[2], 10) || DEFAULT_PORT;
const root = __dirname;

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".gif": "image/gif",
  ".ico": "image/x-icon",
  ".webmanifest": "application/manifest+json",
  ".wav": "audio/wav",
  ".mp3": "audio/mpeg",
};

const server = http.createServer((req, res) => {
  // Nettoie l'URL et empêche de sortir du dossier racine.
  let urlPath = decodeURIComponent(req.url.split("?")[0]);
  if (urlPath === "/") urlPath = "/index.html";

  const filePath = path.join(root, path.normalize(urlPath));
  if (!filePath.startsWith(root)) {
    res.writeHead(403);
    res.end("403 Forbidden");
    return;
  }

  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
      res.end("404 Not Found");
      return;
    }
    const ext = path.extname(filePath).toLowerCase();
    res.writeHead(200, { "Content-Type": MIME[ext] || "application/octet-stream" });
    res.end(data);
  });
});

server.listen(port, () => {
  const url = `http://localhost:${port}`;
  console.log("Wave Generator — serveur démarré");
  console.log(`  Dossier servi : ${root}`);
  console.log(`  Adresse       : ${url}`);
  console.log("  Arrêt         : Ctrl+C");
});

server.on("error", (err) => {
  if (err.code === "EADDRINUSE") {
    console.error(`Le port ${port} est déjà utilisé. Essayez : node server.js 8080`);
  } else {
    console.error(`Erreur serveur : ${err.message}`);
  }
  process.exit(1);
});
