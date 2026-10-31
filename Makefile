COMPOSE := docker compose -f docker/docker-compose.yml

.PHONY: test
test: test-backend test-frontend

# Runs in Docker so the only local requirement is Docker itself.
.PHONY: test-backend
test-backend:
	@docker build --target backend-test --progress plain .

.PHONY: test-frontend
test-frontend:
	@cd frontend/app && bun install --frozen-lockfile && bun run lint && bun run test

# Regenerates the OpenAPI document and frontend types after an API change. Needs the .NET 10 SDK.
.PHONY: openapi
openapi:
	@cd backend && UPDATE_OPENAPI=1 dotnet test tests/MovieComparison.IntegrationTests --filter OpenApiContractTests
	@cd frontend/app && bun run gen:api

# The production image against the fake upstream. No token needed: http://localhost:8080
.PHONY: demo
demo:
	@$(COMPOSE) --profile demo up --build --force-recreate -d

.PHONY: demo-down
demo-down:
	@$(COMPOSE) --profile demo down

# Hot-reloading development stack against the real API: http://localhost:3000
.PHONY: deploy-local
deploy-local:
	@$(COMPOSE) --profile local up --build --force-recreate -d

# Tests the real API with the token in docker/secrets/local-env
.PHONY: check-upstream
check-upstream:
	@tools/check-upstream.sh docker/secrets/local-env
