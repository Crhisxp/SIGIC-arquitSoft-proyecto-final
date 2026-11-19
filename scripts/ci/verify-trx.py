#!/usr/bin/env python3
"""Require four nonempty, successful suites with zero skipped/aborted tests."""
from pathlib import Path
import sys
from xml.etree import ElementTree as ET

root = Path(sys.argv[1] if len(sys.argv) > 1 else 'TestResults')
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
for layer in ('Domain', 'Application', 'Infrastructure', 'WebApi'):
    reports = list((root / layer).rglob('*.trx'))
    if len(reports) != 1:
        raise SystemExit(f'{layer}: expected one current TRX, found {len(reports)}')
    tree = ET.parse(reports[0])
    counters = tree.find('.//t:ResultSummary/t:Counters', ns)
    if counters is None:
        raise SystemExit(f'{layer}: missing counters')
    total = int(counters.attrib['total'])
    passed = int(counters.attrib['passed'])
    if total <= 0 or passed != total or int(counters.attrib['executed']) != total:
        raise SystemExit(f'{layer}: non-passing or skipped tests: {counters.attrib}')
    print(f'{layer}: {passed}/{total}, zero skipped tests')
