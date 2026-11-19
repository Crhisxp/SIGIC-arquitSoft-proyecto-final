#!/usr/bin/env python3
"""Merge Cobertura by assembly/source/line; gate Domain's own unit-test suite."""
import argparse
from collections import defaultdict
import json
from pathlib import Path, PurePosixPath
import sys
from xml.etree import ElementTree as ET


def source_key(filename, sources, repo):
    filename = filename.replace('\\', '/')
    # Cobertura emits relative-to-source paths; make absolute/relative forms agree.
    candidates = [filename] + [str(PurePosixPath(s.replace('\\', '/')) / filename) for s in sources]
    for candidate in candidates:
        normalized = candidate.replace('\\', '/')
        for marker in ('/src/', '/tests/'):
            if marker in normalized:
                return marker[1:] + normalized.split(marker, 1)[1]
        if normalized.startswith(('src/', 'tests/')):
            return normalized
        try:
            relative = Path(normalized).resolve().relative_to(repo)
            if relative.parts[0] in ('src', 'tests'):
                return relative.as_posix()
        except ValueError:
            pass
    return PurePosixPath(filename).as_posix()


def merge(reports, repo):
    hits = {}
    for report in reports:
        root = ET.parse(report).getroot()
        sources = [s.text for s in root.findall('./sources/source') if s.text]
        for package in root.findall('./packages/package'):
            assembly = package.attrib['name']
            for cls in package.findall('./classes/class'):
                filename = source_key(cls.attrib['filename'], sources, repo)
                for line in cls.findall('./lines/line'):
                    key = (assembly, filename, int(line.attrib['number']))
                    hits[key] = hits.get(key, False) or int(line.attrib['hits']) > 0
    return hits


def summarize(hits):
    totals = defaultdict(lambda: [0, 0])
    for (assembly, _, _), hit in hits.items():
        totals[assembly][0] += int(hit)
        totals[assembly][1] += 1
    return {name: {'covered': covered, 'valid': valid, 'percent': 100 * covered / valid}
            for name, (covered, valid) in sorted(totals.items())}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('results', type=Path)
    parser.add_argument('--threshold', type=float, default=95)
    args = parser.parse_args()
    if not 0 <= args.threshold <= 100:
        parser.error('threshold must be between 0 and 100')
    reports = sorted(args.results.rglob('coverage.cobertura.xml'))
    domain_reports = sorted((args.results / 'Domain').rglob('coverage.cobertura.xml'))
    if not reports or not domain_reports:
        raise SystemExit('Missing current coverage, including TestResults/Domain.')
    repo = Path(__file__).resolve().parents[2]
    combined = merge(reports, repo)
    domain = {k: v for k, v in merge(domain_reports, repo).items() if k[0] == 'SIGIC.Domain'}
    if not domain:
        raise SystemExit('Domain unit suite has no SIGIC.Domain executable lines.')
    assemblies = summarize(combined)
    covered, valid = sum(combined.values()), len(combined)
    output = {
        'scope': 'unique assembly/source/line, OR of hits; no average of percentages',
        'reports': len(reports), 'assemblies': assemblies,
        'combined': {'covered': covered, 'valid': valid, 'percent': 100 * covered / valid},
        'gate': {'assembly': 'SIGIC.Domain', 'suite': 'Domain',
                 'covered': sum(domain.values()), 'valid': len(domain),
                 'percent': 100 * sum(domain.values()) / len(domain), 'threshold': args.threshold},
    }
    (args.results / 'coverage-summary.json').write_text(json.dumps(output, indent=2) + '\n')
    print(json.dumps(output, indent=2))
    if 100 * sum(domain.values()) < args.threshold * len(domain):
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
