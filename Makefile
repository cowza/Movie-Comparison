COMPOSE := docker compose
DEMO_ENV := API_BASE_URL=http://fake-upstream:8080 API_TOKEN=fake-token \
	REFRESH_INTERVAL=00:00:15 FAILURE_RETRY_INTERVAL=00:00:05 STALE_AFTER=00:00:30

-include .env
export DEPLOY_HOST

# Build and run locally against the API in .env: http://localhost:8080
.PHONY: deploy-local
deploy-local:
	@$(COMPOSE) up --build --detach --wait

.PHONY: down
down:
	@$(COMPOSE) --profile tls --profile demo down

.PHONY: logs
logs:
	@$(COMPOSE) logs --follow app

# Deploy the published image with HTTPS to DEPLOY_HOST over SSH.
.PHONY: deploy
deploy:
	@test -n "$(DEPLOY_HOST)" || (echo "Set DEPLOY_HOST in .env" >&2; exit 1)
	@DOCKER_HOST=ssh://$(DEPLOY_HOST) $(COMPOSE) --profile tls up --no-build --pull always --detach --wait

# Tests the token in .env against the real API.
.PHONY: check-upstream
check-upstream:
	@tools/check-upstream.sh .env

# The production image against the fake provider API. Needs no token.
.PHONY: demo
demo:
	@$(DEMO_ENV) $(COMPOSE) --profile demo up --build --detach --wait

.PHONY: smoke
smoke: demo
	@tools/smoke-test.sh; status=$$?; $(MAKE) --no-print-directory down; exit $$status

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
