"""Regression cases for the CI denominator and cross-suite hit union."""
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('sigic_coverage', Path(__file__).with_name('coverage.py'))
coverage = importlib.util.module_from_spec(spec)
spec.loader.exec_module(coverage)


def report(path, packages, source='/work/src/SIGIC.Domain/'):
    classes = lambda rows: ''.join(
        '<class name="C" filename="' + filename + '"><lines>' +
        ''.join(f'<line number="{n}" hits="{hit}"/>' for n, hit in lines) +
        '</lines></class>' for filename, lines in rows)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text('<coverage><sources><source>' + source + '</source></sources><packages>' + ''.join(
        f'<package name="{name}"><classes>{classes(rows)}</classes></package>'
        for name, rows in packages) + '</packages></coverage>')


class CoverageTests(unittest.TestCase):
    def test_duplicate_reports_classes_and_complementary_hits(self):
        with tempfile.TemporaryDirectory() as temp:
            a, b = [Path(temp) / x / 'coverage.cobertura.xml' for x in ('a', 'b')]
            report(a, [('SIGIC.Domain', [('Entities/P.cs', [(1, 1), (2, 0)]),
                                         ('Entities/P.cs', [(1, 1)])])])
            report(b, [('SIGIC.Domain', [('/work/src/SIGIC.Domain/Entities/P.cs', [(1, 0), (2, 1)])])])
            hits = coverage.merge([a, a, b], Path('/work'))
            self.assertEqual((sum(hits.values()), len(hits)), (2, 2))

    def test_assemblies_with_same_filename_stay_distinct(self):
        with tempfile.TemporaryDirectory() as temp:
            p = Path(temp) / 'coverage.cobertura.xml'
            report(p, [('SIGIC.Domain', [('A.cs', [(1, 1)])]),
                       ('SIGIC.Application', [('A.cs', [(1, 0)])])])
            stats = coverage.summarize(coverage.merge([p], Path('/work')))
            self.assertEqual(stats['SIGIC.Domain']['percent'], 100)
            self.assertEqual(stats['SIGIC.Application']['percent'], 0)

    def test_other_suite_cannot_hide_domain_unit_coverage_failure(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            report(root / 'Domain/a/coverage.cobertura.xml',
                   [('SIGIC.Domain', [('A.cs', [(1, 1), (2, 0)])])])
            report(root / 'Infrastructure/a/coverage.cobertura.xml',
                   [('SIGIC.Domain', [('A.cs', [(1, 1), (2, 1)])])])
            result = subprocess.run([sys.executable, str(Path(coverage.__file__)), str(root)],
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 1)
            self.assertIn('"covered": 2', result.stdout)
            self.assertIn('"percent": 50.0', result.stdout)

    def test_missing_domain_fails_closed(self):
        with tempfile.TemporaryDirectory() as temp:
            result = subprocess.run([sys.executable, str(Path(coverage.__file__)), temp],
                                    capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Missing current coverage', result.stderr)


if __name__ == '__main__':
    unittest.main()
