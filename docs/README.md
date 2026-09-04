# Jint documentation

The documentation site is built with VitePress.

## Run locally

From the repository root:

```bash
cd docs
npm install
npm run dev
```

VitePress prints the local URL, normally `http://localhost:5173/`.

## Build the production site

```bash
cd docs
npm install
npm run build
npm run preview
```

GitHub Actions runs the same production build for pull requests. A push to
`main` that changes the site or its workflow publishes the result to GitHub
Pages.
