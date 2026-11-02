# SAP HANA Cloud connectivity

ARI-HANA-01B uses SAP's official `Sap.Data.Hana.Net.v10.0` ADO.NET provider. Runtime connection
strings and SQL commands stay inside `ARI.Infrastructure`; versioned deployment SQL lives under
`database/hana`. `ARI.Core` has no SAP dependency.

## Local configuration

From the repository root, configure the application with .NET User Secrets:

```powershell
dotnet user-secrets set --project src/ARI.Api "Hana:Host" "<instance>.hana.<region>.hanacloud.ondemand.com"
dotnet user-secrets set --project src/ARI.Api "Hana:Port" "443"
dotnet user-secrets set --project src/ARI.Api "Hana:User" "ARI_APP"
dotnet user-secrets set --project src/ARI.Api "Hana:Password" "<password>"
dotnet user-secrets set --project src/ARI.Api "Hana:ConnectionTimeoutSeconds" "15"
```

Do not put these values in `appsettings*.json`. Environment variables using double underscores,
such as `Hana__Password`, are supported for deployed environments.

The application validates host, port, user, password, and timeout when a connection is opened.
`HanaConnectionFactory` always adds these non-configurable security settings:

```text
Encrypt=True
SSLValidateCertificate=True
PersistSecurityInfo=False
```

The operating system/default SAP client trust store must trust the CA that issued the HANA Cloud
server certificate. Certificate validation cannot be disabled through ARI configuration.

## Smoke test and health semantics

Start the API in the Development environment so User Secrets are loaded:

```powershell
dotnet run --project src/ARI.Api --urls http://localhost:5080
Invoke-RestMethod http://localhost:5080/health
Invoke-WebRequest -UseBasicParsing http://localhost:5080/health/ready
```

- `/health` returns HTTP 200 whenever the API process is reachable. It does not touch HANA.
- `/health/ready` opens a fresh pooled connection, executes
  `SELECT CURRENT_USER FROM DUMMY`, and checks that the result equals `Hana:User`.
- Successful readiness returns HTTP 200 and identifies the authenticated user.
- Missing/invalid configuration, network/TLS/authentication errors, query failures, and a user
  mismatch return HTTP 503 without exposing exception or connection details.

Expected successful response:

```json
{
  "status": "Ready",
  "dependency": "SAP HANA",
  "authenticatedUser": "ARI_APP"
}
```

## Troubleshooting

- Confirm the HANA Cloud instance is running and its allowed-connections/IP policy includes this client.
- Use the SQL endpoint host and port from SAP HANA Cloud Central, without `https://`.
- Ensure `ARI_APP` exists, is not locked, and its password is current.
- Do not work around certificate errors by disabling validation; fix the client trust store instead.
