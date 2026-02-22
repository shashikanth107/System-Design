# Ride-Sharing — Data Modeling Example

This document walks through designing a data warehouse model for a ride-sharing platform (think Uber/Lyft). It covers the questions to ask, a conceptual and logical model, the grain for facts, fact & dimension tables with attributes, keys and constraints guidance, normalization trade-offs, SCD handling, interview-focused discussion, Star vs Snowflake decision, and a step-by-step approach for atypical interview questions.

---

## 1) Clarifying questions to ask (with suggested answers)
These are the clarifying questions you should ask during interviews or initial design sessions and recommended answers based on typical ride-sharing use cases.

Q1: What are the main analytics and reporting use cases?
- Suggested answer: Daily active riders, trips per city, driver earnings, revenue per km, churn analysis, heatmaps of demand, real-time surge detection, ML features for ETA and pricing.

Q2: What is the required freshness of the data?
- Suggested answer: Core analytics (daily) via batch; near real-time (seconds to minutes) for surge detection and live dashboards via streaming.

Q3: Expected data volume?
- Suggested answer: Start: 1M events/day; scale: 10M+ events/day. Design partitions and compression assuming growth.

Q4: What is the desired retention and regulatory constraints?
- Suggested answer: Trips retained for 3–7 years (analytics), PII masked/hashed and retained per legal requirements.

Q5: What is the grain of the primary fact (trip) table?
- Suggested answer: Each completed trip event — one row per trip (trip_id) with start/end timestamps, driver_id, rider_id, fare, distance, duration, payment_id, and location references.

Q6: Should vehicle and driver attributes be historically tracked?
- Suggested answer: Driver attributes like rating or status should be SCD-tracked (Type 2 for major changes). Vehicle assignments per trip are captured on the fact row.

Q7: Are late-arriving events expected (e.g., delayed GPS or payment confirmation)?
- Suggested answer: Yes — ETL must handle late-arriving updates and be idempotent.

Q8: Are aggregations precomputed (materialized) or computed at query time?
- Suggested answer: Precompute daily aggregates for heavy dashboards; ad-hoc queries use columnar storage and partitions.


---

## 2) Conceptual & logical model (high level)

Conceptual entities:
- Rider (User)
- Driver
- Vehicle
- Trip (Fact)
- Payment
- Location (lat/lon + geohash)
- Time
- Promotion / Coupon
- Rating / Feedback

Logical view (star schema centered on `fact_trip`):
- Fact: fact_trip
- Dimensions: dim_user, dim_driver, dim_vehicle, dim_time, dim_location (origin/destination), dim_payment, dim_promo

Granularity (the grain):
- fact_trip grain = one row per completed trip. If you need per-segment analytics (per GPS segment), create a separate fact (fact_trip_segment) with a different grain.


---

## 3) Fact and Dimension tables (high-level attributes)

Fact table: `fact_trip` (grain: 1 row per completed trip)
- surrogate keys and natural keys:
  - trip_sk (surrogate PK)
  - trip_id (natural/external)
- foreign keys to dimensions:
  - user_sk, driver_sk, vehicle_sk, start_location_sk, end_location_sk, time_sk, payment_sk, promo_sk
- measures:
  - fare_amount, distance_meters, duration_seconds, surge_multiplier, commission, tip_amount
- metadata:
  - created_at, updated_at, status (completed/cancelled/no_show)

Dimension: `dim_user`
- user_sk (PK, surrogate)
- user_id (natural, from source)
- signup_date, signup_city, user_type (rider/driver-both), hashed_email, country
- current_rating, lifetime_trips, status

Dimension: `dim_driver`
- driver_sk (PK)
- driver_id (natural)
- signup_date, onboarding_region, vehicle_type_primary, license_state
- current_rating, active_since, driver_status

Dimension: `dim_vehicle`
- vehicle_sk (PK)
- vehicle_id (natural)
- make, model, year, plate_hash, vehicle_type

Dimension: `dim_time`
- time_sk (PK)
- date, hour, minute, day_of_week, week_of_year, month, quarter, year

