# Evaluation report — not yet executed

**Status:** template only. No metrics in this document are measured results. Copy to `docs/reports/YYYY-MM-DD-evaluation.md` when a real run exists.

## Reproduction manifest

| Field | Actual value |
| --- | --- |
| Run timestamp / operator | Pending |
| Git commit / working tree status | Pending |
| OS / CPU / RAM / GPU / VRAM | Pending |
| SDK / Node / package locks / container digests | Pending |
| Ollama version / chat model digest / quantization | Pending |
| Embedding model digest / vector dimensions | Pending |
| Corpus document versions and hashes | Pending |
| Prompt / chunker / retrieval versions | Pending |
| Chunk size / overlap / topK / threshold / context budget | Pending |
| Deadline / concurrency / cold-warm protocol | Pending |
| Exact setup and runner commands | Pending implementation |
| Raw artifact location | Pending |

## Results

| Metric | Development 01–20 | Holdout 21–30 | Combined | Gate met? |
| --- | --- | --- | --- | --- |
| Overall cases passed / total | Not run | Not run | Not run | Unknown |
| Answerable accuracy count and % | Not run | Not run | Not run | Unknown |
| Citation precision count and % | Not run | Not run | Not run | Unknown |
| Citation coverage count and % | Not run | Not run | Not run | Unknown |
| Retrieval recall@6 count and % | Not run | Not run | Not run | Unknown |
| Unknown/escalation count and % | Not run | Not run | Not run | Unknown |
| Clarification full-interaction pass | Not run | Not run | Not run | Unknown |
| Warm completed-answer p50 / p95 | Not run | Not run | Not run | Unknown |
| Cold completed-answer p50 / p95 | Not run | Not run | Not run | Unknown |
| Embedding / retrieval / generation timings | Not run | Not run | Not run | Unknown |
| Infrastructure failure count / total | Not run | Not run | Not run | Unknown |

Declare whether aggregates use first runs or all repeated runs; retain all observations. For repeated quality runs, show per-case consistency and worst failure rather than cherry-picking the best answer. Report uncertainty due to small sample sizes.

## Per-case evidence

Include all 30 cases, each with expected behavior, retrieved IDs, final answer, cited IDs/quotes, reviewer verdict/reason, latency, request ID and artifact link. List failed attempts as well as successes. Distinguish retrieval miss, generation error, unsupported claim, wrong citation, inappropriate abstention and infrastructure failure.

## Usability and reliability

Record tested viewport/device/browser, automated accessibility results, manual keyboard/screen-reader checks and five-user task outcomes. Record restart/outage/restore experiments, elapsed recovery, duplicated/lost records and fixes. Attach evidence rather than asserting “all tests passed” without command/output references.

## Release decision

Pending. State gates passed/failed, unresolved issues and their severity, limitations, next corrective actions and whether the candidate is approved for the local demonstration. Production readiness requires the separate operational/security prerequisites.
