# Upstream test suites, 2026-10-13

Latest valid `scripts/upstream-tests.sh` run per suite, recounted from the TRX files under
`logs/upstream-tests/<run>/` (not committed). Earlier runs that hit harness bugs (missing SDK HOME,
`--report-trx`, Register in Release, Auth integration rows double-counting the unit lane) are left out.

| Suite                          | Repository @ commit                 | Run              | Total | Passed | Failed | Skipped | Exit |
| ------------------------------ | ----------------------------------- | ---------------- | ----- | ------ | ------ | ------- | ---- |
| storage                        | `altinn-storage` @ `daa7654`        | 20261013T014232Z | 1437  | 1437   | 0      | 0       | 0    |
| events                         | `altinn-events` @ `fcc0a70`         | 20261013T013301Z | 481   | 478    | 0      | 3       | 0    |
| authentication                 | `altinn-authentication` @ `938bc13` | 20261013T013301Z | 628   | 626    | 0      | 2       | 0    |
| authorization (unit)           | `altinn-auth` @ `f33afeff`          | 20261013T013301Z | 302   | 302    | 0      | 0       | 0    |
| authorization (integration)    | `altinn-auth` @ `f33afeff`          | 20261013T013301Z | 157   | 157    | 0      | 0       | 0    |
| accessmanagement (unit)        | `altinn-auth` @ `f33afeff`          | 20261013T013301Z | 1101  | 1101   | 0      | 0       | 0    |
| accessmanagement (integration) | `altinn-auth` @ `f33afeff`          | 20261013T013301Z | 1055  | 1055   | 0      | 0       | 0    |
| register                       | `altinn-register` @ `1bdb478`       | 20261013T023111Z | 1638  | 1637   | 0      | 1       | 0    |
| profile                        | `altinn-profile` @ `156d8c1`        | 20261013T014232Z | 914   | 914    | 0      | 0       | 0    |

Commands are the ones in `scripts/upstream-tests.sh` at this commit. Host prerequisites:
`fs.inotify.max_user_instances=1024` (Authentication/Auth) and Docker (Testcontainers, isolated
Storage/Profile databases).
