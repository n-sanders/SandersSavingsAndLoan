# Sanders Savings and Loan (SSL)

A simple self-hosted family savings tracker. Kids report completed chores and reading; the banker approves pay (or adjusts it) and can also deposit or withdraw directly. Everything lives in a single SQLite file.

## Run with Docker

```bash
docker compose up --build
```

Open [http://localhost:8080](http://localhost:8080).

The SQLite database is stored on the host at `./data/ssl.db` (volume-mounted into the container).

### Backup

```bash
cp data/ssl.db data/ssl-backup-$(date +%Y%m%d).db
```

## Run locally (without Docker)

Requires the .NET 9 SDK.

```bash
cd src
dotnet run
```

By default the app writes to `../data/ssl.db` relative to the project. Override with:

```bash
ConnectionStrings__Default="Data Source=C:\path\to\ssl.db" dotnet run
```

## Default logins

| Username | Passphrase | Role |
|----------|------------|------|
| banker   | banker     | Banker (admin) |
| evie     | evie       | Kid |
| noah     | noah       | Kid |
| hannah   | hannah     | Kid |
| judah    | judah      | Kid |
| ezra     | ezra       | Kid |

These are seeded only when the database is empty. Change them in a future update, or wipe `data/ssl.db` and restart after editing the seeder.

## Who can do what

- **Kids** — see their balance, submit completed tasks with a suggested payment, and view their own task and transaction history.
- **Banker** — see all balances, approve/reject pending tasks (approve deposits the final amount), make deposits and withdrawals without a task, and view any account’s ledger.

Only the banker can move money in v1.
