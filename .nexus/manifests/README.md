# Nexus GitOps deploy manifests

These manifests are the deploy contract between this repository and the
Nexus platform's GitOps deployer (`KubernetesApplicationDeployer.DeployViaArgoCdAsync`,
POM-108). When a version is deployed to an environment, the platform:

1. Resolves hierarchical configs + decrypted secrets and writes them to a
   Kubernetes Secret named `app-{name}-{envSlug}-env` in the target namespace.
2. Upserts an ArgoCD Application with a git-kustomize source:
   `path: .nexus/manifests/{envSlug}`, `targetRevision: {versionTag}`.
3. Waits up to 5 minutes for Deployment `app-{name}` to become healthy,
   rolling back the ArgoCD Application to the previous revision on failure.

## Layout

```
.nexus/manifests/
├── base/          # Deployment + Service + image transform
├── dev/           # overlay — env slug "dev"
├── staging/       # overlay — env slug "staging"
└── production/    # overlay — env slug "production"
```

## Invariants — do not break these

- **Deployment name** stays `app-__APP_NAME__` (the platform health gate
  waits on it by name).
- **Each overlay directory name** equals the Nexus environment slug.
- **envFrom secretRef** in each overlay stays `app-__APP_NAME__-{envSlug}-env`.
- **Service** stays `app-__APP_NAME__` on port 80 (the platform's central
  ingress routes `app.sassy.solutions/{orgId}/{appId}/{env}` to it).
- **Image** is only changed via the `images:` transform in
  `base/kustomization.yaml`; CI bumps `newTag` on every main build.

## Adding an environment

Copy an overlay directory, rename it to the new environment slug, and update
the secret name + `ASPNETCORE_ENVIRONMENT` / `Nexus__Environment` values in
`patch-deployment.yaml`.
