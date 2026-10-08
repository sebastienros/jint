| Storage | URLs | Operation | Baseline ms | Candidate ms | Paired time Δ% [95% CI] | Allocated MB, baseline → candidate |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| Disabled | repeated | ColdMiss | 6.956 | 7.100 | +2.51 [+1.08, +4.89] | 60.19 → 60.20 |
| Disabled | repeated | WarmHit | 7.140 | 6.895 | -0.42 [-4.80, +1.28] | 60.19 → 60.20 |
| Disabled | repeated | StaleRevalidation | 6.819 | 6.958 | +2.72 [-0.07, +9.74] | 60.19 → 60.20 |
| Disabled | repeated | Control | 3.884 | 3.840 | -0.65 [-3.65, +3.01] | 9.01 → 9.01 |
| Disabled | distinct | ColdMiss | 7.147 | 6.858 | -1.44 [-4.04, -0.99] | 60.20 → 60.21 |
| Disabled | distinct | WarmHit | 6.828 | 6.839 | -1.26 [-1.36, +0.31] | 60.20 → 60.21 |
| Disabled | distinct | StaleRevalidation | 6.868 | 6.804 | +0.29 [-1.88, +0.95] | 60.20 → 60.21 |
| Disabled | distinct | Control | 3.838 | 3.852 | -0.13 [-2.06, +6.98] | 9.01 → 9.01 |
| Memory | repeated | ColdMiss | 6.751 | 7.194 | +5.65 [+3.17, +6.72] | 60.19 → 60.65 |
| Memory | repeated | WarmHit | 6.849 | 7.189 | +7.13 [+4.63, +8.82] | 60.19 → 60.48 |
| Memory | repeated | StaleRevalidation | 6.812 | 7.138 | +4.79 [+1.65, +18.92] | 60.19 → 60.48 |
| Memory | repeated | Control | 3.805 | 3.846 | +1.36 [+0.84, +4.31] | 9.01 → 9.01 |
| Memory | distinct | ColdMiss | 6.975 | 8.821 | +26.47 [+26.18, +28.45] | 60.20 → 113.51 |
| Memory | distinct | WarmHit | 6.942 | 8.074 | +16.31 [+10.43, +23.74] | 60.20 → 60.49 |
| Memory | distinct | StaleRevalidation | 6.924 | 9.318 | +36.89 [+28.66, +38.29] | 60.20 → 62.19 |
| Memory | distinct | Control | 3.876 | 3.787 | -2.22 [-6.36, -0.92] | 9.01 → 9.01 |
| Disk | repeated | ColdMiss | 6.853 | 8.601 | +25.04 [+16.04, +28.46] | 60.19 → 60.67 |
| Disk | repeated | WarmHit | 6.650 | 7.901 | +18.30 [+17.89, +31.06] | 60.41 → 60.79 |
| Disk | repeated | StaleRevalidation | 6.796 | 8.987 | +24.23 [+23.41, +34.23] | 60.19 → 60.50 |
| Disk | repeated | Control | 3.786 | 3.745 | -1.69 [-5.45, +1.20] | 9.01 → 9.01 |
| Disk | distinct | ColdMiss | 6.842 | 113.957 | +1566.20 [+1493.73, +1592.77] | 60.20 → 116.33 |
| Disk | distinct | WarmHit | 6.657 | 32.565 | +387.70 [+378.33, +406.67] | 60.42 → 82.02 |
| Disk | distinct | StaleRevalidation | 6.984 | 131.039 | +1763.88 [+1743.04, +1864.31] | 60.20 → 65.12 |
| Disk | distinct | Control | 3.785 | 3.738 | -1.72 [-5.08, +1.11] | 9.01 → 9.01 |
