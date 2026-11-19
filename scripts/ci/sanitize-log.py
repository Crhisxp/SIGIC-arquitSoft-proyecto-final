#!/usr/bin/env python3
"""Remove configured credentials, SCRAM verifiers and password fields from logs."""
import os
from pathlib import Path
import re
import sys

content = Path(sys.argv[1]).read_text() if len(sys.argv) > 1 else sys.stdin.read()
for key in ('SIGIC_DB_PASSWORD', 'SIGIC_DB_ADMIN_PASSWORD'):
    value = os.environ.get(key)
    if value:
        content = content.replace(value, '[REDACTED]')
content = re.sub(r'SCRAM-SHA-256\$[^\s"\']+', '[REDACTED SCRAM]', content)
content = re.sub(r'(?i)(password\s*[=:]\s*)([^;\s]+)', r'\1[REDACTED]', content)
sys.stdout.write(content)
