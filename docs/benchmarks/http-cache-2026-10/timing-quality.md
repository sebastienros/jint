These rows have BenchmarkDotNet MValue > 2.8, indicating multiple timing modes. Interpret their timing means cautiously.

| Run | Storage | URLs | Operation | MValue |
| --- | --- | --- | --- | ---: |
| cand-r1 | Disk | repeated | ColdMiss | 2.900 |
| cand-r1 | Disk | distinct | ColdMiss | 2.905 |
| cand-r2 | Memory | repeated | ColdMiss | 2.842 |
| cand-r3 | Disabled | distinct | ColdMiss | 3.385 |
| cand-r3 | Memory | distinct | WarmHit | 3.364 |
| cand-r3 | Disk | distinct | ColdMiss | 3.189 |
