#!/usr/bin/env bash
# Runs unprivileged inside a systemd unit delegated only for this invocation.
set -euo pipefail
phase="$1"
output="$2"
case "$phase" in verify|calibrate|compare) ;; *) echo 'Unknown phase' >&2; exit 2 ;; esac
config="$output/setup/config.json"
if [[ "$phase" == verify ]]; then
    python3 tools/browser-comparison/measure.py --config "$config" --output "$output/verification"
    exit
fi
# DelegateSubgroup keeps the controller out of the empty delegation root.
relative=$(awk -F: '$1 == "0" {print $3}' /proc/self/cgroup)
[[ "$relative" == */controller ]] || { echo 'Expected a dedicated systemd controller subgroup' >&2; exit 1; }
root="/sys/fs/cgroup${relative%/controller}"
[[ -z "$(cat "$root/cgroup.procs")" ]] || { echo 'Delegation root is populated' >&2; exit 1; }
printf '+cpu +memory' > "$root/cgroup.subtree_control"
mkdir "$root/measurements"
printf '+cpu +memory' > "$root/measurements/cgroup.subtree_control"
printf '%s\n' "$root/measurements" > "$output/cgroup-root.txt"
# Calibration and comparison share the same VM, installations, accounting and gates.
python3 tools/browser-comparison/measure.py --config "$config" --output "$output/calibration" --measure --rounds 6 --launches 3 --scope-root "$root/measurements" --calibrate jint
python3 tools/browser-comparison/analyze.py "$output/calibration" > "$output/calibration-analysis.json"
if [[ "$phase" == compare ]]; then
    python3 tools/browser-comparison/measure.py --config "$config" --output "$output/comparison" --measure --rounds 6 --launches 3 --scope-root "$root/measurements"
    python3 tools/browser-comparison/analyze.py "$output/comparison" > "$output/comparison-analysis.json"
fi
