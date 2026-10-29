# Local quotation HTTP smoke test

This test uses the actual quotation sender at `D:\Project\ITP-Quotation` and the actual POTracker receiving controller, hosted with isolated in-memory databases. It starts a temporary HTTP receiver on an available loopback port, generates a temporary API key, runs the sender, and stops the receiver. No business database is accessed or changed. It does not test SQL transaction behavior or database concurrency.

From the POTracker workspace in PowerShell:

```powershell
$receiverOutput = Join-Path (Get-Location) 'tests/QuotationConnectionSmoke/Receiver/out/'
dotnet build tests/QuotationConnectionSmoke/Receiver/Receiver.csproj "-p:OutputPath=$receiverOutput"
dotnet build tests/QuotationConnectionSmoke/Sender/Sender.csproj
python tests/QuotationConnectionSmoke/run.py
```

The sender project reference uses the user's local quotation project path. Update that reference if moving the project. This test verifies first delivery, persistent order reference, duplicate retry, changed-data conflict, invalid key rejection, and received material values.
