# Movie Price Comparison

## Overview
A web application that compares movie prices across different providers.

## Requirements
- Handle provider service interruptions gracefully
- Secure API token management without public exposure

## Assumptions
- Movie prices update daily (caching implemented)
- Users select movies from a dropdown interface
- Movie information displayed in card format showing available providers
- Clear indication of best price provider
- Provider APIs may be unstable (retry mechanism implemented)
- Partial data display continues if some provider API calls fail

## Development Setup
The following instructions assume WSL with Docker installed (tested with Docker Desktop Windows)

1. Configure Environment
   ```bash
   # Copy environment file
   cp .env.example /docker/secrets/local-env
   ```

2. Example local-env configuration:
   ```ini
   ASPNETCORE_ENVIRONMENT=Development
   ASPNETCORE_URLS=http://+:5000
   VSTEST_HOST_DEBUG=1
   ExternalApiSettings__BaseUrl=https://myapidomain.azurewebsites.net
   ExternalApiSettings__ApiToken=123456
   ```

3. Deploy locally:
   ```bash
   make deploy-local
   ```

## Production Setup
Instructions for deploying to a remote environment

1. Configure Environment
   ```bash
   # Copy environment file
   cp .env.example /docker/secrets/remote-env
   ```

2. Example remote-env configuration:
   ```ini
   ASPNETCORE_ENVIRONMENT=Production
   ASPNETCORE_URLS=http://+:5000
   ExternalApiSettings__BaseUrl=https://myapidomain.azurewebsites.net
   ExternalApiSettings__ApiToken=123456
   VIRTUAL_HOST=subdomain.domain.com
   VIRTUAL_PORT=80
   LETSENCRYPT_HOST=subdomain.domain.com
   LETSENCRYPT_EMAIL=myemail@domain.com
   ```

3. Server Setup
   - Create a DigitalOcean droplet following their [official guide](https://docs.digitalocean.com/products/droplets/how-to/create/)
   - Configure SSH key during installation
   - Install Docker if not pre-installed using the [official Docker installation guide](https://docs.docker.com/engine/install/ubuntu/#install-using-the-repository)

4. DNS Configuration
   - Create an A Name Record in your website's DNS settings
   - Point the record to your DigitalOcean Droplet IP

5. Configure Remote Context
   ```bash
   docker context create remote --docker "host=ssh://user@subdomain.domain.com"
   ```

6. Deploy to Production
   ```bash
   make deploy-remote
   ```