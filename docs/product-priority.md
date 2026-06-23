# Product priority — aggregation (debil-be)

Source: `src/ReviewInsights.Api/Features/Products/ProductsService.cs`

The backend computes a product-level priority by aggregating the per-review priority tags assigned by the worker (`agent`). Three ideas drive the algorithm.

---

## 1. Recency weighting (exponential decay)

A review from yesterday is more relevant than one from two years ago. Each review gets a weight:

```
weight = e^( -ln(2) / 90 × age_in_days )
```

In plain English: **a review halves in weight every 90 days**. A one-week-old review counts ~95%; a six-month-old review counts ~25%; a two-year-old review counts ~5%.

> **Stale guard:** If the newest review is older than 365 days the product automatically gets `low` — there's nothing recent to act on.

---

## 2. Small-sample protection (Wilson lower bound)

Imagine a product with exactly 1 review tagged `critical`. Its raw critical rate is 100% — but that single review might just be a one-off. To avoid false alarms the algorithm uses a **statistical lower bound**: "what's the lowest plausible true rate given this evidence, at 95% confidence?"

The intuition:

| Reviews tagged critical | Total reviews | Raw rate | Lower bound used |
|---|---|---|---|
| 1 | 1 | 100% | ~5% |
| 5 | 10 | 50% | ~24% |
| 20 | 30 | 67% | ~48% |

The lower bound grows toward the raw rate as sample size increases. **A product needs to consistently accumulate bad reviews before reaching `critical`.** One or two bad reviews won't do it.

---

## 3. Classification thresholds

After computing weighted shares and lower bounds, priority is assigned top-down — first matching condition wins:

| Priority | Condition |
|---|---|
| **critical** | Wilson lower bound of *critical share* ≥ **20%** |
| **critical** | *Critical share* ≥ 1.5× the class average AND ≥ 10% (catches outliers even in a bad category) |
| **high** | Wilson lower bound of *critical + high share* ≥ **30%** |
| **medium** | Raw *critical + high + medium share* ≥ **40%** |
| **low** | Everything else |

The **dominant rule** (the `rule_id` with the highest recency-weighted count) is surfaced as `priorityRule` on the product so it's always clear *why* a product landed where it did.

---

## Constants

| Constant | Value | Meaning |
|---|---|---|
| `PriorityHalfLifeDays` | 90 | Review weight halves every 90 days |
| `StaleCutoffDays` | 365 | Products with no review in 12 months → low |
| `CriticalWilsonThreshold` | 20% | Wilson LB needed for direct critical |
| `HighWilsonThreshold` | 30% | Wilson LB (crit+high) needed for high |
| `MediumShareThreshold` | 40% | Raw non-low share needed for medium |
| `ClassOutlierMultiplier` | 1.5× | How much worse than category average triggers critical |
| `ClassOutlierMinShare` | 10% | Minimum critical share for the outlier check |

---

## See also

- Per-review priority tagging (worker): [`agent/docs/priority.md`](../../agent/docs/priority.md)
- System overview and worker integration: [`agent/docs/overview.md`](../../agent/docs/overview.md)
