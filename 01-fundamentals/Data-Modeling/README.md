# Data Modeling (Fundamentals)

This folder contains patterns, checklists, and examples for designing analytical data models and warehouses. It focuses on principles you should apply across domains and links to specific examples (see `Ride-Sharing`).

## What you'll find here

- A short checklist of clarifying questions to ask before designing a model
- Guidance on data granularity and modeling process (conceptual -> logical -> physical)
- Best practices for keys, constraints, and normalization for analytical systems
- Patterns for Slowly Changing Dimensions (SCDs)
- Decision guidance: Star vs Snowflake
- Interview-focused tips and common question themes

## Quick checklist — questions to ask (and why they matter)
Ask these before you start designing; answers drive granularity, storage patterns, and update strategies.

1. What are the primary analytical/use cases?
   - Reporting (daily summaries), ad-hoc BI, ML features, real-time dashboards?
   - Why it matters: Determines granularity, freshness, and indexing choices.

2. What is the expected data volume and growth rate?
   - Events per second/day, rows per table, retention period.
   - Why: Affects partitioning, sharding, and storage format (columnar vs row).

3. What latency / freshness is required?
   - Near real-time, hourly, daily batch?
   - Why: Determines streaming vs batch ETL design and SCD approach.

4. What data must be preserved for compliance?
   - PII, GDPR/CCPA, retention windows.
   - Why: Drives masking, encryption, and access controls.

5. Which entities are natural candidates for dimensions vs facts?
   - Users, drivers, vehicles, locations, time, payments, trips.
   - Why: Factorization into facts/dimensions reduces redundancy and speeds queries.

6. How important is history (auditability) vs simplicity?
   - Keep historical attribute changes (SCD Type 2) or overwrite (Type 1)?
   - Why: Impacts schema complexity and storage cost.

7. How will keys be assigned and joined across systems?
   - Use surrogate keys in DW or rely on natural keys from OLTP?
   - Why: Surrogate keys simplify joins and SCDs; natural keys tie back to source.

8. Security and access patterns?
   - Who needs access to what data? Row-level/security policies?

9. What are the SLAs for ETL failures and reprocessing?
   - Tolerance for late-arriving data, idempotency requirements.

10. Are there pre-existing schemas or downstream consumers to stay compatible with?


## Suggested process (conceptual -> logical -> physical)

1. Gather requirements and answer the checklist above.
2. Draw a conceptual model: main entities and their relationships (high-level boxes).
3. Decide granularity (the grain) for each fact table.
4. Convert conceptual model into logical model: define facts, dimensions, attributes.
5. Choose primary keys (surrogate) and natural keys to track source mappings.
6. Design physical schema: partitions, indexes, column types, storage format (Parquet/ORC/CSV), and retention.
7. Plan ETL for SCDs, late-arriving data, and reprocessing.
8. Add monitoring and data quality checks (row counts, null rates, key violations).


## Where to go next
Open the `Ride-Sharing` example for a complete walk-through (conceptual model, fact & dimension table definitions, SCD patterns, and interview-style questions).
