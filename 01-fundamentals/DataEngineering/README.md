SQL Programming
Derive business insights for a food delivery app by writing SQL queries
Comprehensive coverage of topics from intermediate-level concepts such as Case Statements and subqueries to advanced SQL functions such as joins and analytical functions
Application of window functions as lead, lag functions to evaluate day-over-day insight on business performance
Use rank and dense rank functions to understand merchants’ reach in the market
Complex SQL problems on customer-merchant pairwise dependence using a variety of functions and operators
Deep dive into joins, their type, and comparison of left join vs. right join vs. outer join vs. broadcast join
Thematic coverage of frequently asked interview problems through template problems
A step-by-step guide to what you can expect in an interview and how to tackle them in a time-constrained environment
2

Data Modeling
Design Data Warehouse tables for Uber or a similar ride-sharing platform
Coming up with a conceptual and logical model, define data granularity
Define the fact and dimension tables with high-level attributes
Best practices on how to choose keys and constraints for the entities
Discussion on how to normalize tables
How to handle cases of Slowly Changing Dimensions
Thematic discussion on interview problems from Meta, Amazon, Twitter, and Uber
Learn how to decide your data warehouse schema: Star vs. Snowflake schema design
A step-by-step guide to approaching atypical interview questions
3

ETL and Pipeline Design
Create a data pipeline for near-real-time ingestion of Netflix clickstream/playback data. Design for ad-hoc monitoring of certain metrics
Comprehensive coverage of different stages of design: Upstream, ETL environment, and downstream requirements
Gain interview perspective on essential ETL design techniques such as handling data ingestion, different file formats, data granularity, landing and storage levels, and reporting metrics
Detailed outline of performance parameters depending on data granularity, volume, velocity, accepted latency, etc.
A top-down approach to building a high-level architecture: Identify available technology at each stage
Follow-up questions:
How often do you update your data in DW?
Pipeline has been fine for 6 months; now, certain marketplaces have more aggressively incoming data. How would you handle that? What changes would you make to your design if new data is more unstructured? 
Discussion on trivial but important questions: What is being monitored? Does everything go into one monitoring dashboard? 
What would the architecture look like for the ML platform that uses this data? 
Discussion on the role of DE in large-scale, multi-faceted systems, what you can expect in an interview, and how to tackle them in a time-constrained environment
4

Data Platforms
Design a data platform for a gaming company. Understand data-driven approach in deciding business metrics
Breaking down high-level components of Data Platform design: Ingestion, Warehousing, Transformation, Catalog and Governance, Privacy & Access, and Visualization
Structured discussion on how to define data flow and come up with a DAG
Learn how to design high-performance platforms at scale
How do you implement a production-ready design using Kafka and Spark? Orchestrate your pipeline using Airflow (or alternate services)
How do you define your success metrics? How do you gauge the relevance of your data? At what frequency do we capture and process it? 
How do we ensure data backup, and at what scale? 
Discussion of optimization techniques at scale like partitioning, distributed platform, cloud services, etc.
An insightful discussion on Product Sense, working with different aspects of data engineering systems, what you can expect in an interview, and how to tackle them in a time-constrained environment