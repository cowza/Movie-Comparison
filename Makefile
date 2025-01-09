.PHONY: deploy-remote
deploy-remote: load-remote-backend load-remote-frontend
	@echo "Deploying to remote..."
	@docker --context remote compose -f docker/docker-compose.yml --profile remote up --force-recreate -d

.PHONY: deploy-local
deploy-local:
	@docker --context default compose -f docker/docker-compose.yml --profile local up --build --force-recreate -d

.PHONY: build-remote-backend
build-remote-backend:
	@echo "Building backend image for remote..."
	@docker build --secret id=local-env,src=docker/secrets/remote-env -f backend/Dockerfile -t backend ./backend

.PHONY: build-remote-frontend
build-remote-frontend:
	@echo "Building frontend image for remote..."
	@docker build --secret id=local-env,src=docker/secrets/remote-env -f frontend/app/Dockerfile -t frontend ./frontend/app

.PHONY: load-remote-backend
load-remote-backend: build-remote-backend
	@echo "Saving backend image..."
	@docker image save -o /tmp/backend.tar backend:latest
	@echo "Uploading backend image to remote..."
	@docker --context remote image load -i /tmp/backend.tar
	@rm -f /tmp/backend.tar

.PHONY: load-remote-frontend
load-remote-frontend: build-remote-frontend
	@echo "Saving frontend image..."
	@docker image save -o /tmp/frontend.tar frontend:latest
	@echo "Uploading frontend image to remote..."
	@docker --context remote image load -i /tmp/frontend.tar
	@rm -f /tmp/frontend.tar