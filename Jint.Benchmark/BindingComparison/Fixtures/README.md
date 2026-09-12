# Parser fixture

`wide-report.csv` preserves the 53-column header, eight workload rows and job metadata shape of an actual
BenchmarkDotNet 0.15.8 gate report. **All timing, distribution, GC and allocation values were replaced with
synthetic constants; this file is not measurement evidence.** Its job name identifies that substitution.

The original collector sampled the first 4096 characters, ending inside a row. Python's CSV sniffer then
rejected this valid report because the truncated record had a different number of fields. This fixture
keeps the report wider than that sample and exercises both comma and semicolon delimiters.
