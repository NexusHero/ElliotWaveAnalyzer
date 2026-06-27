# Developer convenience targets. Mutation testing is intentionally NOT part of CI —
# it is slow and meant to be run on demand.

.PHONY: build test mutation

build:
	dotnet build

test:
	dotnet test

# Mutation testing with Stryker.NET. Restores the pinned local tool first, then runs
# Stryker against the API using tests/ElliotWaveAnalyzer.Tests/stryker-config.json.
mutation:
	dotnet tool restore
	cd tests/ElliotWaveAnalyzer.Tests && dotnet stryker
