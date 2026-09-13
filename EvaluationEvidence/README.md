# Evaluation

This folder contains the labelled real-game benchmark and evaluation script referenced in the project proposal's deliverables: *"A public GitHub repository containing fully commented source code, the labelled real-game benchmark, and the evaluation script."*

## What's here

- **`ground_truth_labels.csv`** — 21 individually verified test cases across two independent third-party Unity projects, comparing Report Writer's automated prediction against an independently-established ground truth for each element.
- **`evaluate.py`** — reads the CSV above and computes precision, recall, and F-score. Run it yourself to reproduce the exact figures reported in the dissertation.

## Why this exists

Report Writer's automated scan (a static, Edit-Mode analysis) is only useful if its pass/fail verdicts are actually correct. To check this, each row in the CSV records a case where the *true* answer was established independently — by manually clicking that exact UI element with Contrast Meter while the game was genuinely running in Play Mode. Contrast Meter samples the actual, live, camera-rendered pixel colour at the moment of the click, which is a fundamentally different (and more directly trustworthy) measurement than Report Writer's static analysis. Comparing the two tells us how often Report Writer's automated prediction agrees with reality.

## How to run it

```
cd EvaluationEvidence
python evaluate.py ground_truth_labels.csv
```

Requires Python 3.9 or later (uses standard library only — no packages to install).

## Column reference for `ground_truth_labels.csv`

| Column | Meaning |
|---|---|
| `game` | Which evaluated project the element came from: `RedRunner` (BayatGames/RedRunner, MIT licence) or `ChopChop` (UnityTechnologies/open-project-1, Apache 2.0 licence) |
| `element` | Human-readable name of the UI text element |
| `hierarchy_path` | The element's exact location in the Unity scene hierarchy, as recorded in Report Writer's own JSON output — lets you trace any row back to the real GameObject |
| `report_writer_prediction` | PASS or FAIL, as automatically computed by Report Writer's static (Edit Mode) scan |
| `report_writer_ratio` | The exact contrast ratio Report Writer calculated |
| `ground_truth_label` | The independently-verified true answer (PASS or FAIL) |
| `ground_truth_ratio` | The exact contrast ratio Contrast Meter reported during the live, Play Mode verification click |
| `verification_method` | How ground truth was established — always `ContrastMeter (Play Mode; live click)` in this dataset, i.e. a real click on the actual running game, not an assumption |
| `selection_method` | How this element came to be included in the sample — see below |
| `notes` | Any relevant context specific to that row (e.g. why a result was unusual) |

## `selection_method` — and why it matters

A sample built entirely from elements you go looking for *because you already suspect something's wrong with them* is not a fair test of general accuracy — it only tests whether the tool gets the cases right that a human already flagged as interesting. This dataset is split into two genuinely different kinds of selection, reported both combined and separately, so a reader can judge how much of the result depends on which kind of selection was used:

- **`deliberate`** — the element was chosen on purpose, because inspecting Report Writer's raw output first suggested it might be a meaningful test case (e.g. an element showing the known white-background-fallback pattern, or a plausible design issue). Useful for investigating specific hypotheses, but not an unbiased sample.
- **`random_seed42`** / **`random_seed43`** — the element was chosen by an unbiased computer-generated random draw (Python's `random.sample()`, using the stated fixed seed number, so the exact same "random" selection can be reproduced by anyone re-running the same seed). These were not picked because anything about them looked interesting beforehand.
- **`random_adjacent`** — an element that was checked incidentally while verifying a genuinely random pick sitting next to it in the same UI panel, not itself chosen by the random-sampling process. Grouped with the deliberate subset in `evaluate.py`'s reported statistics, since it wasn't a blind selection.

`evaluate.py` reports the full combined sample, the random-only subset, and the deliberate-only subset separately, specifically so a reader isn't left wondering whether a good result is just an artefact of favourable, hand-picked test cases.

## Interpreting the one mismatch

Of the 21 verified elements, exactly one (`ChopChop / VersionNo`) shows a disagreement between Report Writer's prediction and ground truth. This is documented in the `notes` column: the live Contrast Meter reading used to establish ground truth for this specific element itself reported an "Unknown / no Main Camera" background — meaning the verification measurement's own reliability is in question for this one case, not just Report Writer's. This is reported honestly as an unresolved anomaly rather than confidently attributed to either tool being wrong.
