# Community_C container

The image is built from the repository root so that Docker can restore the
project before copying the remaining source files.

## Local build

```powershell
docker build --file Community_C/Dockerfile --tag community-c:local .
```

## Local run with Compose

Create the local runtime environment file first. It is intentionally excluded
from the Docker build context and must not be committed.

```powershell
if (-not (Test-Path Dockerfile/community-c.env)) {
    Copy-Item Dockerfile/community-c.env.example Dockerfile/community-c.env
}
docker compose --file Dockerfile/compose.yaml up --build
```

The application is then available at `http://localhost:8080`.

Runtime secrets are supplied only through environment variables. The Dockerfile
does not copy local `appsettings.json`, development settings, key files, or env
files into the image.

The CI workflow validates the .NET solution first and then builds this image
without pushing it.

## Publish to GHCR

The publish workflow runs only for version tags such as `v1.0.0`, or when it is
started manually from GitHub Actions. It repeats the Release build and tests,
then publishes the image to GitHub Container Registry with version and commit
SHA tags.

```text
ghcr.io/mango125/community-c:v1.0.0
ghcr.io/mango125/community-c:sha-<commit>
```

The workflow authenticates with GitHub's short-lived `GITHUB_TOKEN`; no
registry password is stored in the repository.

## Production deployment

Version tags publish the image first and then deploy it to the configured
production environment over SSH. The server keeps runtime secrets in
`/opt/community-c/community-c.env`; they are never copied into the image or the
GitHub Actions runner.

Install `compose.production.yaml` and `deploy.sh` in `/opt/community-c` once.
The deployment script pulls the requested semantic-version image, recreates the
`community-c` container with the existing port and persistent mounts, checks the
local HTTP endpoint, and rolls back to the previous image when the health check
fails.

The `production` GitHub environment requires these secrets:

- `DEPLOY_HOST`
- `DEPLOY_PORT`
- `DEPLOY_USER`
- `DEPLOY_SSH_KEY`
- `DEPLOY_KNOWN_HOSTS`