Dimension: `dim_location`
- location_sk (PK)
- geohash, city, region, country, lat, lon, location_type (pickup/dropoff zone)

Dimension: `dim_payment`
- payment_sk (PK)
- payment_id (natural), payment_method, payment_provider, currency

Dimension: `dim_promo`
- promo_sk (PK)
- promo_id, campaign_name, promo_type, discount_amount, valid_from, valid_to


---

## 4) Best practices: keys and constraints

1. Use surrogate integer keys in the warehouse for all dimensions (`*_sk`) — simpler joins, smaller indexes, and easier SCD implementation.
2. Keep natural/source keys as attributes (e.g., `user_id`, `driver_id`) for traceability and deduplication.
3. Enforce uniqueness on business natural keys in staging if possible to detect duplicates (e.g., trip_id unique per source system).
4. Use NOT NULL constraints on surrogate PKs and required measures (fare_amount, start_time) where appropriate — but avoid strict NOT NULLs on volatile fields to prevent ETL failures.
5. Prefer foreign key constraints in design docs, but many analytic warehouses (Redshift, BigQuery) relax strict FK enforcement for performance; still keep them in metadata and QA checks.
6. Index or partition on high-cardinality join keys and common filter columns (time_sk, start_location_sk, city).
7. Use hashing for sensitive natural keys (e.g., email, plate) and store them as hashed values.


---

## 5) Normalization vs denormalization (trade-offs)

- For analytical warehouses, a denormalized star schema is usually preferable: simpler queries, fewer joins, and better performance in columnar stores.
- Normalize when dimension attributes are large and shared (e.g., a complex address hierarchy) and when storage needs to be reduced at the expense of join complexity (snowflake). Use normalization selectively.
- Keep slowly changing attributes in dimensions, not in fact tables (fact tables should be immutable measures referencing dimension versions via surrogate keys).


---

## 6) Handling Slowly Changing Dimensions (SCD)

Common strategies to track changes in dimensional attributes:

SCD Type 0 — Retain original value (never change)
- Use for immutable values like original signup source (if you never update historically).

SCD Type 1 — Overwrite
- Simple: update row in place. Use when history is not required (e.g., correcting typos).
- SQL example:
  UPDATE dim_driver SET current_rating = 4.9 WHERE driver_id = 'd_123';

SCD Type 2 — Row versioning (recommended for many attributes)
- Add `effective_from`, `effective_to`, and `is_current` flags. Insert a new row when the attribute changes and close the old row.
- Pros: full history and correct historical joins for past facts.
- SQL pseudo-flow:
  1. For incoming driver update, find current row (is_current=TRUE) and compare attributes.
  2. If changed: set is_current=FALSE, effective_to = now() on old row; insert new row with is_current=TRUE and effective_from = now().

SCD Type 3 — Add columns for previous values
- Store limited history in extra columns (e.g., previous_rating). Useful for a small number of historical attributes.

Recommendation: Use Type 2 for driver and user attributes where historical correctness matters (e.g., driver region or status), Type 1 for attributes where history is irrelevant.


---

## 7) Star vs Snowflake — how to decide

Star schema (denormalized dims):
- Pros: simpler queries, faster lookup in OLAP, fewer joins.
- Cons: more storage, some data redundancy.

Snowflake (normalized dims):
- Pros: less redundancy, smaller storage footprint for large attribute sets.
- Cons: more complex queries and joins; may reduce performance on columnar stores.

Decision rule for ride-sharing:
- Default to Star unless a dimension is extremely large and shared with many facts (e.g., a full administrative region hierarchy). If you need to normalize a dimension, keep it limited (partial snowflaking) and maintain surrogate keys to keep joins efficient.


---

## 8) Interview-focused themes & sample questions (Meta, Amazon, Twitter, Uber)

