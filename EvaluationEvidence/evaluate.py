"""
evaluate.py — Precision/recall/F-score evaluation for Accessibility UI Tester's
Report Writer module, against an independently-verified ground-truth benchmark.

Ground truth was established via live Contrast Meter (Play Mode) sampling —
a different measurement pathway from Report Writer's static (Edit Mode)
analysis — across two independent third-party Unity projects (Red Runner,
BayatGames/RedRunner, MIT; Chop Chop, UnityTechnologies/open-project-1).

Sample composition: 15 elements deliberately selected to test specific
limitation categories identified during exploratory analysis, plus 6 drawn
via unbiased random sampling with fixed, documented seeds (42 and 43) for
reproducibility. See ground_truth_labels.csv's `selection_method` column.

Usage:
    python evaluate.py [path/to/ground_truth_labels.csv]

Outputs the confusion matrix and precision/recall/F1 for the full combined
sample, then the same metrics restricted to only the randomly-sampled subset
(reported separately, since a smaller denominator from the same single error
produces a noisier estimate — see dissertation Results chapter for discussion).
"""

import csv
import sys
from pathlib import Path


def load_labels(csv_path: str) -> list[dict]:
    with open(csv_path, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def confusion_matrix(rows: list[dict]) -> dict:
    """Positive class = FAIL (a detected contrast/accessibility violation)."""
    tp = fp = fn = tn = 0
    mismatches = []

    for row in rows:
        predicted = row["report_writer_prediction"].strip().upper()
        truth = row["ground_truth_label"].strip().upper()

        if predicted == "FAIL" and truth == "FAIL":
            tp += 1
        elif predicted == "FAIL" and truth == "PASS":
            fp += 1
            mismatches.append(row)
        elif predicted == "PASS" and truth == "FAIL":
            fn += 1
            mismatches.append(row)
        elif predicted == "PASS" and truth == "PASS":
            tn += 1

    return {"tp": tp, "fp": fp, "fn": fn, "tn": tn, "mismatches": mismatches}


def compute_metrics(cm: dict) -> dict:
    tp, fp, fn, tn = cm["tp"], cm["fp"], cm["fn"], cm["tn"]
    n = tp + fp + fn + tn

    precision = tp / (tp + fp) if (tp + fp) > 0 else float("nan")
    recall = tp / (tp + fn) if (tp + fn) > 0 else float("nan")
    f1 = (
        2 * precision * recall / (precision + recall)
        if (precision + recall) > 0
        else float("nan")
    )
    accuracy = (tp + tn) / n if n > 0 else float("nan")

    return {"n": n, "precision": precision, "recall": recall, "f1": f1, "accuracy": accuracy}


def print_report(title: str, rows: list[dict]) -> None:
    cm = confusion_matrix(rows)
    metrics = compute_metrics(cm)

    print(f"\n{'=' * 60}")
    print(title)
    print("=" * 60)
    print(f"n = {metrics['n']}")
    print(f"TP={cm['tp']}  FP={cm['fp']}  FN={cm['fn']}  TN={cm['tn']}")
    print(f"Precision = {metrics['precision']:.4f}")
    print(f"Recall    = {metrics['recall']:.4f}")
    print(f"F1-score  = {metrics['f1']:.4f}")
    print(f"Accuracy  = {metrics['accuracy']:.4f}")

    if cm["mismatches"]:
        print("\nMismatches:")
        for row in cm["mismatches"]:
            print(
                f"  - [{row['game']}] {row['element']}: "
                f"predicted {row['report_writer_prediction']} ({row['report_writer_ratio']}), "
                f"ground truth {row['ground_truth_label']} ({row['ground_truth_ratio']})"
            )
            if row.get("notes"):
                print(f"    Note: {row['notes']}")
    else:
        print("\nNo mismatches — 100% classification agreement in this subset.")


def main() -> None:
    csv_path = sys.argv[1] if len(sys.argv) > 1 else "ground_truth_labels.csv"

    if not Path(csv_path).exists():
        print(f"Error: {csv_path} not found.")
        print("Usage: python evaluate.py [path/to/ground_truth_labels.csv]")
        sys.exit(1)

    rows = load_labels(csv_path)

    print_report(f"FULL COMBINED SAMPLE (n={len(rows)})", rows)

    # Note: "random_adjacent" entries were clicked incidentally while
    # verifying a true random draw sitting next to them, not selected by
    # the seeded random.sample() process itself — grouped with the
    # deliberate subset for reporting purposes, matching the dissertation's
    # stated n=6 random-sample figure (random_seed42 + random_seed43 only).
    random_rows = [
        r for r in rows
        if r["selection_method"].startswith("random_seed")
    ]
    print_report(
        f"RANDOM-SAMPLE-ONLY SUBSET (n={len(random_rows)}) "
        "— reported separately for transparency about selection-bias risk "
        "in the combined figure above",
        random_rows,
    )

    deliberate_rows = [
        r for r in rows
        if r["selection_method"] in ("deliberate", "random_adjacent")
    ]
    print_report(
        f"DELIBERATE-SELECTION SUBSET (n={len(deliberate_rows)}) "
        "— elements chosen to specifically test known limitation categories",
        deliberate_rows,
    )

    print(f"\n{'=' * 60}")
    print("Per RedRunner/ChopChop breakdown:")
    print("=" * 60)
    for game in sorted(set(r["game"] for r in rows)):
        game_rows = [r for r in rows if r["game"] == game]
        print_report(f"{game} (n={len(game_rows)})", game_rows)


if __name__ == "__main__":
    main()
