# =============================================================================
# Makefile — softNerd CardGameStore
# Uso: make <comando>
# Requer: Docker Desktop + make (via WSL, Git Bash ou Chocolatey)
# =============================================================================

.PHONY: up down restart logs build clean status

## Sobe todos os containers (build + start)
up:
	docker compose up --build -d

## Para e remove os containers (mantém os dados)
down:
	docker compose down

## Reinicia todos os containers
restart:
	docker compose restart

## Mostra os logs em tempo real
logs:
	docker compose logs -f

## Rebuild sem cache (quando algo estiver travado)
build:
	docker compose build --no-cache

## Para containers e APAGA todos os volumes (reseta o banco)
clean:
	docker compose down -v
	@echo "Volumes apagados. Banco de dados resetado."

## Status dos containers
status:
	docker compose ps
