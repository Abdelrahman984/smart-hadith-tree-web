# Architecture Overview

The Smart Hadith Tree follows a modern Full-Stack architecture, strictly adhering to Clean Architecture principles on the backend and React Server Components (RSC) on the frontend.

## 1. Backend (Clean Architecture)

The .NET 9 solution is divided into four main layers:

- **Domain (`SmartHadithTree.Domain`)**: Contains the core business entities (`HadithText`, `Narrator`, `Transmission`, `ScholarEvaluation`). This layer has no dependencies on external libraries (no EF Core, no ASP.NET).
- **Application (`SmartHadithTree.Application`)**: Contains business use cases (Services), DTOs, and Interfaces. It orchestrates the domain models. Semantic Kernel is integrated here for AI processing.
- **Infrastructure (`SmartHadithTree.Infrastructure`)**: Implements the interfaces defined in the Application layer. Contains the EF Core `DbContext`, Database Migrations, and Repositories (e.g., `HadithChainRepository` which uses Recursive CTEs).
- **API (`SmartHadithTree.Api`)**: The presentation layer. Exposes the Application layer via RESTful HTTP Controllers. 

## 2. Frontend (Next.js)

The frontend is built with Next.js App Router (`frontend/src/app`).

- **Server Components vs Client Components**: We default to React Server Components (RSC) for better performance and SEO. Interactive components (like the search bar or the React Flow canvas) use `"use client"`.
- **Data Fetching**: The API client (`lib/api.ts`) communicates with the backend on `http://localhost:5147`. React Query is used for caching and state management of these requests.
- **Isnad Visualization**: `React Flow` combined with `elkjs` is used to render the complex tree of narrators. The tree is configured to render Top-to-Bottom, ensuring long Arabic names don't overlap horizontally.

## 3. Database (PostgreSQL 18)

- **Arabic search**: every searched text has a normalized copy (`NormalizedMatn`, `NormalizedBookName`, `FullIsnadText`, `Narrator.NormalizedFullName` / `NormalizedKnownAs`) without tashkeel and with alef and ta marbuta variants unified by `ArabicNormalizer`, the same function the query goes through. These columns are byte-wise (`COLLATE "C"`) and carry `pg_trgm` GIN indexes, so `Contains` (translated to `LIKE '%…%'`) is an index lookup instead of a scan. (Until 2026-10-21 the database was SQL Server with an `Arabic_100_CI_AI` collation, which ignored the tashkeel but not the letter variants: `docs/postgres_migration.md`.)
- **Graph Queries**: Retrieving a full Isnad chain requires navigating a graph. This is done in the database with a **Recursive CTE** (`WITH RECURSIVE` in `HadithChainRepository`), fetching an entire tree in a single query.

## 4. Artificial Intelligence (Semantic Kernel)

- The backend integrates **Microsoft Semantic Kernel**.
- When a user clicks on a narrator in the frontend, the backend queries the database for all classical `ScholarEvaluations` associated with that narrator.
- These evaluations are passed to an LLM (e.g., Gemini-1.5-Pro) via a strict system prompt.
- The AI acts as a RAG (Retrieval-Augmented Generation) system: it is explicitly instructed *only* to read the retrieved evaluations and summarize them into a definitive ruling (e.g., "Thiqah" - Trustworthy), preventing hallucinations.