What interviewers often probe in data-modeling questions:
- Clarifying questions and constraints (data volume, freshness, SLAs).
- Choice of grain and why (important to explain trade-offs).
- Choosing dimensions vs facts and reasoning about joins.
- Handling late-arriving data and idempotency of ETL.
- SCD approach: whether to use Type 1/2/3 and why.
- Partitioning and indexing strategy for large tables (time-based, city-based).
- Query patterns: how would you compute daily active riders, average earnings per driver, or surge hotspots?
- Edge cases: canceled trips, refunds, split payments, GPS points crossing regions.

Sample interview prompt and an approach:
- Prompt: "Design an analytics schema to answer: "Which drivers earned the most during last month in San Francisco?"
  1. Clarify grain: one row per trip.
  2. Identify dimensions: driver, time, location.
  3. Fact measures: fare_amount, tip_amount, commission.
  4. Partition: by date (month/day) and city.
  5. Query: aggregate fact_trip by driver_sk where start_city='San Francisco' and date between X and Y, order by sum(fare + tip - commission) desc.

Another sample prompt: "How to handle refunds that arrive later and adjust monthly revenue?"
- Use an adjustments fact table or an events table that tracks refunds and link adjustments to original trip_id; materialize monthly aggregates from base facts + adjustments to keep historical correctness.


---

## 9) Step-by-step guide for atypical interview questions

1. Restate the problem and confirm constraints.
2. Ask clarifying questions (see section 1).
3. State your chosen grain explicitly.
4. Sketch the high-level star schema on paper.
5. Explain how facts and dimensions map to queries.
6. Describe ETL flow briefly (staging, dedupe, SCD handling, load to dims, load to facts).
7. Discuss scalability (partitions, clustering, indexes) and failure handling (reprocessing).
8. Address edge cases and how they are represented (cancellations, partial refunds, transfers).
9. Summarize trade-offs and why the chosen design meets the requirements.


---

## 10) Minimal SQL examples

Create a simple `fact_trip` and `dim_time` (Postgres-style for clarity):

```sql
CREATE TABLE dim_time (
  time_sk BIGINT PRIMARY KEY,
  date DATE NOT NULL,
  hour SMALLINT,
  day_of_week SMALLINT,
  month SMALLINT,
  year SMALLINT
);

CREATE TABLE dim_driver (
  driver_sk BIGINT PRIMARY KEY,
  driver_id TEXT UNIQUE,
  driver_name TEXT,
  is_current BOOLEAN,
  effective_from TIMESTAMP,
  effective_to TIMESTAMP
);

CREATE TABLE fact_trip (
  trip_sk BIGINT PRIMARY KEY,
  trip_id TEXT UNIQUE,
  user_sk BIGINT,
  driver_sk BIGINT,
  time_sk BIGINT,
  start_location_sk BIGINT,
  end_location_sk BIGINT,
  distance_meters FLOAT,
  fare_amount NUMERIC(10,2),
  tip_amount NUMERIC(10,2),
  created_at TIMESTAMP
);

-- Example aggregation: top drivers in a period
SELECT d.driver_id, SUM(f.fare_amount + f.tip_amount) AS earnings
FROM fact_trip f
JOIN dim_driver d ON f.driver_sk = d.driver_sk
JOIN dim_time t ON f.time_sk = t.time_sk
WHERE t.date BETWEEN '2025-10-01' AND '2025-10-31'
GROUP BY d.driver_id
ORDER BY earnings DESC
LIMIT 10;
```


---

## 11) Next steps and suggestions for a practical exercise

1. Wire a small sample dataset (CSV) of trips and load into staging tables.
2. Implement a Type 2 SCD handling script for `dim_driver` and `dim_user`.
3. Build `fact_trip` loads that dedupe on `trip_id` and link to correct `*_sk` using lookup queries.
4. Create daily materialized aggregates (e.g., daily_revenue_by_city) and validate with SQL.

That's a complete starter blueprint for designing a ride-sharing analytic model. If you want, I can also:
- Generate sample CSVs and a small ETL script (Python) to populate these tables,
- Create SQL tests that validate SCD behavior and deduplication,
- Or expand to include trip-segment fact tables (per-GPS-segment analytics).

Tell me which follow-up you'd like and I'll implement it next.